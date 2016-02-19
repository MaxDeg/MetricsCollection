namespace MetricsCollector

open FSharp.Data
open InfluxDb
open FSharp.Collections.ParallelSeq

type Config = JsonProvider<"""[{
    "agent": {
		"host": "mdg-7",
		"interval": "10",
		"database": {
			"host": "localhost",
            "port": 8086,
			"name": "monitoring2",
			"precision": "s",
			"username": "mdg",
			"password": "***"
		}
	},
	"ping": [ "ll-7" ],
	"perfCounters": [
		{
			"category": "Processor",
			"counters": [ "% Idle Time" ],
			"instances": [ "a" ]
		}
	],
	"eventLogs": [
		{
			"name": "Application",
			"types": [ "Error" ]
		}
	]
},
{
    "agent": {
		"host": "mdg-7",
		"interval": "10",
		"database": {
			"host": "localhost",
			"name": "monitoring2"
		}
	},
	"ping": [ "ll-7" ],
	"perfCounters": [
		{
			"category": "Processor"
		}
	],
	"eventLogs": [
		{
			"name": "Application"
		}
	]
}]""", SampleIsList = true>

type ICollector =
    abstract member Collect: unit -> pseq<Point>