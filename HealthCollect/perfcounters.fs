module PerformanceCounters

open System
open System.Diagnostics
open InfluxDb

type InstanceCountersValue =
    { instance: string
      values: Map<string, float32> }

type PerfCounter(category: string, instances: string[] option, counters: string[] option) =
    let perfCategory = new PerformanceCounterCategory(category)

    let instanceList = match instances with
                        | Some insts -> insts
                        | None -> 
                            let insts = perfCategory.GetInstanceNames()
                            if Array.isEmpty insts then [| String.Empty |] else insts

    let counters = match counters with
                    | Some cnts -> cnts
                                    |> Array.collect (fun c -> instanceList |> Array.map (fun i -> c, i))
                                    |> Array.map (fun (c, i) -> new PerformanceCounter(category, c, i))
                    | None -> instanceList 
                                |> Array.collect (fun i -> perfCategory.GetCounters(i))
    
    
    new(category: string) = 
        PerfCounter(category, None, None)

    new(category: string, counters: string[]) = 
        PerfCounter(category, None, Some counters)

    member this.Fetch () =
        Array.map (fun (c: PerformanceCounter) -> c.CounterName, c.InstanceName, c.NextValue()) counters
        |> Array.groupBy (fun (_, instance, _) -> instance)
        |> Array.map (fun (instance, values) -> 
                        { instance = instance
                          values = values
                                    |> Array.map (fun (c, _, v) -> c, v) 
                                    |> Map.ofArray })
