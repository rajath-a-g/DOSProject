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
    let z = y-x+1

    let actorArray = Array.create z (spawn system "myActor" worker)
    {x..y} |> Seq.iter (fun a ->
        actorArray.[a] <- spawn system (string a) worker
        ()
    )
    {x..y} |> Seq.iter(fun a ->
        actorArray.[a] <! a
        ()
    ) 

let perfectSquare n =
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
        | ProcessJob(x,y) -> first <- x
                             i <- y-x+1
                             loopIt x y
                             return! loop ()
        | Reply(z) -> sum <- sum + z
                      i <- i - 1
                      if i = 0 then 
                        printfn "%i" sum
                        let isPerfect = perfectSquare sum
                        if isPerfect then 
                            mailbox.Sender () <! Reply(first)
                        return ()
                      else 
                      return! loop ()
    }
    loop ()
type MasterMessage = 
                    | GotInput of int * int
                    | Reply of int

let splitRange n k =
    let actorArr = Array.create n (spawn system "range" processor)
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
    let mutable checkLimit = 0
    let rec loop () = actor {
        let mutable low =  Num.MaxValue
        let! msg = mailbox.Receive ()
        match msg with
        | GotInput(N,K) -> checkLimit <- N
                           splitRange N K
                           return! loop ()
        | Reply(first) -> checkLimit <- checkLimit - 1
                          if first < low then
                            low <- first
                          else
                            return! loop ()
                          if checkLimit = 0 then
                            printfn "result: %i" low
                            return ()
                          else
                            return! loop ()
                                               
    }
    loop ()

let masterRef = spawn system "master" master

masterRef <! GotInput(fsi.CommandLineArgs.[1] |> int, fsi.CommandLineArgs.[2] |> int)

System.Console.ReadLine() |> ignore
