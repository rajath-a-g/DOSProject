open System
open WebSocketSharp
open Akka.Actor
open Akka.FSharp
open System.Text.Json
open System.Collections.Generic


let mutable total = 0
let system = ActorSystem.Create("FSharp")
//websocket type declarations for methods
type Response = { Operation : string ; Status : string; ReasonForError : string; Result: string}
type RegisterReq = { UserId : int64 ; Password : string ; NumTweets: int }
type LoginReq = { UserId : int64 ; Password : string }
type LogoutReq = { UserId : int64 }
type SubsReq = { UserId : int64; ToFollow: int64 }
type TweetReq = { UserId : int64; Tweet: string }
type QueryMenReq = { key : string; }
type RetweetReq = { UserId : int64; FromId : int64 }
type QueryTweetsReq = { Id : int64 ; }
          
let onRx nodeName = fun (arg:MessageEventArgs) ->
    printfn "Server Response: %s" arg.Data 
    let message = arg.Data
    let resp = JsonSerializer.Deserialize<Response>(message)
    let op = resp.Operation
    let actorRef = system.ActorSelection("akka://FSharp/user/" + nodeName)
    
    match op with
    | "Register" -> if resp.Status = "Success" then
                        printfn "Register Suceeded for %A" nodeName
                        actorRef <! "Logout"
                    else
                        printfn "Register failed with reason: %s" resp.ReasonForError
    | "Logout" -> if resp.Status = "Success" then
                       actorRef <! "Login"
                  else 
                      printfn "Logout failed with reason: %s" resp.ReasonForError
    | "Login" -> if resp.Status = "Success" then
                    actorRef <! "Tweet"
                 else 
                      printfn "Login failed with reason: %s" resp.ReasonForError
    | "Tweet" -> if resp.Status = "Success" then
                    total <- total + 10
                    if total = 10 then
                        actorRef <! "QueryMen"
                 else 
                      printfn "Tweet failed with reason: %s" resp.ReasonForError
    | "QueryMentions" -> if resp.Status = "Success" then
                            let res = JsonSerializer.Deserialize<List<string>>(resp.Result)
                            printfn "Got the queryMentions result: %A" res
                            actorRef <! "QueryHash"
                         else 
                            printfn "QueryMentions failed with reason: %s" resp.ReasonForError
    | "QueryHashTags" -> if resp.Status = "Success" then
                            let res = JsonSerializer.Deserialize<List<string>>(resp.Result)
                            printfn "Got the queryHashtags result: %A" res
                            actorRef <! "Retweet"
                         else 
                            printfn "QueryHashTags failed with reason: %s" resp.ReasonForError
    | "Retweet" -> if resp.Status = "Success" then
                        printfn "Retweet success"
                        actorRef <! "QueryTweets"
                   else
                        printfn "Retweet failed with reason: %s" resp.ReasonForError
    | "QueryTweets" -> if resp.Status = "Success" then
                            let res = JsonSerializer.Deserialize<List<string>>(resp.Result)
                            printfn "Got the query tweets result: %A" res
                       else 
                            printfn "QueryTweets Failed failed with reason: %s" resp.ReasonForError
    | "TweetLive" -> if resp.Status = "Success" then
                        let res = JsonSerializer.Deserialize<List<string>>(resp.Result)
                        printfn "Client %s tweeted %s" nodeName res.[0]
    | _ -> printfn "Wrong tag"

let error = fun (arg:ErrorEventArgs) ->
    printfn "Request failed with %A" arg.Message

let clientActorNode id toFoll (serverMailbox:Actor<string>) =
    let userId = id
    let nodeName = serverMailbox.Self.Path.Name
    //Client websockets to delegate the functions to different actors
    let  wssconnect = new WebSocket("ws://localhost:9001/Register")
    let  wssLogout = new WebSocket("ws://localhost:9001/Logout")
    let  wssLogin = new WebSocket("ws://localhost:9001/Login")
    let  wssSubscribe = new WebSocket("ws://localhost:9001/Subscribe")
    let  wssTweet = new WebSocket("ws://localhost:9001/Tweet")
    let  wssQueryMen = new WebSocket("ws://localhost:9001/QueryMentions")
    let  wssQueryHash = new WebSocket("ws://localhost:9001/QueryHashTags")
    let  wssdisconnect = new WebSocket("ws://localhost:9001/Disconnect")
    let  wssRetweet = new WebSocket("ws://localhost:9001/Retweet")
    let  wssQueryTweets = new WebSocket("ws://localhost:9001/QueryTweets")
    //Registering the callbacks for each of the sockets
    wssconnect.OnMessage.Add(onRx nodeName)
    wssconnect.OnError.Add(error)
    wssLogout.OnMessage.Add(onRx nodeName)
    wssLogout.OnError.Add(error)
    wssLogin.OnMessage.Add(onRx nodeName)
    wssLogin.OnError.Add(error)
    wssSubscribe.OnMessage.Add(onRx nodeName)
    wssSubscribe.OnError.Add(error)
    wssTweet.OnMessage.Add(onRx nodeName)
    wssTweet.OnError.Add(error)
    wssQueryMen.OnMessage.Add(onRx nodeName)
    wssQueryMen.OnError.Add(error)
    wssQueryHash.OnMessage.Add(onRx nodeName)
    wssQueryHash.OnError.Add(error)
    wssRetweet.OnMessage.Add(onRx nodeName)
    wssRetweet.OnError.Add(error)
    wssQueryTweets.OnMessage.Add(onRx nodeName)
    wssQueryTweets.OnError.Add(error)
    let rec loop() = actor {
        let! (message: string) = serverMailbox.Receive()
        match message with
        | "Register" -> wssconnect.Connect()
                        let data = { UserId = userId ; Password = "string" ; NumTweets = 10 }
                        let serialzed = JsonSerializer.Serialize<RegisterReq>(data)
                        wssconnect.Send(serialzed)
        | "Logout" ->   wssLogout.Connect()
                        let data = { UserId = userId }
                        let serialzed = JsonSerializer.Serialize<LogoutReq>(data)
                        wssLogout.Send(serialzed)
        | "Login" ->    wssLogin.Connect()
                        let data = { UserId = userId ; Password = "string" }
                        let serialzed = JsonSerializer.Serialize<LoginReq>(data)
                        wssLogin.Send(serialzed)
        | "Subscribe"-> wssSubscribe.Connect()
                        let data = { UserId = userId ; ToFollow = toFoll }
                        let serialzed = JsonSerializer.Serialize<SubsReq>(data)
                        wssSubscribe.Send(serialzed)
        | "Tweet" ->    wssTweet.Connect()
                        let data = { UserId = userId; Tweet = "@User7894 Hi, and welcome to twitter." }
                        let serialzed = JsonSerializer.Serialize<TweetReq>(data)
                        wssTweet.Send(serialzed)
                        let data = { UserId = userId; Tweet = "Great day today #GreatDay" }
                        let serialzed = JsonSerializer.Serialize<TweetReq>(data)
                        wssTweet.Send(serialzed)
                        let data = { UserId = userId; Tweet = "#DOS rocks" }
                        let serialzed = JsonSerializer.Serialize<TweetReq>(data)
                        wssTweet.Send(serialzed)
                        let data = { UserId = userId; Tweet = "#COP5615 is the subject to take." }
                        let serialzed = JsonSerializer.Serialize<TweetReq>(data)
                        wssTweet.Send(serialzed)
                        let data = { UserId = userId; Tweet = "@User7894 Stay safe #COVID" }
                        let serialzed = JsonSerializer.Serialize<TweetReq>(data)
                        wssTweet.Send(serialzed)
                        let data = { UserId = userId; Tweet = "I love tweeting" }
                        let serialzed = JsonSerializer.Serialize<TweetReq>(data)
                        wssTweet.Send(serialzed)
        | "QueryMen" -> wssQueryMen.Connect()
                        let data = { key = "@User7894" }
                        let serialzed = JsonSerializer.Serialize<QueryMenReq>(data)
                        wssQueryMen.Send(serialzed)
        | "QueryHash" -> wssQueryHash.Connect()
                         let data = { key = "#DOS" }
                         let serialzed = JsonSerializer.Serialize<QueryMenReq>(data)
                         wssQueryHash.Send(serialzed)
        | "Retweet" ->  wssRetweet.Connect()
                        let data = { UserId = userId ; FromId = toFoll }
                        let serialzed = JsonSerializer.Serialize<RetweetReq>(data)
                        wssRetweet.Send(serialzed)
        | "QueryTweets" -> wssQueryTweets.Connect()
                           let data = { Id = userId }
                           let serialzed = JsonSerializer.Serialize<QueryTweetsReq>(data)
                           wssQueryTweets.Send(serialzed)
        | "Terminate" -> wssconnect.Close()
                         wssLogin.Close()
                         wssLogout.Close()
                         wssSubscribe.Close()
                         wssTweet.Close()
                         wssQueryHash.Close() 
                         wssQueryMen.Close()
                         wssRetweet.Close()
                         wssQueryTweets.Close()
        | _ -> printfn "Wrong Message"         
        return! loop()
    }
    loop()     

[<EntryPoint>]
let main argv =
    //Simulating clients by spawning actors
    let clientActor = spawn system "Client1" (clientActorNode 12345L 7894L)
    let clientActor2 = spawn system "Client2" (clientActorNode 7894L 12345L)
    clientActor <! "Register"
    clientActor2 <! "Register"
    
    Console.ReadLine() |> ignore

    clientActor <! "Terminate"
    clientActor2 <! "Terminate"

    0 // return an integer exit code