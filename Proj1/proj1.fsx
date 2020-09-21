#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#time "on"

open Akka.FSharp
open System
// Work Unit - change as required to improve performance
let numberOfActors = 10

let system = System.create "system" <| Configuration.load ()

// Worker message union which processes the sum of squares and determines if it is perfect square
type WorkerMessage = 
                        | GotInput of int * int * int

// Master message union which supervises the worker actors
type Mastermessage = 
                        | StartJob of int * int
                        | Ans of int
                        | Count

/// <summary>
/// Logic to check if it is a perfect square, squaring back to check if it is equal to the given number,
/// takes care of the rounding off behaviour of the sqrt method
/// </summary>
/// <param name="num">Given number</param>
/// <returns>if it is a perfect square or not</returns>
let perfectSquare num =
    let h = num &&& (bigint 0xF)
    if (h > 9I) then false
    else
        if ( h <> 2I && h <> 3I && h <> 5I && h <> 6I && h <> 7I && h <> 8I ) then
            let t = ((num |> double |> sqrt) + 0.5) |> floor|> bigint
            t*t = num
        else false

/// <summary>
/// This function returns the sum of squares.
/// </summary>
/// <param name="start">The last number of the sequence</param>
/// <returns>Sum of sqaures upto start</returns>
let sumOfSqrs start = bigint.Divide(start * (start + 1I) * (2I * start + 1I), 6I)   

/// <summary>
/// Return the sum of squares 
/// </summary>
/// <param name="first">Starting number in the range of sequence</param>
/// <param name="k">Range of numbers to be counted upto from first number</param>
/// <returns>Sum of sqaures from first</returns>
let sumOfSeq first k = 
    let one = first + k - 1 |> bigint
    let two = first - 1 |> bigint
    let sum1 = sumOfSqrs one
    let sum2 = sumOfSqrs two
    bigint.Subtract(sum1, sum2)

/// <summary>
/// Checks if the sum of sqaures is a perfect square
/// </summary>
/// <param name="i">Starting number in the range of sequence</param>
/// <param name="k">Range of numbers to be counted upto from first number</param>
/// <returns>If the sum is a perfect square</returns>
let checkPerfectSquare i k = 
        let sum = sumOfSeq i k
        perfectSquare sum

// Worker Actor -takes start of the range and checks if the sum of sqaures of the range is perfect square
// Return the result to master
let worker (mailbox: Actor<_>) = 
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

/// <summary>
/// Splits the given range according to the number of work units
/// </summary>
/// <param name="n">Ending number in the range of sequence</param>
/// <param name="k">Range of numbers to be counted upto from first number</param>
/// <param name="noOfActors">Number of work units</param>
/// <returns>Number of worker actors running</returns>
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
            let masterRef = spawn system (string name) worker
            masterRef <! GotInput(start, unit+1, k)
        else
            let start = rem * (unit + 1) + (a - 1 - rem) * unit + 1
            let name = Guid.NewGuid()
            let masterRef = spawn system (string name) worker
            masterRef <! GotInput(start, unit, k)
    )
    runActor

// flag required to check if all actors have completed.
let mutable flag = 0

// Master Actor - Supervises the worker actors and delegates work to them
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
        | Count -> numActors <- numActors - 1
                   if numActors = 0 then
                        flag <- 1
                   return! loop ()
    }
    loop ()

// Master Actor reference
let masterRef = spawn system "master" master
// Starting point of the program
masterRef <! StartJob(fsi.CommandLineArgs.[1] |> int , fsi.CommandLineArgs.[2] |> int)

while flag<>1 do
    Threading.Thread.Sleep 10

system.Terminate()
