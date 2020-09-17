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
    // let work = spawn system  "worker" worker
    // for i in [x .. y] do
    //     work <! i
    let actorArray = Array.create 100001 (spawn system "myActor" worker)
    {x..y} |> Seq.iter (fun a ->
        actorArray.[a] <- spawn system (string a) worker
        ()
    )
    {x..y} |> Seq.iter(fun a ->
        actorArray.[a] <! a
        ()
    ) 
        
let processor (mailbox: Actor<_>) = 
    let mutable sum = 0
    let mutable i = 100000
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ProcessJob(x,y) -> loopIt x y
                             return! loop ()
        | Reply(z) -> sum <- sum + z
                      i <- i - 1
                      if i = 0 then 
                        printfn "%i" sum
                        return ()
                      else 
                      return! loop ()
    }
    loop ()

let perfectSquare n =
    let h = n &&& 0xF
    if (h > 9) then false
    else
        if ( h <> 2 && h <> 3 && h <> 5 && h <> 6 && h <> 7 && h <> 8 ) then
            let t = ((n |> double |> sqrt) + 0.5) |> floor|> int
            t*t = n
        else false
let sqrt = perfectSquare 25
printfn "%A" sqrt

let processorRef = spawn system "processor" processor

processorRef <! ProcessJob(fsi.CommandLineArgs.[1] |> int, fsi.CommandLineArgs.[2] |> int)

System.Console.ReadLine() |> ignore