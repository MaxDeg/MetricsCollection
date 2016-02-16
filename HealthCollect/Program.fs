// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

open System
open InfluxDb
open PerformanceCounters
open System.Timers

[<EntryPoint>]
let main argv = 
    printfn "%A" argv
    
    let machine = Environment.MachineName
    let influxdb = new InfluxDbClient()
    let counter = new PerfCounter("Memory")

    let show (point: Point) = Console.WriteLine(point.Serialize)

    let toPoint (instanceCntValues: InstanceCountersValue) = 
            new Point("memory", Map [ "host", machine; "instance", instanceCntValues.instance ], 
                        instanceCntValues.values |> Map.map (fun name value -> double value |> Float))

    let timer = new Timer(1000.0)
    timer.Elapsed.Add(fun _ -> counter.Fetch() |> Array.map toPoint |> Array.iter show)
    timer.AutoReset <- true
    timer.Start()

    Console.ReadKey() |> ignore
    0 // return an integer exit code
