#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#time "on"

open Akka.FSharp
open System

let system = System.create "system" <| Configuration.load ()

type ProcessorMessage = 
                        | ProcessJob of bigint * bigint
                        | Reply of bigint
type MasterMessage = 
                    | GotInput of bigint * int
                    | Ans of bigint
                    | Count
                        
let worker (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match box message with
        | :? bigint as msg -> mailbox.Sender () <! Reply(msg * msg)
        | _ -> ()
        return! loop ()
    }
    loop ()

let loopIt x y =
    let sub = bigint.Subtract(y, x)
    let zVal = bigint.Add(sub, 1I)
    //let z = y-x+1I
    let h = zVal |> int
    let name = Guid.NewGuid()
    let actorArray = Array.create h (spawn system (string name) worker)
    {0I..zVal-1I} |> Seq.iter (fun n ->
        //printfn "%i%i%i%s" x y a name
        let a = n |> int
        let name1 = Guid.NewGuid()
        actorArray.[a] <- spawn system (string name1) worker
        ()
    )
    {0I..zVal-1I} |> Seq.iter(fun n ->
        //printfn "HERE%i%i%i" x y a
        let a = n |> int
        let value = n + x
        actorArray.[a] <! value
        ()
    ) 

let perfectSquare n =
    //printfn "%i" n
    let h = n &&& (bigint 0xF)
    if (h > 9I) then false
    else
        if ( h <> 2I && h <> 3I && h <> 5I && h <> 6I && h <> 7I && h <> 8I ) then
            let t = ((n |> double |> sqrt) + 0.5) |> floor|> bigint
            t*t = n
        else false
       
let processor (mailbox: Actor<_>) = 
    let mutable sum = 0I
    let mutable first = 0I
    let mutable i = 0I
   
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ProcessJob(x,y) -> //printfn "%i%i" x y
                             first <- x
                             i <- y-x+1I
                             loopIt x y
                             return! loop ()
        | Reply(z) -> sum <- sum + z
                      //printfn "%A" sum
                      i <- i - 1I
                      if i = 0I then 
                        //printfn "%A/n" first
                        select ("akka://system/user/master") mailbox <! Count
                        let isPerfect = perfectSquare sum
                        if isPerfect then 
                            select ("akka://system/user/master") mailbox <! Ans(first)
                            return ()
                      return! loop ()
    }
    loop ()


let splitRange n k =
    let p = n+1I
    let convP = p |> int
    //let convK = k |> bigint
    let actorArr = Array.create convP (spawn system "range" processor)
    {1I..n} |> Seq.iter (fun n ->
        let a = n |> int
        actorArr.[a] <- spawn system (string a) processor
        ()
    )
    {1I..n} |> Seq.iter (fun n ->
        let a = n |> int
        let ed = a+k-1 |> bigint
        actorArr.[a] <! ProcessJob(n, ed)
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
                           let diff = N |> int
                           let diffe = diff - K
                           //printfn "%i" diffe
                           return! loop ()
        | Ans(first) -> //printfn "In master%i" first
                        printfn "result: %i" (int first)
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

masterRef <! GotInput(fsi.CommandLineArgs.[1] |> int |> bigint, fsi.CommandLineArgs.[2] |> int)

// //Async.RunSynchronously start
// System.Console.ReadLine()
while flag<>1 do
    Threading.Thread.Sleep 100
system.Terminate()
