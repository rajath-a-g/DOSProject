#load "PastrySimulator.fsx"

let main() = 
    let numNodes = int fsi.CommandLineArgs.[1]
    let numRequests = int fsi.CommandLineArgs.[2]
    printfn "numNodes=%i numRequests=%i" numNodes numRequests
    //Reading number of nodes and requests to invoke pastry algorithm
    PastrySimulator.pastry numNodes numRequests

main()