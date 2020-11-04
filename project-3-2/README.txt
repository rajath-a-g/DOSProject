----------------------
Command Line Arguments
----------------------

Example Usage: dotnet fsi --langversion:preview project3.fsx <number of nodes> <number of requests>

----------------------------------------
1) Team members:
1.	Prajwala Nagaraj -  1099-2662
2.	Rajath Ganesh – 5314 -1354
----------------------------------------

----------------------------------------
2) What is working?

- The project follows the algorithm described in the paper ,implementing the pastry protocol.
- We have maintained 3 sets through which routing is done - 
							1) largerLeaf set
							2) smallerLeaf set 
							3) Routing table
- The output is the average number of hops.
- We also print the time elapsed for all requests to reach the destination.
----------------------------------------

----------------------------------------
3) What is the largest network you managed to deal with?

- The largest network we managed to deal is 10^6 number of nodes with 100 number of requests.

Pastry protocol for numNodes = 1000000 numRequests = 100
Output:
Total Routes = 100000000 Total Hops = 944105822
Average hops per route = 9.441058
Total time elapsed is given by 7974905.345100 milliseconds	

Pastry Nodes	Num Requests	Avg. No. Hops
32		10		1.725000
64		10		2.059375
128		10		2.429688
512		10		3.130664
1024		10		3.540820
10240		10		5.341865
10240		100		5.280102
1000000		100		9.441058


- The program support more number of nodes and requests ,but takes a lot of time to return.
----------------------------------------


How to run :

Go to project3 folder and run the dotnet command passing the above mentioned Example Usage with required inputs.


