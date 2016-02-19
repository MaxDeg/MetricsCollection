module MetricsCollector.PerformanceCounters

open System
open System.Diagnostics
open InfluxDb

type Collector (config: Config.Root) = 
    let countersFor (category : string) (instances : seq<string>) (counters : seq<string>) = 
        let perfCategory = new PerformanceCounterCategory(category)
    
        let instances = 
            if Seq.isEmpty instances then 
                let insts = perfCategory.GetInstanceNames()
                if Array.isEmpty insts then [| String.Empty |]
                else insts
            else Array.ofSeq instances
    
        let counters = 
            if Seq.isEmpty counters then instances |> Array.collect (fun i -> perfCategory.GetCounters(i))
            else 
                counters
                |> Seq.collect (fun c -> instances |> Array.map (fun i -> c, i))
                |> Seq.map (fun (c, i) -> new PerformanceCounter(category, c, i))
                |> Seq.toArray
    
        // First value is always 0 :)
        counters |> Array.iter (fun c -> c.NextValue() |> ignore)
        counters

    let counters = 
        config.PerfCounters
        |> Seq.collect (fun cfg -> countersFor cfg.Category cfg.Instances cfg.Counters)
        |> Seq.toArray
    
    interface ICollector with 
        member this.Collect () = 
           counters
            |> Seq.map (fun c -> 
                   let instanceTag = 
                       if String.IsNullOrEmpty(c.InstanceName) then List.empty
                       else [ ("instance", c.InstanceName) ]
           
                   let tags = 
                       List.concat [ instanceTag
                                     [ ("category", c.CategoryName)
                                       ("counter", c.CounterName) ] ]
           
                   let value = Map [ "value", double (c.NextValue()) |> Float ]
                   new Point("perf_counters", tags |> Map.ofList, value))
