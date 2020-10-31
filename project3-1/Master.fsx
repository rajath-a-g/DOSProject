#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"
#load "PastryNode.fsx"

open PastryNode
open Akka.FSharp
open System

type MasterMessage = 
                    | Start
                    | InitialJoin of array<int>
                    | StartRouting
                    | SecondaryJoin
                    | FinishedJoining
                    | Route of string * int * int * int
                    | NotInBoth
                    | RouteFinish of int * int * int
                    | RouteNotInBoth

let Master numNodes numRequests system (mailbox:Actor<_>) =
    let base1 = (Math.Log((double numNodes)) / Math.Log(4.0)) |> ceil |> int
    let Base = (float base1)
    let nodeSpace = Math.Pow(4.0, Base) |> int
    let mutable (randomList : array<int>) = Array.empty   
    let mutable groupOneSize = if numNodes <= 1024 then numNodes else 1024
    let mutable (groupOne : array<int>) = Array.zeroCreate (groupOneSize+1)
    let mutable numHops = 0
    let mutable numJoined = 0
    let mutable notInBoth = 0
    let mutable numRouteNotInBoth = 0
    let mutable numRouted = 0
    let rand = Random()

    let swap (a: _[]) x y =
        let tmp = a.[x]
        a.[x] <- a.[y]
        a.[y] <- tmp

    // shuffle an array (in-place)
    let shuffle a =
        Array.iteri (fun i _ -> swap a i (rand.Next(i, (Array.length a)))) a

    {0..nodeSpace} |> Seq.iter (fun i -> 
        randomList <- Array.append [|i|] randomList
    )
    shuffle randomList
    // printfn "NumNodes %d" numNodes
    // printfn "Random list : %A" randomList
    // printfn "%d" groupOne.[2]

    {0..groupOneSize} |> Seq.iter (fun i ->
        //printfn "%d" i
        groupOne.[i] <- randomList.[i]
        //printfn "%d" i
    )
    printfn "Here?"
    {0..numNodes} |> Seq.iter (fun i -> 
        let base1 = Base |> int
        //printfn "%d" i
        spawn system (string randomList.[i]) <| PastryNode numNodes numRequests randomList.[i] base1 |> ignore
    )
    printfn "Here?"

    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
        | Start -> {0..groupOneSize} |> Seq.iter (fun i ->
                        let arrayCopy = Array.copy groupOne
                        mailbox.ActorSelection("/user/master/" + (string randomList.[i])) <! InitialJoin(arrayCopy)
                    )
        | FinishedJoining -> numJoined <- numJoined + 1
                             if numJoined = groupOneSize then
                                if numJoined >= numNodes then
                                    mailbox.Self <! StartRouting
                                else
                                    mailbox.Self <! SecondaryJoin
                             if numJoined > groupOneSize then
                                if numJoined = numNodes then
                                    mailbox.Self <! StartRouting
                                else
                                    mailbox.Self <! SecondaryJoin
        | SecondaryJoin -> let startId = randomList.[rand.Next(numJoined)]
                           mailbox.ActorSelection("/user/master/" + (string startId)) <! Route("Join", startId, randomList.[numJoined], -1)
        | StartRouting -> printfn "Routing"
                          mailbox.ActorSelection("/user/master/*") <! StartRouting
        | NotInBoth -> notInBoth <- notInBoth + 1
        | RouteFinish(requestTo, requestFrom, hops) -> numRouted <- numRouted + 1
                                                       numHops <- numHops + hops
                                                       {1..10} |> Seq.iter (fun i -> 
                                                            if numRouted = (numNodes * numRequests * (i / 10)) then
                                                                {1..i} |> Seq.iter (fun j ->
                                                                    printf "."
                                                                )
                                                                printf "|"  
                                                       )
                                                       if numRouted >= (numRequests * numNodes) then
                                                            printfn ""
                                                            printfn "Total Routes-> %d Total Hops-> %d" numRouted numHops
                                                            printfn "Average Hops Per Route-> %f" ((double numHops) / (double numRouted))
                                                            mailbox.Context.System.Terminate() |> ignore
        | RouteNotInBoth -> numRouteNotInBoth <- numRouteNotInBoth + 1
        | _ -> printfn "Wrong message"
        return! loop ()
    }
    loop ()