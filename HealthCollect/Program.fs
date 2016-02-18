open System
open Topshelf
open Time
open System.Timers
open InfluxDb
open Collectors

[<EntryPoint>]
let main argv = 
    let influxdb = new InfluxDbClient()
    influxdb.Create("monitoring") |> ignore

    let machine = Environment.MachineName
    let send (points: seq<Point>) = 
        let pointsWithHost = points |> Seq.map (fun p -> p.With(Map [ "host", machine ]))
        influxdb.Write("monitoring", pointsWithHost, Some Precision.Seconds) |> ignore

    let runTests _ =
        [ Ping.collect(); PerformanceCounters.collect() ]
        |> Seq.collect (fun i -> i)
        |> send
//        let pingPoints = ping [ "ll-7"; "adsi-7"; "mdg-7"; "jgi-7" ]
//                            |> Array.map (fun (u, h) -> toPoint u h.RoundtripTime)
//
//        let memPerfPoints = memPerf.Fetch() |> Array.map (fun p -> p.AsPoint("memory"))
//        let cpuPerfPoints = cpuPerf.Fetch() |> Array.map (fun p -> p.AsPoint("cpu"))
//        let diskPerfPoints = diskPerf.Fetch() |> Array.map (fun p -> p.AsPoint("disk"))
//
//        Array.concat [ pingPoints; memPerfPoints; cpuPerfPoints; diskPerfPoints ]
//        |> send


    let start ctx = 
        let timer = new Timer(10.0 * 1000.0)
        timer.AutoReset <- true
        timer.Elapsed.Add(runTests)

        timer.Start()
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

        true

    let stop ctx = true

    Service.Default
        |> with_start start
        |> with_recovery (ServiceRecovery.Default |> restart (min 1))
        |> with_stop stop
        |> run
