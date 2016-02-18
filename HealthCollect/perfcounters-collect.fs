module Collectors.PerformanceCounters

open System
open System.Linq
open System.Diagnostics
open FSharp.Collections.ParallelSeq
open InfluxDb

let private config = Config()
config.Load("config.yaml")

let countersFor (category: string) (instances: string[]) (counters: string[]) =
    let perfCategory = new PerformanceCounterCategory(category)

    let instances = if Array.isEmpty instances then
                            let insts = perfCategory.GetInstanceNames()
                            if Array.isEmpty insts then [| String.Empty |] else insts
                       else
                            instances

    let counters= if Array.isEmpty counters then
                        instances |> Array.collect (fun i -> perfCategory.GetCounters(i))
                    else
                        counters
                        |> Array.collect (fun c -> instances |> Array.map (fun i -> c, i))
                        |> Array.map (fun (c, i) -> new PerformanceCounter(category, c, i))
    
    // First value is always 0 :)
    counters |> Array.iter (fun c -> c.NextValue() |> ignore)

    counters

let private counters =
    config.perfcounters
    |> Seq.collect (fun cfg -> countersFor cfg.category (Array.ofSeq cfg.instances) (Array.ofSeq cfg.counters))


let collect () =
    counters
    |> PSeq.map (fun c -> 
                    let instanceTag = if String.IsNullOrEmpty(c.InstanceName) then List.empty else [ ("instance", c.InstanceName) ]
                    let tags = List.concat [ instanceTag; [ ("category", c.CategoryName); ("counter", c.CounterName) ] ]
                    let value = Map [ "value", double (c.NextValue()) |> Float ]

                    new Point("perf_counters", tags |> Map.ofList, value))
    |> PSeq.toArray