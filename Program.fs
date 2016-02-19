//    let config = Config()
//    config.Load("config.yaml")
//        let pingPoints = ping [ "ll-7"; "adsi-7"; "mdg-7"; "jgi-7" ]
//                            |> Array.map (fun (u, h) -> toPoint u h.RoundtripTime)
//
//        let memPerfPoints = memPerf.Fetch() |> Array.map (fun p -> p.AsPoint("memory"))
//        let cpuPerfPoints = cpuPerf.Fetch() |> Array.map (fun p -> p.AsPoint("cpu"))
//        let diskPerfPoints = diskPerf.Fetch() |> Array.map (fun p -> p.AsPoint("disk"))
//
//        Array.concat [ pingPoints; memPerfPoints; cpuPerfPoints; diskPerfPoints ]
//        |> send
//        let sw = Stopwatch.StartNew()
//
//        ping [ "ll-7"; "adsi-7"; "mdg-7"; "jgi-7" ]
//        |> Array.iter (fun (u, h) -> u + " " + string h.RoundtripTime |> Console.WriteLine)
//        Console.WriteLine("Elapsed: " + string sw.ElapsedMilliseconds)
//
//        let evtLogs = new EventLogs([| "Application" |])
//        let logs = evtLogs.Fetch(Some (new DateTimeOffset(new DateTime(2016, 02, 18))))
//        Console.WriteLine("Elapsed: " + string sw.ElapsedMilliseconds)
//        
//        logs |> Array.iter (fun (n, l) -> l.Message |> Console.WriteLine)
//
//
//        match influxdb.Query("monitoring", "SELECT * FROM ping WHERE host = '{0}' ORDER BY time DESC LIMIT 1", machine) with
//           | Error e -> Console.WriteLine(e)
//           | Result r ->
//                let serie = r |> Array.head |> Seq.head
//                let idx = serie.Columns |> Array.findIndex (fun c -> c = "time")
//
//                serie.Values 
//                |> Array.head 
//                |> Array.item idx 
//                |> Console.WriteLine
module Program

open FSharp.Data.JsonExtensions
open InfluxDb
open MetricsCollector
open System
open System.Timers
open Topshelf

[<EntryPoint>]
let main _ = 
    let config = Config.Load("config.json")
    
    let machine = 
        if String.IsNullOrEmpty(config.Agent.Host) then Environment.MachineName
        else config.Agent.Host
    
    let influxdb = 
        new InfluxDbClient(config.Agent.Database.Host, config.Agent.Database.Port |> Option.map (fun p -> uint16 p))
    influxdb.Create(config.Agent.Database.Name) |> ignore

    let send (points : seq<Point>) = 
        let pointsWithHost = points |> Seq.map (fun p -> p.With(Map [ "host", machine ]))
        if not <| Seq.isEmpty points then 
            influxdb.Write
                (config.Agent.Database.Name, pointsWithHost, config.Agent.Database.Precision |> Option.map fromString) 
            |> ignore
    
    let collectors : ICollector list = 
        [ new Ping.Collector(config)
          new PerformanceCounters.Collector(config)
          new EventLogs.Collector(config) ]
    
    let runTests _ = 
        collectors
        |> Seq.collect (fun i -> i.Collect())
        |> send
    
    let start _ = 
        let timer = new Timer(10.0 * 1000.0)
        timer.AutoReset <- true
        timer.Elapsed.Add(runTests)
        timer.Start()
        true
    
    let stop _ = true
    Service.Default
    |> with_start start
    |> with_recovery (ServiceRecovery.Default |> restart (TimeSpan.FromMinutes(1.0)))
    |> with_stop stop
    |> run
