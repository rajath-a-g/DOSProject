Group Members : Rajath A Ganesh [UFID : 5314-1354] , Prajwala Nagaraj [UFID : 1099-2662]

# Project 1
The goal of this project is to use F# and the actor model to build a
good solution to sum of squares problem that runs well on multi-core machines.

## Prerequisites:
* .NET SDK latest
* F# 
* Nuget

## How to run 
```
dotnet fsi --langversion:preview proj1.fsx 3 2
3
```

## Size of Work Unit:
To determine the size of the work unit we used the input ``` 10^6 and k = 4 ``` and used a range of number of units and we plotted the following graph

![Graph](https://github.com/rajath-a-g/DOSProject/blob/master/Proj1/graph1.png)

## Result :
The result for the problem asked is given as :

![Output](https://github.com/rajath-a-g/DOSProject/blob/master/Proj1/Result.PNG)

## Running time :
On basis of input : 1000000 4 on 8 actors, the ratio(CPU/Real) we are getting is : 4.186
## Largest problem :
The largest problem we tried was 100000000 24 with output as shown below:

![Large](https://github.com/rajath-a-g/DOSProject/blob/master/Proj1/resultLarge.PNG)

## Other Approaches tried
We also tried another approach where we calculated the squares and then summed them up using an actor and all ranges were given to different worked actors. The following were the heirarchy of actors:
* Master - Splits into a sliding window range.
* Processor - Spawns actors to calculate the squares of each number and then sums it up and checks if it is a perfect square and returns back to the master
* Worker - Replies back with the square of the given number.
This approach took a lot of time complete. The time taken was 10 times more than the time in the approach done above. The file ``` Proj1WithoutFormula.fsx ``` has the code for the following approach and takes the same inputs.

## Sequential Program for Testing 
We created a sequential program(without actors) to calculate the same problem sequentially in F# to help us test our program. The file is called ``` normalF#Prog.fsx```
