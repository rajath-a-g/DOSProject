#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"
#load "Master.fsx"

open Master
open Akka.FSharp
open System
open Akka.Configuration

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            loglevel : ERROR
        }")


if fsi.CommandLineArgs.Length = 3 then
    let numNodes = fsi.CommandLineArgs.[1] |> int
    let numRequests = fsi.CommandLineArgs.[2] |> int
    let pastryProtocol numNodes numRequests = 
        let system = create "system" <| configuration
        let master = spawn system "master" <| Master numNodes numRequests system
        master <! Start
    pastryProtocol numNodes numRequests
    Console.ReadLine () |> ignore
 

