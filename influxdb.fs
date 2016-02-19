module InfluxDb

open HttpClient
open Newtonsoft.Json
open System

type Precision = 
    | NanoSeconds
    | MicroSeconds
    | MilliSeconds
    | Seconds
    | Minutes
    | Hours

type KeyValue = 
    | Int of int64
    | Float of double
    | String of string
    | Bool of bool
    override this.ToString() = 
        match this with
        | Int i -> string i
        | Float f -> string f
        | String s -> "\"" + s.Replace("\"", "\\\"") + "\""
        | Bool b -> string b

type Point(measurement : string, timestamp : DateTimeOffset, tags : Map<string, string> option, fields : Map<string, KeyValue>) = 
    let escape (value : string) = value.Replace(" ", "\ ").Replace(",", "\,")
    
    let timestampPrecision = 
        function 
        | NanoSeconds -> timestamp.ToUnixTimeMilliseconds() * 100L
        | MicroSeconds -> timestamp.ToUnixTimeMilliseconds() * 10L
        | MilliSeconds -> timestamp.ToUnixTimeMilliseconds()
        | Seconds -> timestamp.ToUnixTimeSeconds()
        | Minutes -> timestamp.ToUnixTimeSeconds() / 60L
        | Hours -> timestamp.ToUnixTimeSeconds() / 120L
    
    new(measurement : string, fields : Map<string, KeyValue>) = Point(measurement, DateTimeOffset.UtcNow, None, fields)
    new(measurement : string, timestamp : DateTimeOffset, fields : Map<string, KeyValue>) = 
        Point(measurement, timestamp, None, fields)
    new(measurement : string, tags : Map<string, string>, fields : Map<string, KeyValue>) = 
        Point(measurement, DateTimeOffset.UtcNow, Some tags, fields)
    
    member this.With(additionalTags : Map<string, string>) = 
        let tagsSeq = 
            match tags with
            | Some t -> Map.toSeq t
            | None -> Seq.empty
        new Point(measurement, timestamp, 
                  Some(Seq.concat [ tagsSeq
                                    Map.toSeq additionalTags ]
                       |> Map.ofSeq), fields)
    
    member this.Serialize(precision : Precision option) = 
        let tags = 
            match tags with
            | Some t -> 
                t
                |> Map.toList
                |> List.map (fun (key, value) -> escape key + "=" + escape value)
            | None -> List.empty
        
        let fields = 
            fields
            |> Map.toList
            |> List.map (fun (key, value) -> escape key + "=" + string value)
        
        String.Join(",", escape measurement :: tags) + " " + String.Join(",", fields) + " " 
        + string (defaultArg precision Precision.NanoSeconds |> timestampPrecision)

type Serie = 
    { Name : string
      Tags : Map<string, string>
      Columns : string []
      Values : obj [] [] }

type Series = 
    { Series : Serie [] }

type QueryResult = 
    { Results : Series []
      Error : string }

type QueryResponse = 
    | Error of string
    | Result of Serie [] []

let toString (precision : Precision) = 
    match precision with
    | NanoSeconds -> "n"
    | MicroSeconds -> "u"
    | MilliSeconds -> "ms"
    | Seconds -> "s"
    | Minutes -> "m"
    | Hours -> "h"

let fromString (precision : string) = 
    match precision with
    | "n" -> NanoSeconds
    | "u" -> MicroSeconds
    | "ms" -> MilliSeconds
    | "s" -> Seconds
    | "m" -> Minutes
    | "h" -> Hours
    | _ -> NanoSeconds

type InfluxDbClient(host : string, port : uint16 option) = 
    let url path = String.Format("http://{0}:{1}/", host, defaultArg port 8086us) + path
    
    let precisionToString = 
        function 
        | Some p -> toString p
        | None -> toString NanoSeconds
    
    new() = InfluxDbClient("localhost", None)
    member this.Write(database : string, point : Point, precision : Precision option) = 
        this.Write(database, [ point ], precision)
    
    member this.Write(database : string, points : seq<Point>, precision : Precision option) = 
        let body = Seq.map (fun (p : Point) -> p.Serialize precision) points
        
        let request = 
            createRequest Post (url "write")
            |> withQueryStringItem { name = "db"
                                     value = database }
            |> withQueryStringItem { name = "precision"
                                     value = precisionToString precision }
            |> withBody (String.Join("\n", body))
        if getResponseCode request = 204 then true
        else 
            Console.WriteLine(getResponseBody request)
            false
    
    member this.Query(database : string, query : string, [<ParamArray>] parameters : obj []) = 
        let result = 
            createRequest Get (url "query")
            |> withQueryStringItem { name = "db"
                                     value = database }
            |> withQueryStringItem { name = "q"
                                     value = String.Format(query, parameters) }
            |> getResponseBody
        
        let response = JsonConvert.DeserializeObject<QueryResult>(result)
        if not <| String.IsNullOrEmpty(response.Error) then Error response.Error
        else Result(response.Results |> Array.map (fun s -> s.Series))
    
    member this.Create(database : string) = 
        let result = 
            createRequest Get (url "query")
            |> withQueryStringItem { name = "q"
                                     value = "CREATE DATABASE " + database }
            |> getResponseBody
        
        let response = JsonConvert.DeserializeObject<QueryResult>(result)
        String.IsNullOrEmpty(response.Error)
