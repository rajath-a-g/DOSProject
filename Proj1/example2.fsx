#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#time "on"

open Akka.FSharp
open System

let system = System.create "system" <| Configuration.load ()

type ProcessorMessage = 
                        | ProcessJob of int * int
type MasterMessage = 
                    | GotInput of int * int
                    | Ans of int
                    | Count
                        

let perfectSquare n =
    //printfn "%i" n
    let h = n &&& (bigint 0xF)
    if (h > 9I) then false
    else
        if ( h <> 2I && h <> 3I && h <> 5I && h <> 6I && h <> 7I && h <> 8I ) then
            let t = ((n |> double |> sqrt) + 0.5) |> floor|> bigint
            t*t = n
        else false
let sumOfSqrs start = bigint.Divide(start * (start + 1I) * (2I * start + 1I), 6I)     
//  sum = sumOfSquares(first + k - 1) - sumOfSquares(first - 1)
let sumOfSeq first k = 
    let one = first + k - 1 |> bigint
    let two = first - 1 |> bigint
    let sum1 = sumOfSqrs one
    let sum2 = sumOfSqrs two
    bigint.Subtract(sum1, sum2)

let processor (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ProcessJob(x,y) -> let sum = sumOfSeq x y
                             //printfn "Sum : %i%i%A" x y sum
                             let isPerfect = perfectSquare sum
                             select ("akka://system/user/master") mailbox <! Count
                             if isPerfect then 
                                select ("akka://system/user/master") mailbox <! Ans(x)
                                return ()
                             return! loop ()
    }
    loop ()


let splitRange n k =
    let p = n+1
    let convP = p |> int
    let actorArr = Array.create convP (spawn system "range" processor)
    {1..n} |> Seq.iter (fun a ->
        actorArr.[a] <- spawn system (string a) processor
        ()
    )
    {1..n} |> Seq.iter (fun a ->
        actorArr.[a] <! ProcessJob(a, k)
        ()
    )


let mutable flag = 0
type Num = int
let master (mailbox: Actor<_>) =
    let mutable i = 0
    let rec loop () = actor {
        let! msg = mailbox.Receive ()
        match msg with
        | GotInput(N,K) -> i <- N |> int
                           splitRange N K
                           //printfn "%A" i
                           return! loop ()
        | Ans(first) -> //printfn "In master%i" first
                        printfn "result: %i" first
                        return! loop () 
        | Count -> i <- i - 1
                   //printfn "%A" i
                   if i = 0 then
                        //printfn "Setting flag 1"
                        flag <- 1
                   return! loop ()
                   
                                                        
    }
    loop ()

let masterRef = spawn system "master" master
//printfn "%A" masterRef

masterRef <! GotInput(fsi.CommandLineArgs.[1] |> int , fsi.CommandLineArgs.[2] |> int)

// //Async.RunSynchronously start
// System.Console.ReadLine()
while flag<>1 do
    Threading.Thread.Sleep 100
system.Terminate()
