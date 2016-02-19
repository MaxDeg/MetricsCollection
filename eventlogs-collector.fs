module MetricsCollector.EventLogs

open System
open System.Diagnostics
open InfluxDb

type Collector (config: Config.Root) = 
    let mutable lastCheck = DateTime.Now
    let logs = config.EventLogs |> Array.map (fun n -> new EventLog(n.Name), n.Types)
    
    let entriesToPoints (types: string[]) (entries : EventLogEntryCollection) = 
        let start = lastCheck
        lastCheck <- DateTime.Now

        seq { for i in 1..entries.Count -> entries.[entries.Count - i] }
        |> Seq.takeWhile (fun e -> e.TimeWritten > start)
        |> Seq.filter (fun e -> Array.contains (string e.EntryType) types)
        |> Seq.map (fun e ->            
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
            |> Seq.collect (fun (l, types) -> l.Entries |> entriesToPoints types)

