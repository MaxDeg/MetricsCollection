
open System
open System.Diagnostics

let load (l: string[]) = l |> Array.map (fun n -> new EventLog(n))
let getLastDate (l: EventLog) = if l.Entries.Count = 0 then DateTime.Now else l.Entries.[l.Entries.Count - 1].TimeWritten

let entriesToPoints (start: DateTime) (entries : EventLogEntryCollection) =
    seq { for i in 1..entries.Count -> entries.[entries.Count - i] }
    |> Seq.takeWhile (fun e -> e.TimeWritten > start)

let logs = 
    let rec logs' (els: (EventLog * DateTime)[]) =
        seq { yield els |> Array.map (fun (e, d) -> entriesToPoints d e.Entries)
              yield! els |> Array.map (fun (e, d) -> e, getLastDate e) |> logs' }

    [| "Application"; "System" |]
    |> load
    |> Array.map (fun l -> l, getLastDate l)
    |> logs'

Seq.take 1 logs