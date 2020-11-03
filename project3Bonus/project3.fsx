#load "PastrySimulator.fsx"

let main() = 
    let numNodes = int fsi.CommandLineArgs.[1]
    let numRequests = int fsi.CommandLineArgs.[2]
    let percentageDecimalFailingNodes = double fsi.CommandLineArgs.[3]
    //Reading number of nodes and requests to invoke pastry algorithm
    // if numFailingNodes >= numNodes then
    //     failwith "More number of failing nodes than number of nodes. Exiting...."
    // else if numFailingNodes < 0 then
    //     failwith "Not a valid number of nodes (It cannot be negative). Exiting...."
    // else
    let numFailingNodes = percentageDecimalFailingNodes * (double numNodes) |> int
    printfn "numNodes=%i numRequests=%i numFailingNodes=%i" numNodes numRequests numFailingNodes 
    PastrySimulator.pastry numNodes numRequests numFailingNodes

main()