module EventLogs

open System
open System.Diagnostics
open FSharp.Collections.ParallelSeq

type EventLogs(logNames: string[], eventTypes: EventLogEntryType list) =
    let logs = logNames |> Array.map (fun n -> new EventLog(n))

    new (logNames: string[]) = EventLogs(logNames, [ EventLogEntryType.Warning; EventLogEntryType.Error ])

    member this.Fetch (start: DateTimeOffset option) =
        let toSeq (entries: EventLogEntryCollection) =
            seq { for i in 1 .. entries.Count -> entries.[entries.Count - i] }

        let filter (entries: EventLogEntryCollection) =
            entries
            |> toSeq
            |> PSeq.takeWhile (fun e ->  match start with 
                                             | Some d -> e.TimeWritten > d.LocalDateTime 
                                             | None -> true)
            |> PSeq.filter (fun e -> List.contains e.EntryType eventTypes)
            |> PSeq.toArray
        
        logs
        |> Array.collect (fun l -> l.Entries |> filter |> Array.map (fun e -> l.LogDisplayName, e))
