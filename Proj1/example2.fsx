#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#time "on"

open Akka.FSharp
open System
open System.Runtime.InteropServices

let system = System.create "system" <| Configuration.load ()

type ProcessorMessage = 
                        | ProcessJob of int * int
                        | Reply of int
type MasterMessage = 
                    | GotInput of int * int
                    | Ans of int
                        
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
    let z = y-x+1
    let actorArray = Array.create z (spawn system (string Guid.NewGuid) worker)
    {0..z-1} |> Seq.iter (fun a ->
        //printfn "%i%i%i%s" x y a name
        actorArray.[a] <- spawn system (string Guid.NewGuid) worker
        ()
    )
    {0..z-1} |> Seq.iter(fun a ->
        //printfn "HERE%i%i%i" x y a
        let value = a + x
        actorArray.[a] <! value
        ()
    ) 

let perfectSquare n =
    //printfn "%i" n
    let h = n &&& 0xF
    if (h > 9) then false
    else
        if ( h <> 2 && h <> 3 && h <> 5 && h <> 6 && h <> 7 && h <> 8 ) then
            let t = ((n |> double |> sqrt) + 0.5) |> floor|> int
            t*t = n
        else false
       
let processor (mailbox: Actor<_>) = 
    let mutable sum = 0
    let mutable first = 0
    let mutable i = 0
   
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ProcessJob(x,y) -> //printfn "%i%i" x y
                             first <- x
                             i <- y-x+1
                             loopIt x y
                             return! loop ()
        | Reply(z) -> sum <- sum + z
                      i <- i - 1
                      if i = 0 then 
                        //printfn "%i" sum
                        let isPerfect = perfectSquare sum
                        if isPerfect then 
                            select ("akka://system/user/master") mailbox <! Ans(first)
                            return ()
                      return! loop ()
    }
    loop ()


let splitRange n k =
    let p = n+1
    let actorArr = Array.create p (spawn system "range" processor)
    {1..n} |> Seq.iter (fun a ->
        actorArr.[a] <- spawn system (string a) processor
        ()
    )
    {1..n} |> Seq.iter (fun a ->
        let ed = a+k-1
        actorArr.[a] <! ProcessJob(a, ed)
        ()
    )

type Num = int
let master (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! msg = mailbox.Receive ()
        match msg with
        | GotInput(N,K) -> splitRange N K
                           return! loop ()
        | Ans(first) -> //printfn "In master%i" first
                        printfn "result: %i" first
                        return! loop ()                                         
    }
    loop ()

let masterRef = spawn system "master" master
//printfn "%A" masterRef
let start = async {
    masterRef <! GotInput(fsi.CommandLineArgs.[1] |> int, fsi.CommandLineArgs.[2] |> int)
}
do start |> Async.StartImmediate

//System.Console.ReadLine() |> ignore
//System.Console.CancelKeyPress