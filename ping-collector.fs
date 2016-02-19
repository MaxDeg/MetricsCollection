module MetricsCollector.Ping

open System.Net.NetworkInformation
open FSharp.Collections.ParallelSeq
open InfluxDb


type Collector (config: Config.Root) = 
    let uris = config.Ping

    let ping (uri : string) = 
        let pinger = new Ping()
        pinger.Send(uri)

    let fields (r : PingReply) = 
        Map [ "status", String(string r.Status)
              "value", Int r.RoundtripTime ]

    interface ICollector with 
        member this.Collect () = 
            uris
            |> Seq.map (fun target -> new Point("ping", Map [ "target", target ], ping target |> fields))
