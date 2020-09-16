#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#time "on"

open Akka.FSharp
open System

let system = System.create "system" <| Configuration.load ()

type ProcessorMessage = 
                        | ProcessJob of int * int
                        | Reply of int
                        



let worker (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match box message with
        | :? int as msg -> mailbox.Sender () <! Reply(msg * msg)
        | _ -> ()
        return! loop ()
    }
    loop ()


let loopIt x y = 
    let work = spawn system  "worker" worker
    for i in [x .. y] do
        work <! i
        

let processor (mailbox: Actor<_>) = 
    let mutable sum = 0
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ProcessJob(x,y) -> loopIt x y
        | Reply(z) -> sum <- sum + z
        printfn "%i" sum
        return! loop ()
    }
    loop ()

let processorRef = spawn system "processor" processor

processorRef <! ProcessJob(fsi.CommandLineArgs.[1] |> int, fsi.CommandLineArgs.[2] |> int)

System.Console.ReadLine() |> ignore