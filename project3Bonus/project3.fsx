#load "PastrySimulator.fsx"

let main() = 
    let numNodes = int fsi.CommandLineArgs.[1]
    let numRequests = int fsi.CommandLineArgs.[2]
    let percentageDecimalFailingNodes = double fsi.CommandLineArgs.[3]
    //Reading number of nodes and requests to invoke pastry algorithm
    //Also reading the percantage decimal of failing nodes to simulate the alogorithm with failures.
    //If the numNodes is 100 and percentageDecimalFailingNodes is 0.1, it implies we fail (0.1*100 =>) 10 nodes in system.
    let numFailingNodes = percentageDecimalFailingNodes * (double numNodes) |> int
    PastrySimulator.pastry numNodes numRequests numFailingNodes

main()
