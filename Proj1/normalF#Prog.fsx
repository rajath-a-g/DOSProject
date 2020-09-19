#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#time "on"

open Akka.FSharp
open System

let perfectSquare n =
    let h = n &&& 0xF
    if (h > 9) then false
    else
        if ( h <> 2 && h <> 3 && h <> 5 && h <> 6 && h <> 7 && h <> 8 ) then
            let t = ((n |> double |> sqrt) + 0.5) |> floor|> int
            t*t = n
        else false

let sumOfSquares x y = [x..y] |> List.map (fun x->x*x) |> List.sum

let GotInput n k = for i in 1..n do
                    let e = i+k-1
                    let val1 = sumOfSquares i e
                    printfn " sumOfSquares: %i " val1 
                    //check for perfect square
                    if perfectSquare val1 then
                        printfn "Perfect Sqrs: %i " i


let i1 = fsi.CommandLineArgs.[1] |> int
let i2 = fsi.CommandLineArgs.[2] |> int
GotInput  i1 i2

System.Console.ReadLine() |> ignore