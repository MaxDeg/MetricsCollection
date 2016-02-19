module MetricsCollector.EventLogs

open System
open System.Diagnostics
open InfluxDb
open FSharp.Collections.ParallelSeq

type Collector (config: Config.Root) = 
    let mutable lastCheck = DateTime.Now
    let logs = config.EventLogs |> Array.map (fun n -> new EventLog(n.Name), n.Types)
    
    let entriesToPoints (types: string[]) (entries : EventLogEntryCollection) = 
        let start = lastCheck
        lastCheck <- DateTime.Now

        seq { for i in 1..entries.Count -> entries.[entries.Count - i] }
        |> PSeq.takeWhile (fun e -> e.TimeWritten > start)
        |> PSeq.filter (fun e -> Array.contains (string e.EntryType) types)
        |> PSeq.map (fun e ->            
                       let tags = Map [ "category", e.Category
                                        "type", string (e.EntryType)
                                        "source", e.Source
                                        "time_written", string (e.TimeWritten)
                                        "username", e.UserName ]
           
                       let value = Map [ "message", e.Message |> String ]
                       new Point("event_logs", new DateTimeOffset(e.TimeGenerated), Some tags, value))

    interface ICollector with 
        member this.Collect () =
            logs 
            |> PSeq.collect (fun (l, types) -> l.Entries |> entriesToPoints types)

