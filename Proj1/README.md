Group Members : Rajath A Ganesh [UFID : 5314-1354] , Prajwala Nagaraj [UFID : 1099-2662]

# Project 1
The goal of this project is to use F# and the actor model to build a
good solution to sum of squares problem that runs well on multi-core machines.

# Prerequisites:
* .NET SDK latest
* F# 
* Nuget

# How to run 
```
dotnet fsi --langversion:preview proj1.fsx 3 2
3
```

# Size of Work Unit:
To determine the size of the work unit we used the input ``` 10^6 and k = 4 ``` and used a range of number of units and we plotted the following graph
![Output](https://github.com/rajath-a-g/DOSProject/blob/master/Proj1/graph1.PNG)

# Result :
The result for the problem asked is given as :
![Output](https://github.com/rajath-a-g/DOSProject/blob/master/Proj1/Result.PNG)

# Running time :
On basis of input : 1000000 4 on 8 actors, the ratio(CPU/Real) we are getting is : 4.186
# Largest problem :
The largest problem we tried was 100000000 24 with output as shown below:
![Output](https://github.com/rajath-a-g/DOSProject/blob/master/Proj1/resultLarge.PNG)
