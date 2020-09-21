#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#time "on"

open Akka.FSharp
open System

let perfectSquare n =
    let h = n &&& (bigint 0xF)
    if (h > 9I) then false
    else
        if ( h <> 2I && h <> 3I && h <> 5I && h <> 6I && h <> 7I && h <> 8I ) then
            let t = ((n |> double |> sqrt) + 0.5) |> floor|> bigint
            t*t = n
        else false

let sumOfSquares x y = [x..y] |> List.map (fun x->x*x) |> List.sum

let GotInput n k = for i in 1I..n do
                    let e = i+k-1I
                    let val1 = sumOfSquares i e
                    //printfn " sumOfSquares: %i " val1 
                    //check for perfect square
                    if perfectSquare val1 then
                        printfn "Perfect Sqrs: %A " i


let i1 = fsi.CommandLineArgs.[1] |> int |> bigint
let i2 = fsi.CommandLineArgs.[2] |> int |> bigint
GotInput  i1 i2

System.Console.ReadLine() |> ignore