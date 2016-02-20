module Program

open InfluxDb
open MetricsCollector
open FSharp.Collections.ParallelSeq
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
    //influxdb.Create(config.Agent.Database.Name) |> ignore

    let send (points : Point[]) = 
        let pointsWithHost = points |> Array.map (fun p -> p.With(Map [ "host", machine ]))
        if not <| Array.isEmpty points then 
            Array.iter (fun (p: Point) -> Console.WriteLine(p.Serialize(None))) points
            //influxdb.Write(config.Agent.Database.Name, pointsWithHost, config.Agent.Database.Precision |> Option.map fromString) 
            //|> ignore
    
    let collectors : ICollector[] = 
        [| new Ping.Collector(config)
           new PerformanceCounters.Collector(config)
           new EventLogs.Collector(config) |]
    
    let runTests _ = 
        collectors
        |> Array.collect (fun i -> try i.Collect() with | _ -> Array.empty)
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
