module InfluxDb

open System
open HttpClient
open Newtonsoft.Json

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
    with 
        override this.ToString() = 
            match this with
            | Int i -> string i
            | Float f -> string f
            | String s -> "\"" + s + "\""
            | Bool b -> string b

type Point(measurement: string, timestamp: DateTimeOffset, tags: Map<string, string> option, fields: Map<string, KeyValue>) =
    let escape value = "\"" + value + "\""
    let timestamp = timestamp.ToUnixTimeSeconds()

    new (measurement: string, fields: Map<string, KeyValue>) =
        Point(measurement, DateTimeOffset.UtcNow, None, fields)
        
    new (measurement: string, timestamp: DateTimeOffset, fields: Map<string, KeyValue>) =
        Point(measurement, timestamp, None, fields)
        
    new (measurement: string, tags: Map<string, string>, fields: Map<string, KeyValue>) =
        Point(measurement, DateTimeOffset.UtcNow, Some tags, fields)

    member this.Serialize =
        let tags = match tags with
                    | Some t -> t 
                                // |> Map.filter (fun _ v -> not(String.IsNullOrWhiteSpace(v)))
                                |> Map.toList 
                                |> List.map (fun (key, value) -> escape key + "=" + escape value)
                    | None -> List.empty

        let fields = fields 
                        |> Map.toList 
                        |> List.map (fun (key, value) -> escape key + "=" + string value)

        String.Join(",", measurement :: tags) + " " + String.Join(" ", fields) + " " + string timestamp

type Series =
    { Name: string
      Tags: Map<string, string>
      Columns: string[]
      Values: obj[][] }

type internal QueryResult = 
    { Results: Series[]
      Error: string }

type QueryResponse =
    | Error of string
    | Result of Series[]

type InfluxDbClient(host: string, port: uint16) =
    let url path = String.Format("http://{0}:{1}/", host, port) + path
    
    let precisionToString = function
        | Some NanoSeconds -> "n"
        | Some MicroSeconds -> "u"
        | Some MilliSeconds -> "ms"
        | Some Seconds -> "s"
        | Some Minutes -> "m"
        | Some Hours -> "h"
        | None -> "n"

    new () = InfluxDbClient("localhost")
    new (host: string) = InfluxDbClient(host, 8086us)
    
    member this.Write(database: string, point: Point, precision: Precision option) = 
        this.Write(database, [| point |], precision)

    member this.Write(database: string, points: Point[], precision: Precision option) =
        let body = Array.map (fun (p: Point) -> p.Serialize) points
        let request = createRequest Post (url "write")
                        |> withQueryStringItem { name = "db"; value = database }
                        |> withQueryStringItem { name = "precision"; value = precisionToString precision }
                        |> withBody (String.Join("\n", body))
        if getResponseCode request = 204 then
            true
        else
            Console.WriteLine(getResponseBody request)
            false
            
    member this.Query(database: string, query: string) = 
        let result = createRequest Post (url "read")
                    |> withQueryStringItem { name = "db"; value = database }
                    |> withBody ("q=" + query)
                    |> getResponseBody

        let response = JsonConvert.DeserializeObject<QueryResult>(result)
        if String.IsNullOrEmpty(response.Error) then 
            Error response.Error
        else 
            Result response.Results
