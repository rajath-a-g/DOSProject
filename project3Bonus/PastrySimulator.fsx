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

// int -> bigint -> int list
let bigintToDigits b source =
    let rec loop (b : int) num digits =
        let (quotient, remainder) = bigint.DivRem(num, bigint b)
        match quotient with
        | zero when zero = 0I -> int remainder :: digits
        | _ -> loop b quotient (int remainder :: digits)
    loop b source []

let digitsToString length source =
    let base4String = source |> List.map (fun (x : int) -> x.ToString("X").ToLowerInvariant()) |> String.concat ""
    let zeroLength = length - base4String.Length
    String.replicate zeroLength "0" + base4String


let getBase4String (source:int) (length:int) =
    let bigintToBase4 = bigintToDigits 4
    bigint(source) |> bigintToBase4 |>  digitsToString length

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
            | RecoverLeaf of (int array) * int
            | RecoverRoutingTable of int * int * int
            | RemoveNode of int
            | RequestInRoutingTable of int * int
            | RequestLeafWithout of int
            | FailNodes
            | NodeDie

let node numNodes numRequests myID baseVal (childMailbox: Actor<_>) = 
    let mutable smallerLeafs = Set.empty
    let mutable largerLeafs = Set.empty
    let nodeIDSpace = int(float(4) ** float(baseVal))
    let mutable numOfBack = 0
    let routingTable = Array2D.create baseVal 4 -1

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
        // Add to routing table if required
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
                    // http://api.getakka.net/docs/stable/html/8D5B7D38.htm
                    let message = Route("Route", myID, rng.Next(nodeIDSpace), -1)
                    system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(1000 |> float), childMailbox.Self, message)
            | InitialJoin(groupOne) -> 
                // We have all the random id's or probable id since we got the message 
                // so delete my id from it and keep all other random id.
                // Now the make the leafset of node through addbuffer function.
                // Tell master about joining the network.
                let nodeGroupOne = groupOne |> Array.filter ((<>)myID)
                addGroupToNodeState nodeGroupOne
                for i in 0..baseVal-1 do
                    routingTable.[i, (getBase4String myID baseVal).[i] |> string |> int] <- myID
                sender <! FinishedJoining
            | Route(msg, source, destination, hops) ->
                if msg = "Join" then
                    // If myid and id of helping node has more than one bit in prefix than 
                    // current then take all the valid entries from its rtable into my routingtable
                    // Calculate prefix length 
                    let prefixLen = getPrefixLen (getBase4String myID baseVal) (getBase4String destination baseVal)
                    let destinationRef = select ("akka://FSharp/user/master/"+ (destination |> string)) system
                    if hops = -1 && prefixLen > 0 then
                        for i in 0..prefixLen-1 do
                            destinationRef <! AddRow(i, Array.copy routingTable.[i, *])
                    destinationRef <! AddRow(prefixLen, Array.copy routingTable.[prefixLen, *])
                    // * If id of helping node leafset can be used for next routing or not. 
                    // * Checking all entries in the smaller leaf table and routing the message to node 
                    //   with smallest differnce (proximity). 
                    // * Whether helping node id in range or not
                    if (smallerLeafs.Count > 0 && destination >= smallerLeafs.MinimumElement && destination <= myID)
                        || (largerLeafs.Count > 0 && destination <= largerLeafs.MaximumElement && destination >= myID) then
                        // Search for the nearest node in either smallerLeafs or largerLeafs sets
                        let mutable diff = nodeIDSpace + 10
                        let mutable nearest = -1
                        if destination < myID then
                            // In smaller leaf set
                            // Iterate over leaves and reset nearest accordingly
                            for smallerLeaf in smallerLeafs do
                                if abs (destination - smallerLeaf) < diff then 
                                    nearest <- smallerLeaf
                                    diff <- abs (destination - smallerLeaf)
                        else
                            // In larger leaf set
                            // Iterate over leaves and reset nearest accordingly
                            for largerLeaf in largerLeafs do
                                if abs (destination - largerLeaf) < diff then
                                    nearest <- largerLeaf
                                    diff <- abs (destination - largerLeaf)
                        // In leaf but not near to myID
                        // That is, nearest as found above is further than diff from myID
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
                    else
                        let mutable diff = nodeIDSpace + 10
                        let mutable nearest = -1
                        for i in 0..3 do
                            if routingTable.[prefixLen, i] <> -1 && Math.Abs(routingTable.[prefixLen, i] - destination) < diff then
                                diff <- Math.Abs(routingTable.[prefixLen, i] - destination)
                                nearest <- routingTable.[prefixLen, i]
                        if nearest <> -1 then
                            if nearest = myID then
                                if destination > myID then
                                    let largerMax = select ("akka://FSharp/user/master/" + (largerLeafs.MaximumElement |> string)) system
                                    largerMax <! Route(msg, source, destination, hops+1)
                                elif destination < myID then
                                    let smallerMin = select ("akka://FSharp/user/master/" + (smallerLeafs.MinimumElement |> string)) system
                                    smallerMin <! Route(msg, source, destination, hops+1)
                                else 
                                    printfn "Not possible!"
                            else
                                let nearestref = select ("akka://FSharp/user/master/" + (nearest |> string)) system
                                nearestref <! Route(msg, source, destination, hops+1)   
                    // elif destination > myID then
                    //     let largerLeafsMaxRef = select ("akka://FSharp/user/master/"+ (largerLeafs.MaximumElement |> string)) system
                    //     largerLeafsMaxRef <! Route(msg, source, destination, hops+1)  
                    //     // context.parent ! NotInBoth
                    // elif destination < myID then 
                    //     let smallerLeafsMinRef = select ("akka://FSharp/user/master/"+ (smallerLeafs.MinimumElement |> string)) system
                    //     smallerLeafsMinRef <! Route(msg, source, destination, hops+1)
                    // else 
                    //     printfn "Not possible"
                elif msg = "Route" then
                    // message is route, begin sending message
                    // Case 1 myID = destination ==> stop routing
                    // REMOVE: Else: find the nearest node in proximity and check its leafset and routing table till we reach its table
                    
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
                                // In smaller leaf set
                                // Iterate over leaves and reset nearest accordingly
                                for smallerLeaf in smallerLeafs do
                                    if abs (destination - smallerLeaf) < diff then 
                                        nearest <- smallerLeaf
                                        diff <- abs (destination - smallerLeaf)
                            else
                                // In larger leaf set
                                // Iterate over leaves and reset nearest accordingly
                                for largerLeaf in largerLeafs do
                                    if abs (destination - largerLeaf) < diff then
                                        nearest <- largerLeaf
                                        diff <- abs (destination - largerLeaf)
                            // In leaf but not near to myID
                            // That is, nearest as found above is further than diff from myID
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
                        else 
                            let mutable diff = nodeIDSpace + 10
                            let mutable nearest = -1
                            for i in 0..3 do
                            if routingTable.[prefixLen, i] <> -1 && Math.Abs(routingTable.[prefixLen, i] - destination) < diff then
                                diff <- Math.Abs(routingTable.[prefixLen, i] - destination)
                                nearest <- routingTable.[prefixLen, i]
                            if nearest <> -1 then
                                if nearest = myID then
                                    if destination > myID then
                                        let largerMax = select ("akka://FSharp/user/master/" + (largerLeafs.MaximumElement |> string)) system
                                        largerMax <! Route(msg, source, destination, hops+1)
                                    elif destination < myID then
                                        let smallerMin = select ("akka://FSharp/user/master/" + (smallerLeafs.MinimumElement |> string)) system
                                        smallerMin <! Route(msg, source, destination, hops+1)
                                    else 
                                        printfn "Not possible!"
                            else
                                let nearestref = select ("akka://FSharp/user/master/" + (nearest |> string)) system
                                nearestref <! Route(msg, source, destination, hops+1)

                        // elif destination > myID then
                        //     let largerLeafsMaxRef = select ("akka://FSharp/user/master/"+ (largerLeafs.MaximumElement |> string)) system
                        //     largerLeafsMaxRef <! Route(msg, source, destination, hops+1)   
                        // elif destination < myID then
                        //     let smallerLeafsMinRef = select ("akka://FSharp/user/master/"+ (smallerLeafs.MinimumElement |> string)) system
                        //     smallerLeafsMinRef <! Route(msg, source, destination, hops+1) 
                        // else
                        //     printfn "Not possible"
            | AddRow(rowNumber, tableRow) -> 
                for i in 0..tableRow.Length-1 do
                    if routingTable.[rowNumber, i] = -1 then
                        routingTable.[rowNumber, i] <- tableRow.[i]
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
            | UpdateID(newNodeID) ->
                addToNodeState newNodeID
                sender <! Acknowledgement
            | Acknowledgement ->
                numOfBack <- numOfBack - 1
                if numOfBack = 0 then
                    let parent = select ("akka://FSharp/user/master") system 
                    parent <! FinishedJoining
            | NodeDie -> system.ActorSelection("akka://FSharp/user/master/*") <! RemoveNode(myID)
                         printfn "Dead : %d" myID
                         system.Stop(childMailbox.Self)
            | RemoveNode(removeID) ->  if (removeID > myID) && (largerLeafs.Contains removeID) then
                                            largerLeafs <- largerLeafs.Remove removeID
                                            if largerLeafs.Count > 0 then
                                                let largerMax = select ("akka://FSharp/user/master/" + (largerLeafs.MaximumElement |> string)) system
                                                largerMax <! RequestLeafWithout(removeID)
                                       if (removeID < myID) && (smallerLeafs.Contains removeID) then
                                            smallerLeafs <- smallerLeafs.Remove removeID
                                            if smallerLeafs.Count > 0 then
                                                let smallerMin = select ("akka://FSharp/user/master/" + (smallerLeafs.MinimumElement |> string)) system
                                                smallerMin <! RequestLeafWithout removeID
                                       let prefixLen = getPrefixLen (getBase4String myID baseVal) (getBase4String removeID baseVal)
                                       if routingTable.[prefixLen, (getBase4String removeID baseVal).[prefixLen] |> string |> int] = removeID then
                                            routingTable.[prefixLen, (getBase4String removeID baseVal).[prefixLen] |> string |> int] <- -1
                                            for i in 0..3 do
                                                if routingTable.[prefixLen, i] <> myID && routingTable.[prefixLen, i] <> removeID && routingTable.[prefixLen, i] <> -1 then
                                                    let prefixRef = select ("akka://FSharp/user/master/" + (routingTable.[prefixLen, i] |> string)) system
                                                    prefixRef <! RequestInRoutingTable(prefixLen, (getBase4String removeID baseVal).[prefixLen] |> string |> int)
            | RequestLeafWithout(theID) -> let mutable temp = Set.empty
                                           temp <- Set.union temp largerLeafs
                                           temp <- Set.union temp smallerLeafs
                                           temp <- temp.Remove theID
                                           let tempcp = (Set.toArray temp)
                                           sender <! RecoverLeaf(Array.copy tempcp, theID)
            | RecoverLeaf(newList, theDead) -> for i in newList do
                                                    if i > myID && not (largerLeafs.Contains i) then
                                                        if largerLeafs.Count < 4 then
                                                            largerLeafs <- largerLeafs.Add(i)
                                                        else
                                                            if i < largerLeafs.MaximumElement then
                                                                largerLeafs <- largerLeafs.Remove(largerLeafs.MaximumElement)
                                                                largerLeafs <- largerLeafs.Add(i)
                                                    elif i < myID && not (smallerLeafs.Contains i) then
                                                        if smallerLeafs.Count < 4 then
                                                            smallerLeafs <- smallerLeafs.Add(i)
                                                        else 
                                                            if i > (smallerLeafs.MinimumElement) then
                                                                smallerLeafs <- smallerLeafs.Remove(smallerLeafs.MinimumElement)
                                                                smallerLeafs <- smallerLeafs.Add(i)
            | RequestInRoutingTable(prefixLen, column) -> if routingTable.[prefixLen, column] <> -1 then
                                                                sender <! RecoverRoutingTable(prefixLen, column, routingTable.[prefixLen, column])
            | RecoverRoutingTable(row, column, newId) -> if routingTable.[row, column] <> -1 then
                                                                routingTable.[row, column] <- newId
            | _-> ignore 0
            return! childLoop()
        }
    childLoop()

let application (numNodes:int) (numRequests:int) (numFailingNodes:int) (mailbox : Actor<_>) = 
    // Max possible number of digits in the node ID space
    // each digit is in base 4, ie the digit ranges from 0, 1, 2, 3
    let baseVal = int(ceil (log10(float(numNodes))/log10(float(4))))
    // The whole possible range of node id space is 4^maxDigits
    let nodeIDSpace = int(float(4) ** float(baseVal))
    // Random ID list contains random mapping of each index in node ID space to a random value in node ID space
    let randomIdMapping = [|0 .. nodeIDSpace-1|] |> Shuffle
    let groupOneSize = min numNodes 1024
    let groupOne = Array.copy randomIdMapping.[0..groupOneSize-1]
    let mutable numJoined = 0
    let mutable numHops = 0
    let mutable numRouted = 0
    let mutable count = 0
    printfn "baseVal = %d nodeIDSpace = %d groupOneSize = %d" baseVal nodeIDSpace groupOneSize

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
                printfn "Network has been built!!! Waiting for requests...."
                if numJoined >= numNodes then 
                    mailbox.Self <! FailNodes
                else 
                    mailbox.Self <! SecondaryJoin
            if numJoined > groupOneSize then
                if numJoined = numNodes then 
                    mailbox.Self <! FailNodes
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
                if numRouted = (numNodes - numFailingNodes)*numRequests*i/10 then
                    printfn "We have finished %d0 percent of the routing" i
            if numRouted >= (numNodes - numFailingNodes) * numRequests  && count = 0 then
                printfn "\nResults are:"
                printfn "Total Routes = %d Total Hops = %d" numRouted numHops
                let ratio = float(numHops)/float(numRouted)
                printfn "Average hops per route = %f" ratio
                count <- count + 1
                system.Stop(mailbox.Self)
                terminate <- false
        | FailNodes -> for i in 0..numFailingNodes-1 do
                            let randomRef = select ("akka://FSharp/user/master/"+ (randomIdMapping.[i] |> string)) system
                            randomRef <! NodeDie
                       system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(1000 |> float), mailbox.Self, StartRouting)
        | _-> ignore 0
        return! loop()
    }
    loop()

let pastry (numNodes:int) (numRequests:int) (numFailingNodes:int)= 
    printfn "Starting Pastry protocol for numNodes = %i numRequests = %i" numNodes numRequests
    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
    let parentRef = spawn system "master" (application numNodes numRequests numFailingNodes)
    parentRef <! Start
    while terminate do
        ignore 0
    stopWatch.Stop()
    printfn "Total time elapsed is given by %f milliseconds" stopWatch.Elapsed.TotalMilliseconds