#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#time "on"

open Akka.FSharp
open System

let numberOfActors = 10

let system = System.create "system" <| Configuration.load ()

type ProcessorMessage = 
                        | GotInput of int * int * int

type Mastermessage = 
                        | StartJob of int * int
                        | Ans of int
                        | Count

let perfectSquare n =
    let h = n &&& (bigint 0xF)
    if (h > 9I) then false
    else
        if ( h <> 2I && h <> 3I && h <> 5I && h <> 6I && h <> 7I && h <> 8I ) then
            let t = ((n |> double |> sqrt) + 0.5) |> floor|> bigint
            t*t = n
        else false
let sumOfSqrs start = bigint.Divide(start * (start + 1I) * (2I * start + 1I), 6I)     
let sumOfSeq first k = 
    let one = first + k - 1 |> bigint
    let two = first - 1 |> bigint
    let sum1 = sumOfSqrs one
    let sum2 = sumOfSqrs two
    bigint.Subtract(sum1, sum2)

let checkPerfectSquare i k = 
        let sum = sumOfSeq i k
        perfectSquare sum

let processor (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | GotInput(x, y, k) -> {x..(x + y - 1)} |> Seq.iter (fun i -> 
                                    if checkPerfectSquare i k then
                                        select ("akka://system/user/master") mailbox <! Ans(i)
                                    select ("akka://system/user/master") mailbox <! Count
                               )
                               return! loop ()
    }
    loop ()

let assignWork n k noOfActors =
    let unit = n / noOfActors
    let rem = n % noOfActors
    let mutable runActor = 0
    if unit = 0 then
        runActor <- rem
    else
        runActor <- noOfActors

    {1..runActor} |> Seq.iter(fun a ->
        if a <= rem then
            let start = (a - 1) * (unit + 1) + 1
            let name = Guid.NewGuid()
            let masterRef = spawn system (string name) processor
            masterRef <! GotInput(start, unit+1, k)
        else
            let start = rem * (unit + 1) + (a - 1 - rem) * unit + 1
            let name = Guid.NewGuid()
            let masterRef = spawn system (string name) processor
            masterRef <! GotInput(start, unit, k)
    )
    runActor

let mutable flag = 0

let master (mailbox: Actor<_>)=
    let mutable numActors = 0
    let rec loop () = actor {
        let! msg = mailbox.Receive ()
        match msg with
        | StartJob(n, k) -> numActors <- assignWork n k numberOfActors
                            numActors <- n 
                            return! loop ()
        | Ans(first) -> printfn "%i" first
                        return! loop () 
        | Count -> //printfn "numofact: %i" numActors
                   numActors <- numActors - 1
                   if numActors = 0 then
                        flag <- 1
                   return! loop ()
    }
    loop ()

let masterRef = spawn system "master" master

masterRef <! StartJob(fsi.CommandLineArgs.[1] |> int , fsi.CommandLineArgs.[2] |> int)

while flag<>1 do
    Threading.Thread.Sleep 10

system.Terminate()
