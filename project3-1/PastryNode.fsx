#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open Akka.FSharp
open System

type PastryMessage = 
                    | StartRouting
                    | Route of string * int * int * int
                    | InitialJoin of array<int>
                    | FinishedRouting
                    | RouteFinish of int * int * int
                    | Update of int
                    | Acknowledgement
                    | FinishedJoining
                    | DisplayLeafAndRouting
                    | AddRow of int * array<int>
                    | AddLeaf of array<int>
                    | NotInBoth
                    | RouteNotInBoth

let public PastryNode numNodes numRequests myID Base (mailbox:Actor<_>) =
    let mutable (smallerLeaf : array<int>) = Array.empty
    let mutable IDSpace = Math.Pow(4.0, (float Base)) |> int
    let mutable (largerLeaf : array<int>) = Array.empty
    let mutable numOfBack = 0
    let mutable (routingTable  : array<array<int>>) = Array.zeroCreate (Base+1)

    //printfn "pASTRY"
    {0..Base} |> Seq.iter(fun i ->
        let array1 = [|-1;-1;-1;-1|]
        routingTable.[i] <- array1
    )
    //printfn "pASTRY"
    let toBase4String (raw : int) length = 
        let mutable str = Convert.ToString(raw, 4)
        let diff = length - str.Length
        if diff > 0 then
            let mutable j = 0
            while j < diff do
                str <- "0" + str
                j <- j + 1
        str
    
    let checkPrefix (string1 : string) (string2 : string) =
        let mutable j = 0
        while j < string1.Length && string1.[j].Equals(string2.[j]) do
            j <- j + 1
        j

    let addBuffer (all : array<int>) =
        Array.iter (fun i ->
            if i > myID && not (Array.contains i largerLeaf) then
                if largerLeaf.Length < 4 then
                    largerLeaf <- Array.append [|i|] largerLeaf
                else
                    if i < (Array.max largerLeaf) then
                        largerLeaf <- Array.filter (fun elem -> elem = (Array.max largerLeaf)) largerLeaf
                        largerLeaf <- Array.append [|i|] largerLeaf
            else if i < myID && not (Array.contains i smallerLeaf) then
                if smallerLeaf.Length < 4 then
                    smallerLeaf <- Array.append [|i|] smallerLeaf
                else
                    if i > (Array.min smallerLeaf) then
                        smallerLeaf <- Array.filter (fun elem -> elem = (Array.min smallerLeaf)) smallerLeaf
                        smallerLeaf <- Array.append [|i|] smallerLeaf

            let ibase = toBase4String i Base
            let samePrefix = (checkPrefix (toBase4String myID Base) ibase)
            if routingTable.[samePrefix].[(int ibase.[samePrefix])] = -1 then
                routingTable.[samePrefix].[(int ibase.[samePrefix])] <- i

        ) all
    
    let addNode (node : int) =
        if node > myID && (Array.contains node largerLeaf) then
            if largerLeaf.Length < 4 then
                largerLeaf <- Array.append [|node|] largerLeaf
            else
                if node < (Array.max largerLeaf) then
                    largerLeaf <- Array.filter (fun elem -> elem = (Array.max largerLeaf)) largerLeaf
                    largerLeaf <- Array.append [|node|] largerLeaf
        else if node < myID && not (Array.contains node smallerLeaf) then
            if smallerLeaf.Length < 4 then
                smallerLeaf <- Array.append [|node|] smallerLeaf
            else
                if node > (Array.min smallerLeaf) then
                    smallerLeaf <- Array.filter (fun elem -> elem = (Array.min smallerLeaf)) smallerLeaf
                    smallerLeaf <- Array.append [|node|] smallerLeaf

        let ibase = toBase4String node Base
        let samePrefix = (checkPrefix (toBase4String myID Base) ibase)
        if routingTable.[samePrefix].[(int ibase.[samePrefix])] = -1 then
            routingTable.[samePrefix].[(int ibase.[samePrefix])] <- node
    
    // let display =
    //     printfn "smallerLeaf:%A" smallerLeaf
    //     printfn "largerLeaf:%A" largerLeaf
    //     {0..Base} |> Seq.iter (fun i ->
    //         printf "Row %d : \n" i
    //         {0..4} |> Seq.iter (fun j ->
    //             printf "%A " routingTable.[i].[j]
    //             printf "/n"
    //         )
    //     )

    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
        | StartRouting -> {1..numRequests} |> Seq.iter (fun i -> 
                            let duration = TimeSpan(1000L)
                            let rnd = Random ()
                            mailbox.Context.System.Scheduler.ScheduleTellOnce(duration, mailbox.Self, Route("Route", myID, rnd.Next(IDSpace), - 1))
                          )
        | InitialJoin(groupOne) ->  let mutable group = Array.filter (fun elem -> elem = myID) groupOne
                                    addBuffer group
                                    {0..Base} |> Seq.iter (fun i ->
                                        routingTable.[i].[(int (toBase4String myID Base).[i])] <- myID
                                    )
                                    mailbox.Sender () <! FinishedRouting
        | Route(msg, requestFrom, requestTo, hops) -> if msg = "Join" then
                                                        let samePrefix = (checkPrefix (toBase4String myID Base) (toBase4String requestTo Base))
                                                        if hops = -1 && samePrefix > 0 then
                                                            {0..samePrefix} |> Seq.iter (fun i ->
                                                                let rowArrayCopy = Array.empty
                                                                Array.Copy(routingTable.[i], rowArrayCopy, routingTable.[i].Length)
                                                                mailbox.ActorSelection("/user/master/" + (string requestTo)) <! AddRow(i, rowArrayCopy)
                                                            )
                                                        let samePrefixCopy = Array.empty
                                                        Array.Copy(routingTable.[samePrefix], samePrefixCopy, routingTable.[samePrefix].Length)
                                                        mailbox.ActorSelection("/user/master/" + (string requestTo)) <! AddRow(samePrefix, samePrefixCopy)
                                                        if (smallerLeaf.Length > 0 && requestTo >= (Array.min smallerLeaf) && requestTo <= myID)  || (largerLeaf.Length > 0 && requestTo <= (Array.max largerLeaf) && requestTo <= myID) then
                                                            let mutable diff = IDSpace + 10
                                                            let mutable nearest = -1
                                                            if (requestTo < myID) then
                                                                Array.iter(fun i ->
                                                                    if Math.Abs(requestTo - i) < diff then
                                                                        nearest <- i
                                                                        diff <- Math.Abs(requestTo - i)
                                                                ) smallerLeaf
                                                            else
                                                                Array.iter(fun i -> 
                                                                    if Math.Abs(requestTo - i) < diff then
                                                                        nearest <- i
                                                                        diff <- Math.Abs(requestTo - i)
                                                                ) largerLeaf

                                                            if Math.Abs(requestTo - myID) > diff then
                                                                mailbox.ActorSelection("/user/master/" + (string requestTo)) <! Route(msg, requestFrom, requestTo, hops + 1)
                                                            else
                                                                let mutable (allLeaf : array<int>) = Array.empty
                                                                allLeaf <- Array.append [|myID|] allLeaf
                                                                allLeaf <- Array.append smallerLeaf allLeaf
                                                                allLeaf <- Array.append largerLeaf allLeaf
                                                                mailbox.ActorSelection("/user/master/" + (string requestTo)) <! AddLeaf(allLeaf)
                                                        else if smallerLeaf.Length < 4 && smallerLeaf.Length > 0 && requestTo > (Array.min smallerLeaf) then
                                                            mailbox.ActorSelection("/user/master/" + (string (Array.min smallerLeaf))) <! Route(msg, requestFrom, requestTo, hops + 1)
                                                        else if (largerLeaf.Length < 4 && largerLeaf.Length > 0) && (requestTo > (Array.max largerLeaf)) then
                                                            mailbox.ActorSelection("/user/master/" + (string (Array.max largerLeaf))) <! Route(msg, requestFrom, requestTo, hops + 1)
                                                        else if (smallerLeaf.Length = 0 && requestTo < myID) || (largerLeaf.Length = 0 && requestTo > myID) then
                                                            let mutable (allLeaf : array<int>) = Array.empty
                                                            allLeaf <- Array.append [|myID|] allLeaf
                                                            allLeaf <- Array.append smallerLeaf allLeaf
                                                            allLeaf <- Array.append largerLeaf allLeaf
                                                            mailbox.ActorSelection("/user/master/" + (string requestTo)) <! AddLeaf(allLeaf)
                                                        else if routingTable.[samePrefix].[(int (toBase4String requestTo Base).[samePrefix])] <> -1 then
                                                            mailbox.ActorSelection("/user/master/" + (string routingTable.[samePrefix].[(int (toBase4String requestTo Base).[samePrefix])])) <! Route(msg, requestFrom, requestTo, hops + 1)
                                                        else if requestTo > myID then
                                                            mailbox.ActorSelection("/user/master/" + (string (Array.max largerLeaf))) <! Route(msg, requestFrom, requestTo, hops + 1)
                                                        else if requestTo < myID then
                                                            mailbox.ActorSelection("/user/master/" + (string (Array.min smallerLeaf))) <! Route(msg, requestFrom, requestTo, hops + 1)
                                                            mailbox.Context.Parent <! NotInBoth
                                                        else
                                                            printfn "Not Possible"
                                                      else if msg = "Route" then
                                                        if myID = requestTo then
                                                            mailbox.Context.Parent <! RouteFinish(requestFrom, requestTo, hops + 1)
                                                        else
                                                            let samePrefix = (checkPrefix (toBase4String myID Base) (toBase4String requestTo Base))
                                                            if (smallerLeaf.Length > 0 && requestTo >= (Array.min smallerLeaf) && requestTo < myID) || (largerLeaf.Length > 0 && requestTo <= (Array.max largerLeaf) && requestTo > myID) then
                                                                let mutable diff = IDSpace + 10
                                                                let mutable nearest = -1
                                                                if requestTo < myID then
                                                                    Array.iter (fun i -> 
                                                                        if Math.Abs(requestTo - i) < diff then
                                                                            nearest <- i
                                                                            diff <- Math.Abs(requestTo - i)
                                                                    ) smallerLeaf
                                                                else
                                                                    Array.iter (fun i ->
                                                                        if Math.Abs(requestTo - i) < diff then
                                                                            nearest <- i
                                                                            diff <- Math.Abs(requestTo - i)
                                                                    ) largerLeaf
                                                                if Math.Abs(requestTo - myID) > diff then
                                                                    mailbox.ActorSelection("/user/master/" + (string nearest)) <! Route(msg, requestFrom, requestTo, hops + 1)
                                                                else
                                                                    mailbox.Context.Parent <! RouteFinish(requestFrom, requestTo, hops + 1)
                                                            else if smallerLeaf.Length < 4 && smallerLeaf.Length > 0 && requestTo < (Array.min smallerLeaf) then
                                                                mailbox.ActorSelection("/user/master/" + (string (Array.min smallerLeaf))) <! Route(msg, requestFrom, requestTo, hops + 1)
                                                            else if largerLeaf.Length < 4 && largerLeaf.Length > 0 && requestTo > (Array.max largerLeaf) then
                                                                mailbox.ActorSelection("/user/master/" + (string (Array.max largerLeaf))) <! Route(msg, requestFrom, requestTo, hops + 1)
                                                            else if (smallerLeaf.Length = 0 && requestTo < myID) || (largerLeaf.Length = 0 && requestTo > myID) then
                                                                mailbox.Context.Parent <! RouteFinish(requestFrom, requestTo, hops + 1)
                                                            else if routingTable.[samePrefix].[(int (toBase4String requestTo (hops+1)).[samePrefix])] <> -1 then
                                                                mailbox.ActorSelection("/user/master/" + (string routingTable.[samePrefix].[(int (toBase4String requestTo (hops + 1)).[samePrefix])])) <! Route(msg, requestFrom, requestTo, hops + 1)
                                                            else if requestTo > myID then
                                                                mailbox.ActorSelection("/user/master/" + (string(Array.max largerLeaf))) <! Route(msg, requestFrom, requestTo, hops + 1)
                                                                mailbox.Context.Parent <! RouteNotInBoth
                                                            else if requestTo < myID then
                                                                mailbox.ActorSelection("/user/master/" + (string(Array.min smallerLeaf))) <! Route(msg, requestFrom, requestTo, hops + 1)
                                                                mailbox.Context.Parent <! RouteNotInBoth
                                                            else
                                                                printfn "Not Possible"
        | AddRow(rowNum, newRow) -> {0..4} |> Seq.iter (fun i -> 
                                                            if routingTable.[rowNum].[i] = -1 then
                                                                routingTable.[rowNum].[i] <- newRow.[i]
                                                       )
        | AddLeaf(allLeaf) -> addBuffer(allLeaf)
                              Array.iter (fun i -> 
                                numOfBack <- numOfBack + 1
                                mailbox.ActorSelection("/user/master/" + (string i)) <! Update(myID)
                              ) smallerLeaf
                              addBuffer(allLeaf)
                              Array.iter (fun i -> 
                                numOfBack <- numOfBack + 1
                                mailbox.ActorSelection("/user/master/" + (string i)) <! Update(myID)
                              ) largerLeaf
                              {0..Base} |> Seq.iter (fun i ->
                                {0..4} |> Seq.iter (fun j -> 
                                    if routingTable.[i].[j] <> -1 then
                                        numOfBack <- numOfBack + 1
                                        mailbox.ActorSelection("/user/master/" + (string routingTable.[i].[j])) <! Update(myID)
                                )
                              )
                              {0..Base} |> Seq.iter (fun i ->
                                routingTable.[i].[(int (toBase4String myID Base).[i])] <- myID
                              )
        | Update(newID) -> addNode newID
                           mailbox.Sender() <! Acknowledgement
        | Acknowledgement -> numOfBack <- numOfBack - 1
                             if numOfBack = 0 then
                                mailbox.Context.Parent <! FinishedJoining
        | DisplayLeafAndRouting -> // display
                                   mailbox.Context.System.Terminate() |> ignore
        | _ -> printfn "Wrong Message"
        return! loop ()
    }
    loop ()
