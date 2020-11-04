----------------------
Command Line Arguments
----------------------

Example Usage: dotnet fsi --langversion:preview project3.fsx <number of nodes> <number of requests> <percentage decimal of failing nodes>

----------------------------------------
1) Team members:
1.	Prajwala Nagaraj -  1099-2662
2.	Rajath Ganesh – 5314 -1354
----------------------------------------

----------------------------------------
2) What is working?

- The project follows the algorithm described in the paper implementing the pastry protocol with failure nodes induced. The number of failure nodes is input from user.
  If the numNodes is 100 and percentageDecimalFailingNodes is 0.1, it implies we fail (0.1*100 =>) 10 nodes in system.
- We have maintained 3 sets through which routing is done - 
							1) largerLeaf set
							2) smallerLeaf set 
							3) Routing table
- The Failure is induced after the network is built by randomly selecting the mentioned number of nodes to kill. The killed node
  is removed from all three structures - smallerLeaf set,largerLeaf set and routing table.
- The routing successfully reaches destination even with failure nodes and the average number of hops seems to be almost same or even less as the 
  percentage decimal of failing nodes is near and above 0.5.
- On routing to destination via failure node, we pick the nearest node to the failure node and route the request to that node. 
  This is with the assumption that if the request has reached the current node, then it will have the same number of prefix matching with the nearest node to the failure node. 
- We also print the killed node IDs and time elapsed for all requests to reach the destination.
----------------------------------------

----------------------------------------
3) What is the largest network you managed to deal with?

- The largest network we managed to deal is 1024 number of nodes with 100 number of requests.

Results :
Pastry Simulation with Failure:

NumberOfNodes	NumberOfRequests	PercentageDecimalFailure	AverageNumberOfHops
1024		100			0.1				7.237755
1024		100			0.2				5.979256
1024		100			0.3				4.508131
1024		100			0.4				2.948943
1024		100			0.5				1.971953
1024		100			0.6				2.207707
1024		100			0.7				1.006136
1024		100			0.8				0.62161



- The program support more number of nodes(upto 10^6) and requests ,but takes a lot of time to return.
----------------------------------------


How to run :

Go to project3 folder and run the dotnet command passing the above mentioned Example Usage with required inputs.

