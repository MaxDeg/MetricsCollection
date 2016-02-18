module Collectors.Ping

open System
open System.Linq
open System.Net.NetworkInformation
open FSharp.Collections.ParallelSeq
open InfluxDb

let private config = Config()
config.Load("config.yaml")

let private ping (uri: string) =
        let pinger = new Ping()
        pinger.Send(uri)

let collect () =
    let fields (r: PingReply) =
        Map [ "status", String (string r.Status)
              "value", Int r.RoundtripTime ]
    
    config.ping
        |> PSeq.map (fun cfg -> new Point("ping", Map [ "target", cfg.target ], ping cfg.target |> fields))
        |> PSeq.toArray
