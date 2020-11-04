module PastrySimulator

#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open System
open Akka.FSharp

let config =
    Configuration.parse
        @"akka {
                log-dead-letters = off
            }
        }"

let system = System.create "FSharp" (config)
let mutable terminate = true
let rng = Random()

// Enabler functions used in the algorithm.
// shuffle all the values in the passed array
let Shuffle (org:_[]) = 
    let arr = Array.copy org
    let max = (arr.Length - 1)
    let randomSwap (arr:_[]) i =
        let pos = rng.Next(max)
        let tmp = arr.[pos]
        arr.[pos] <- arr.[i]
        arr.[i] <- tmp
        arr
   
    [|0..max|] |> Array.fold randomSwap arr

// Converts the passed integer to a list of integers for the passed base(b)
let bigintToDigits b source =
    let rec loop (b : int) num digits =
        let (quotient, remainder) = bigint.DivRem(num, bigint b)
        match quotient with
        | zero when zero = 0I -> int remainder :: digits
        | _ -> loop b quotient (int remainder :: digits)
    loop b source []

// Converts the passed integer to string
let digitsToString length source =
    let base4String = source |> List.map (fun (x : int) -> x.ToString("X").ToLowerInvariant()) |> String.concat ""
    let zeroLength = length - base4String.Length
    String.replicate zeroLength "0" + base4String

// Converts the passed integer to a string on base 4 scale
let getBase4String (source:int) (length:int) =
    let bigintToBase4 = bigintToDigits 4
    bigint(source) |> bigintToBase4 |>  digitsToString length

// Returns the maximum prefix match length between the strings
let getPrefixLen (s1:string) (s2:string) = 
    let mutable j = 0
    while j < s1.Length && s1.[j] = s2.[j] do
        j <- j+1
    j

type Input = Start 
            | PrimaryJoin
            | SecondaryJoin
            | StartRouting
            | FinishedJoining
            | RouteFinish of int*int*int
            | InitialJoin of int array
            | Route of string*int*int*int
            | AddRow of int*(int array)
            | AddLeaf of int array
            | UpdateID of int
            | Acknowledgement
            | DisplayNodeState

let node numNodes numRequests myID baseVal (childMailbox: Actor<_>) = 
    let mutable smallerLeafs = Set.empty
    let mutable largerLeafs = Set.empty
    let nodeIDSpace = int(float(4) ** float(baseVal))
    let mutable numOfBack = 0
    let routingTable = Array2D.create baseVal 4 -1

    // Updating the node in either largerLeaf, smallerLeaf or routing table by it's ID value
    let addToNodeState nodeID = 
        if nodeID > myID && not (largerLeafs.Contains nodeID) then
            if largerLeafs.Count < 4 then
                largerLeafs <- largerLeafs.Add(nodeID)
            elif nodeID < largerLeafs.MaximumElement then
                largerLeafs <- largerLeafs.Remove largerLeafs.MaximumElement
                largerLeafs <- largerLeafs.Add(nodeID)
        elif nodeID < myID && not (smallerLeafs.Contains nodeID) then
            if smallerLeafs.Count < 4 then
                smallerLeafs <- smallerLeafs.Add(nodeID)
            elif nodeID > smallerLeafs.MinimumElement then
                smallerLeafs <- smallerLeafs.Remove smallerLeafs.MinimumElement
                smallerLeafs <- smallerLeafs.Add(nodeID) 
        // Add to routing table if not present already
        let prefixLen = getPrefixLen (getBase4String myID baseVal) (getBase4String nodeID baseVal)
        if routingTable.[prefixLen, (getBase4String nodeID baseVal).[prefixLen] |> string |> int] = -1 then
           routingTable.[prefixLen, (getBase4String nodeID baseVal).[prefixLen] |> string |> int] <- nodeID

    let addGroupToNodeState nodeIDs = 
        for nodeID in nodeIDs do
            addToNodeState nodeID

    let rec childLoop() = 
        actor {
            let! message = childMailbox.Receive()
            let sender = childMailbox.Sender()
            match message with
            | StartRouting -> 
                for i in 1..numRequests do
                    let message = Route("Route", myID, rng.Next(nodeIDSpace), -1)
                    system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(1000 |> float), childMailbox.Self, message)
            | InitialJoin(groupOne) -> 
                // All probable ID's are generated randomly considering a range with groupOne
                // Adding all probable IDs to leafSets and informing master about joining the network.
                let nodeGroupOne = groupOne |> Array.filter ((<>)myID)
                addGroupToNodeState nodeGroupOne
                for i in 0..baseVal-1 do
                    routingTable.[i, (getBase4String myID baseVal).[i] |> string |> int] <- myID
                sender <! FinishedJoining
            | Route(msg, source, destination, hops) ->
                if msg = "Join" then 
                    // If a single bit is set as part of prefixLen of destination (nodeID) with respect to myID,
                    // then add the entries to routingTable upto PrefixLen ,else update by prefixLen index in routingTable
                    let prefixLen = getPrefixLen (getBase4String myID baseVal) (getBase4String destination baseVal)
                    let destinationRef = select ("akka://FSharp/user/master/"+ (destination |> string)) system
                    if hops = -1 && prefixLen > 0 then
                        for i in 0..prefixLen-1 do
                            destinationRef <! AddRow(i, Array.copy routingTable.[i, *])
                    destinationRef <! AddRow(prefixLen, Array.copy routingTable.[prefixLen, *])
                    // Checks if the ID of helping node leafset can be used for next routing or not.
                    // Routing the message to the node with smallest distance irrespective of the range of helping node.
                    if (smallerLeafs.Count > 0 && destination >= smallerLeafs.MinimumElement && destination <= myID)
                        || (largerLeafs.Count > 0 && destination <= largerLeafs.MaximumElement && destination >= myID) then
                        // Search for the nearest node in either smallerLeafs or largerLeafs sets
                        let mutable diff = nodeIDSpace + 10
                        let mutable nearest = -1
                        if destination < myID then
                            // Updating the nearest in smallerLeaf set
                            for smallerLeaf in smallerLeafs do
                                if abs (destination - smallerLeaf) < diff then 
                                    nearest <- smallerLeaf
                                    diff <- abs (destination - smallerLeaf)
                        else
                            // Updating the nearest in largerLeaf set
                            for largerLeaf in largerLeafs do
                                if abs (destination - largerLeaf) < diff then
                                    nearest <- largerLeaf
                                    diff <- abs (destination - largerLeaf)
                        // If it is in the leaf but not near to myID, then it is the nearest found,
                        // Updating it by call to nearestRef
                        if abs (destination - myID) > diff then
                            let nearestRef = select ("akka://FSharp/user/master/"+ (nearest |> string)) system
                            nearestRef <! Route(msg, source, destination, hops+1)
                        else 
                            // This is converging condition and it is the closest id possible
                            // Create leafSet for destination and send message to addLeafs
                            let destLeafs = (Set.union smallerLeafs largerLeafs).Add(myID)
                            destinationRef <! AddLeaf(Set.toArray destLeafs)
                    elif smallerLeafs.Count < 4 && smallerLeafs.Count > 0 && destination < smallerLeafs.MinimumElement then
                        let smallerLeafsMinRef = select ("akka://FSharp/user/master/"+ (smallerLeafs.MinimumElement |> string)) system
                        smallerLeafsMinRef <! Route(msg, source, destination, hops+1)
                    elif largerLeafs.Count < 4 && largerLeafs.Count > 0 && destination > largerLeafs.MaximumElement then 
                        let largerLeafsMaxRef = select ("akka://FSharp/user/master/"+ (largerLeafs.MaximumElement |> string)) system
                        largerLeafsMaxRef <! Route(msg, source, destination, hops+1) 
                    elif (smallerLeafs.Count = 0 && destination < myID) || (largerLeafs.Count = 0 && destination > myID) then
                        // This is converging condition and it is the closest id possible
                        // Create leafSet for destination and send message to addLeafs
                        let destLeafs = (Set.union smallerLeafs largerLeafs).Add(myID)
                        destinationRef <! AddLeaf(Set.toArray destLeafs) 
                    elif routingTable.[prefixLen, (getBase4String destination baseVal).[prefixLen] |> string |> int] <> -1 then
                        let routingEntryRef =select ("akka://FSharp/user/master/"+ (routingTable.[prefixLen, (getBase4String destination baseVal).[prefixLen] |> string |> int] |> string)) system
                        routingEntryRef <! Route(msg, source, destination, hops+1)
                    elif destination > myID then
                        let largerLeafsMaxRef = select ("akka://FSharp/user/master/"+ (largerLeafs.MaximumElement |> string)) system
                        largerLeafsMaxRef <! Route(msg, source, destination, hops+1)  
                    elif destination < myID then 
                        let smallerLeafsMinRef = select ("akka://FSharp/user/master/"+ (smallerLeafs.MinimumElement |> string)) system
                        smallerLeafsMinRef <! Route(msg, source, destination, hops+1)
                    else 
                        printfn "Not possible"
                elif msg = "Route" then
                    // From this point on, the algorithm begins sending messages.
                    // If the ID is equal to destination, we have completed routing, else find the next node
                    // for routing considering it's leafsets and routing table to acheive minimum number of hops to reach destination.
                    if myID = destination then
                        let parent = select ("akka://FSharp/user/master") system 
                        parent <! RouteFinish(source, destination, hops+1)
                    else
                        let prefixLen = getPrefixLen (getBase4String myID baseVal) (getBase4String destination baseVal)
                        if (smallerLeafs.Count > 0 && destination >= smallerLeafs.MinimumElement && destination < myID) ||
                            (largerLeafs.Count > 0 && destination <= largerLeafs.MaximumElement && destination > myID) then
                            let mutable diff = nodeIDSpace + 10
                            let mutable nearest = -1
                            if destination < myID then
                                // Updating the nearest in smallerLeaf set
                                for smallerLeaf in smallerLeafs do
                                    if abs (destination - smallerLeaf) < diff then 
                                        nearest <- smallerLeaf
                                        diff <- abs (destination - smallerLeaf)
                            else
                                // Updating the nearest in largerLeaf set
                                for largerLeaf in largerLeafs do
                                    if abs (destination - largerLeaf) < diff then
                                        nearest <- largerLeaf
                                        diff <- abs (destination - largerLeaf)
                            // If it is in the leaf but not near to myID, then it is the nearest found,
                            // Updating it by call to nearestRef
                            if abs (destination - myID) > diff then
                                let nearestRef = select ("akka://FSharp/user/master/"+ (nearest |> string)) system
                                nearestRef <! Route(msg, source, destination, hops+1)
                            else 
                                // printfn "%A %d %d" message myID destination
                                let parent = select ("akka://FSharp/user/master") system 
                                parent <! RouteFinish(source, destination, hops+1)
                        elif smallerLeafs.Count < 4 && smallerLeafs.Count > 0 && destination < smallerLeafs.MinimumElement then
                            let smallerLeafsMinRef = select ("akka://FSharp/user/master/"+ (smallerLeafs.MinimumElement |> string)) system
                            smallerLeafsMinRef <! Route(msg, source, destination, hops+1)
                        elif largerLeafs.Count < 4 && largerLeafs.Count > 0 && destination > largerLeafs.MaximumElement then
                            let largerLeafsMaxRef = select ("akka://FSharp/user/master/"+ (largerLeafs.MaximumElement |> string)) system
                            largerLeafsMaxRef <! Route(msg, source, destination, hops+1)   
                        elif (smallerLeafs.Count = 0 && destination < myID) || (largerLeafs.Count = 0 && destination > myID) then
                            // This is converging condition and it is the closest id possible
                            let parent = select ("akka://FSharp/user/master") system 
                            parent <! RouteFinish(source, destination, hops+1)
                        elif routingTable.[prefixLen, (getBase4String destination baseVal).[prefixLen] |> string |> int] <> -1 then
                            let routingEntryRef =select ("akka://FSharp/user/master/"+ (routingTable.[prefixLen, (getBase4String destination baseVal).[prefixLen] |> string |> int] |> string)) system
                            routingEntryRef <! Route(msg, source, destination, hops+1)
                        elif destination > myID then
                            let largerLeafsMaxRef = select ("akka://FSharp/user/master/"+ (largerLeafs.MaximumElement |> string)) system
                            largerLeafsMaxRef <! Route(msg, source, destination, hops+1)   
                        elif destination < myID then
                            let smallerLeafsMinRef = select ("akka://FSharp/user/master/"+ (smallerLeafs.MinimumElement |> string)) system
                            smallerLeafsMinRef <! Route(msg, source, destination, hops+1) 
                        else
                            printfn "Not possible"
            // Updating the routingTable with the passed row value
            | AddRow(rowNumber, tableRow) -> 
                for i in 0..tableRow.Length-1 do
                    if routingTable.[rowNumber, i] = -1 then
                        routingTable.[rowNumber, i] <- tableRow.[i]
            // Updating the leafsets on converging conditions with nearest IDs
            | AddLeaf(leafSet) ->
                addGroupToNodeState leafSet
                for smallerLeaf in smallerLeafs do
                    numOfBack <- numOfBack + 1
                    let smallerLeafRef = select ("akka://FSharp/user/master/"+ (smallerLeaf |> string)) system 
                    smallerLeafRef <! UpdateID(myID)
                for largerLeaf in largerLeafs do
                    numOfBack <- numOfBack + 1
                    let largerLeafRef = select ("akka://FSharp/user/master/"+ (largerLeaf |> string)) system 
                    largerLeafRef <! UpdateID(myID)
                for i in 0..baseVal-1 do
                    routingTable.[i, (getBase4String myID baseVal).[i] |> string |> int] <- myID
            // Acknowledgement from the node that joined the system with it's information is done through UpdateID and Acknowledgement
            | UpdateID(newNodeID) ->
                addToNodeState newNodeID
                sender <! Acknowledgement
            | Acknowledgement ->
                numOfBack <- numOfBack - 1
                if numOfBack = 0 then
                    let parent = select ("akka://FSharp/user/master") system 
                    parent <! FinishedJoining
            | _-> ignore 0
            return! childLoop()
        }
    childLoop()

let pastryNode (numNodes:int) (numRequests:int) (mailbox : Actor<_>) = 
    // Maximum number of digits the number of nodes can have considering range from 0-3
    let baseVal = int(ceil (log10(float(numNodes))/log10(float(4))))
    // The nodeIDSpace represents the range of nodeID's that can be present in the system
    let nodeIDSpace = int(float(4) ** float(baseVal))
    // Randomly map the nodeIDSpace to indices amongst the group for assigning nodeID randomly to the nodes
    let randomIdMapping = [|0 .. nodeIDSpace-1|] |> Shuffle
    let groupOneSize = min numNodes 1024
    let groupOne = Array.copy randomIdMapping.[0..groupOneSize-1]
    let mutable numJoined = 0
    let mutable numHops = 0
    let mutable numRouted = 0

    // Spawn node actors of the overlay network
    for index in 0..numNodes-1 do
        spawn mailbox (randomIdMapping.[index] |> string) (node numNodes numRequests randomIdMapping.[index] baseVal) |> ignore
    
    let rec loop() = actor{
        let! message = mailbox.Receive()
        match message with
        | Start -> 
            printfn "Starting primary routing..."
            for nodeID in groupOne do
                let nodeRef = select ("akka://FSharp/user/master/"+ (nodeID |> string)) system
                nodeRef <! InitialJoin (Array.copy groupOne)
        | FinishedJoining ->  
            numJoined <- numJoined + 1
            if numJoined = groupOneSize then
                printfn "Network has been built, Waiting for requests...."
                if numJoined >= numNodes then 
                    mailbox.Self <! StartRouting
                else 
                    mailbox.Self <! SecondaryJoin
            if numJoined > groupOneSize then
                if numJoined = numNodes then 
                    mailbox.Self <! StartRouting
                else 
                    mailbox.Self <! SecondaryJoin
        | StartRouting -> 
            printfn "Started the routing now.....We will keep you posted with the progress!"
            system.ActorSelection("akka://FSharp/user/master/*") <! StartRouting
        | SecondaryJoin -> 
            // Select random actor from those already joined
            let randomID = randomIdMapping.[rng.Next(numJoined)]
            let randomRef = select ("akka://FSharp/user/master/"+ (randomID |> string)) system 
            randomRef <! Route("Join", randomID, randomIdMapping.[numJoined], -1)
        | RouteFinish(source, destination, hops) -> 
            numRouted <- numRouted + 1
            numHops <- numHops + hops
            for i in 1..10 do
                if numRouted = numNodes*numRequests*i/10 then
                    printfn "We have finished %d0 percent of the routing" i
            if numRouted >= numNodes*numRequests then
                printfn "\n"
                printfn "---------------------------------"
                printfn "Total Routes = %d Total Hops = %d" numRouted numHops
                let ratio = float(numHops)/float(numRouted)
                printfn "Average hops per route = %f" ratio
                printfn "---------------------------------"
                system.Stop(mailbox.Self)
                terminate <- false
        | _-> ignore 0
        return! loop()
    }
    loop()

let pastry (numNodes:int) (numRequests:int) = 
    printfn "Starting Pastry simulation for numNodes = %i numRequests = %i" numNodes numRequests
    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
    let parentRef = spawn system "master" (pastryNode numNodes numRequests)
    parentRef <! Start
    while terminate do
        ignore 0
    stopWatch.Stop()
    printfn "Total time elapsed is given by %f milliseconds" stopWatch.Elapsed.TotalMilliseconds
