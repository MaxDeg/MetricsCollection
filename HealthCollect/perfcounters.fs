module PerformanceCounters

open System
open System.Diagnostics

type InstanceCountersValue =
    { Name: string
      Instance: string
      Values: Map<string, float32> }
//    with
//        member this.AsPoint measurement =
//            let tags = ("category", this.Name) :: if String.IsNullOrEmpty(this.Instance) then List.empty else [ ("instance", this.Instance) ]
//
//            new Point(measurement, Map.ofList tags, this.Values |> Map.map (fun name value -> double value |> Float))

type PerfCounter(category: string, instances: string[], counters: string[]) =
    let perfCategory = new PerformanceCounterCategory(category)

    let instances = if Array.isEmpty instances then
                            let insts = perfCategory.GetInstanceNames()
                            if Array.isEmpty insts then [| String.Empty |] else insts
                       else
                            instances

    let counters = if Array.isEmpty counters then
                        instances |> Array.collect (fun i -> perfCategory.GetCounters(i))
                   else
                        counters
                        |> Array.collect (fun c -> instances |> Array.map (fun i -> c, i))
                        |> Array.map (fun (c, i) -> new PerformanceCounter(category, c, i))
    
    do
        // First value is always 0 :)
        counters |> Array.iter (fun c -> c.NextValue() |> ignore)
    
    new(category: string) = 
        PerfCounter(category, Array.empty, Array.empty)

    new(category: string, counters: string[]) = 
        PerfCounter(category, Array.empty, counters)

    member this.Fetch () =
        Array.map (fun (c: PerformanceCounter) -> c.CounterName, c.InstanceName, c.NextValue()) counters
        |> Array.groupBy (fun (_, instance, _) -> instance)
        |> Array.map (fun (instance, values) -> 
                        { Name = category
                          Instance = instance
                          Values = values
                                    |> Array.map (fun (c, _, v) -> c, v) 
                                    |> Map.ofArray })
