module MetricsCollector.EventLogs

open System
open System.Diagnostics
open InfluxDb
open FSharp.Collections.ParallelSeq

type internal EventLog' =
    { Name: string
      Log: EventLog
      Types: string[]
      LastDate: DateTime }

type Collector (config: Config.Root) = 
    let getLastDate (l: EventLog) = if l.Entries.Count = 0 then DateTime.Now 
                                    else l.Entries.[l.Entries.Count - 1].TimeWritten

    let entriesToPoints (event: EventLog') =
        let entriesCount = event.Log.Entries.Count
        seq { for i in 1..entriesCount -> event.Log.Entries.[entriesCount - i] }
        |> PSeq.takeWhile (fun e -> e.TimeWritten > event.LastDate)
        |> PSeq.filter (fun e -> Array.length event.Types = 0 || Array.contains (string e.EntryType) event.Types)
        |> PSeq.map (fun e ->            
                    let tags = Map [ "name", event.Name
                                     "type", string (e.EntryType)
                                     "source", e.Source
                                     "time_written", string (e.TimeWritten)
                                     "time_generated", string (e.TimeGenerated)
                                     "username", e.UserName ]
                                |> Map.filter (fun _ v -> v <> null)
           
                    let value = Map [ "message", e.Message |> String ]
                    new Point("event_logs", new DateTimeOffset(e.TimeGenerated), Some tags, value))

    let logs =
        let rec logs' (events: EventLog'[]) =
            seq { let next = events |> Array.map (fun e -> { e with LastDate = getLastDate e.Log })
                  yield events
                  yield! next |> logs' }

        let eventLogs = config.EventLogs
                        |> Array.map (fun n -> let event = new EventLog(n.Name)
                                               { Name = n.Name
                                                 Log = event
                                                 Types = n.Types
                                                 LastDate = getLastDate event })
                        |> logs'
        eventLogs.GetEnumerator()
    
    interface ICollector with 
        member this.Collect () =
            logs.MoveNext() |> ignore

            logs.Current
            |> PSeq.collect (fun e -> entriesToPoints e)
            |> PSeq.toArray
