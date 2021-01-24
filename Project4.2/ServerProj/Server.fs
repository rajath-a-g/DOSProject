open System
open WebSocketSharp.Server
open Akka.Actor
open Akka.FSharp
open System.Collections.Generic
open System.Text.RegularExpressions
open System.Text.Json

let mutable ssid = ""
let system = ActorSystem.Create("FSharp")

type TwitterMessage = 
                    | Register of int64 * string * int * WebSocketSessionManager * string
                    | Logout of int64 * WebSocketSessionManager * string
                    | Login of int64 * string * WebSocketSessionManager * string
                    | AddFollower of int64 * int64 * WebSocketSessionManager * string
                    | Tweet of int64 * string * WebSocketSessionManager * string * bool
                    | GetAllFollowing of int64
                    | GetAllMentions of string * WebSocketSessionManager * string
                    | GetAllHashTags of string * WebSocketSessionManager * string
                    | TweetLive of string 
                    | Terminate
                    | RetweetNow of int64 * int64 * WebSocketSessionManager * string
                    | QueryTweetsNow of int64 * WebSocketSessionManager * string
        

//instantiating websocket server
let wssv = WebSocketServer("ws://localhost:9001")
//instantiating server instance
let serversystem = System.create "TwitterServer" (Configuration.load())

let mutable flag = true
//type messages of methods for websocket recognition
type Response = { Operation : string ; Status : string; ReasonForError : string; Result: string}
type RegisterReq = { UserId : int64 ; Password : string ; NumTweets: int }
type LoginReq = { UserId : int64 ; Password : string }
type LogoutReq = { UserId : int64 }
type SubsReq = { UserId : int64; ToFollow: int64 }
type TweetReq = { UserId : int64; Tweet: string }
type QueryMenReq = { key : string; }
type RetweetReq = { UserId : int64; FromId : int64 }
type QueryTweetsReq = { Id : int64 ; }

//Server class
let TwitterServer (mailbox: Actor<_>) =
    //data structure holding the users list and their related information
    let mutable userIdSet = Set.empty
    let mutable passwordTable = Dictionary<int64,string>()
    let mutable numTweetsofUser = Dictionary<int64,int>()
    let mutable followingTable = Dictionary<int64,List<int64>>()
    let mutable followersTable = Dictionary<int64,List<int64>>()
    let mutable tweets = Dictionary<int64,List<string>>()
    let mutable onlineUsers = Set.empty
    let mutable tagsList = Dictionary<string,List<string>>()
    
    //TweetLive for all connected users by checking online status
    let tweetLive tweet userList (x:WebSocketSessionManager) id =
        for user in userList do
            if onlineUsers.Contains(user) then
                let mutable tweetList = List<string>()
                tweetList.Add(tweet)
                let resp = { Operation = "TweetLive"; Status = "Success"; ReasonForError = ""; Result = JsonSerializer.Serialize<List<string>>(tweetList)}
                let data = JsonSerializer.Serialize(resp)
                x.SendTo(data, id)
    
    //Modular function used to determine if userIds in mentions are present in tweets
    let checkForExistence mentionedUserIds =
        let mutable existingMentions = new List<int64>()
        for id in mentionedUserIds do
            if userIdSet.Contains(id) then
                existingMentions.Add(id)
        existingMentions

    let simRef = select ("akka.tcp://Twitter@localhost:2552/user/Simulator") serversystem
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
        | Register(userId, password, numTweets, x, id) -> //Initializing all data structures for the passed user id
                                                    if (not (userIdSet.Contains(userId))) then
                                                       onlineUsers <- onlineUsers.Add(userId)
                                                       userIdSet <- userIdSet.Add(userId)
                                                       passwordTable.Add(userId, password)
                                                       numTweetsofUser.Add(userId, numTweets)
                                                       followersTable.Add(userId, new List<int64>())
                                                       followingTable.Add(userId, new List<int64>())
                                                       tweets.Add(userId, new List<string>())
                                                       let resp = { Operation = "Register"; Status = "Success"; ReasonForError = ""; Result = ""}
                                                       //Sending response in JSON format by applying JsonSerialization on data
                                                       let data = JsonSerializer.Serialize(resp)
                                                       x.SendTo(data, id)
                                                    else
                                                       printfn "Registration Failed. UserID: %d is already in use." userId
                                                       let resp = { Operation = "Register"; Status = "Failed" ; ReasonForError = "User Id already in use."; Result = ""}
                                                       let data = JsonSerializer.Serialize(resp)
                                                       x.SendTo(data, id)
        | Logout(userId, x, id) ->  if (not (userIdSet.Contains(userId))) && (not (onlineUsers.Contains(userId))) then
                                        printfn "User %d is not registered or not online, failed to logout." userId
                                    else
                                        onlineUsers <- onlineUsers.Remove(userId)
                                        let resp = { Operation = "Logout"; Status = "Success" ; ReasonForError = ""; Result = ""}
                                        let data = JsonSerializer.Serialize(resp)
                                        x.SendTo(data, id)
        | Login(userId, passwd, x,id) ->   if (not (userIdSet.Contains(userId))) then
                                                printfn "User %d is not registered, Please register and try again" userId
                                                let resp = { Operation = "Login"; Status = "Failed" ; ReasonForError = "User not registered"; Result = ""}
                                                let data = JsonSerializer.Serialize(resp)
                                                x.SendTo(data, id)
                                           else
                                                if (not (onlineUsers.Contains(userId))) then
                                                    //Username and password validation with stored tables
                                                    if (passwordTable.ContainsKey(userId) && passwordTable.[userId].Equals(passwd)) then
                                                        onlineUsers <- onlineUsers.Add(userId)
                                                        let resp = { Operation = "Login"; Status = "Success" ; ReasonForError = ""; Result = ""} 
                                                        let data = JsonSerializer.Serialize(resp)
                                                        x.SendTo(data, id)      
                                                    else
                                                        printfn "Password incorrect!"
                                                        let resp = { Operation = "Login"; Status = "Failed" ; ReasonForError = "Wrong password!!"; Result = ""}
                                                        let data = JsonSerializer.Serialize(resp)
                                                        x.SendTo(data, id)
                                                else
                                                    printfn "User %d is already logged in" userId
                                                    let resp = { Operation = "Login"; Status = "Failed" ; ReasonForError = "User already logged in."; Result = ""}
                                                    let data = JsonSerializer.Serialize(resp)
                                                    x.SendTo(data, id)
        | AddFollower(userId, toFollowId, x, id) -> printfn "Subscribe %d to %d" userId toFollowId
                                                    if (not (userIdSet.Contains(toFollowId))) && userId <> toFollowId then
                                                        //Updating users following and followed tables
                                                        followersTable.[toFollowId].Add(userId)
                                                        followingTable.[userId].Add(toFollowId)
                                                        let resp = { Operation = "Subscribe"; Status = "Success" ; ReasonForError = ""; Result = ""} 
                                                        let data = JsonSerializer.Serialize(resp)
                                                        x.SendTo(data, id)
                                                    else
                                                        let resp = { Operation = "Subscribe"; Status = "Failed" ; ReasonForError = "Users not present in Twitter."; Result = ""} 
                                                        let data = JsonSerializer.Serialize(resp)
                                                        x.SendTo(data, id) 
        | Tweet(userId, tweet, x, id, isRetweet) ->   tweets.[userId].Add(tweet)
                                                      let followersList = followersTable.[userId]
                                                      tweetLive tweet followersList x id
                                                      let hashtagsList = new List<string>()
                                                      let mentionsList = new List<string>()
                                                      //Storing the tweets with hashtags
                                                      let hashtagsMatchCollection = Regex.Matches(tweet, "#[a-zA-Z0-9_]+")
                                                      hashtagsMatchCollection |> Seq.cast |> Seq.iter(fun (i:Match) -> hashtagsList.Add(i.Value))
                                                      for tag in hashtagsList do
                                                        if tagsList.ContainsKey(tag) then
                                                            tagsList.[tag].Add(tweet)
                                                        else
                                                            tagsList.Add(tag, new List<string>())
                                                            tagsList.[tag].Add(tweet)
                                                      //Storing the tweets with user mentions
                                                      let mentionsMatchCollection = Regex.Matches(tweet, "@User[0-9]+")
                                                      mentionsMatchCollection |> Seq.cast |> Seq.iter(fun (i:Match) -> mentionsList.Add(i.Value))
                                                      for mention in mentionsList do
                                                        if tagsList.ContainsKey(mention) then
                                                            tagsList.[mention].Add(tweet)
                                                        else
                                                            tagsList.Add(mention, new List<string>())
                                                            tagsList.[mention].Add(tweet)
                                                      if not isRetweet then
                                                        let resp = { Operation = "Tweet"; Status = "Success" ; ReasonForError = ""; Result = ""} 
                                                        let data = JsonSerializer.Serialize(resp)
                                                        x.SendTo(data, id)
        | GetAllMentions(key, x, id) -> if tagsList.ContainsKey(key) then
                                            let tweetList = tagsList.[key]
                                            let resp = { Operation = "QueryMentions"; Status = "Success" ; ReasonForError = ""; Result = JsonSerializer.Serialize<List<string>>(tweetList)} 
                                            let data = JsonSerializer.Serialize(resp)
                                            x.SendTo(data, id)  
        | GetAllHashTags(key, x, id) -> if tagsList.ContainsKey(key) then
                                            let tweetList = tagsList.[key]
                                            let resp = { Operation = "QueryHashTags"; Status = "Success" ; ReasonForError = ""; Result = JsonSerializer.Serialize<List<string>>(tweetList)}
                                            let data = JsonSerializer.Serialize(resp)
                                            x.SendTo(data, id)                
        | GetAllFollowing(userId) -> if userIdSet.Contains(userId) then
                                        printfn "User %d follows: %A" userId followingTable.[userId]
        | RetweetNow(userId, fromId, x, id) -> //userId Retweets a random tweet from "fromId" passed
                                               if (not (userIdSet.Contains(userId))) && (not (userIdSet.Contains(fromId))) then
                                                    printfn "User %d is not registered or retweet user %d is not registered, Please register and try again" userId fromId
                                                    let resp = { Operation = "Retweet"; Status = "Failed" ; ReasonForError = "User not registered"; Result = ""}
                                                    let data = JsonSerializer.Serialize(resp)
                                                    x.SendTo(data, id)
                                               else
                                                    let rand = Random()
                                                    let twee = tweets.[fromId].[rand.Next(tweets.[fromId].Count)]
                                                    mailbox.Self <! Tweet(userId, twee, x, id, true)
                                                    let resp = { Operation = "Retweet"; Status = "Success" ; ReasonForError = ""; Result = ""}
                                                    let data = JsonSerializer.Serialize(resp)
                                                    x.SendTo(data, id)   
        | QueryTweetsNow(userId, x, id) -> //Query tweets of the userId passed
                                           if (not (userIdSet.Contains(userId))) then
                                                printfn "User %d is not registered , Please register and try again" userId
                                                let resp = { Operation = "QueryTweets"; Status = "Failed" ; ReasonForError = "User not registered"; Result = ""}
                                                let data = JsonSerializer.Serialize(resp)
                                                x.SendTo(data, id)   
                                           else
                                                let tweetList = tweets.[userId]
                                                let resp = { Operation = "QueryTweets"; Status = "Success" ; ReasonForError = ""; Result = JsonSerializer.Serialize<List<string>>(tweetList)} 
                                                let data = JsonSerializer.Serialize(resp)
                                                x.SendTo(data, id)                 
        | Terminate -> flag <- false
        | _ -> printfn "Wrong Message!"
        return! loop ()
    }
    loop ()

let server = spawn serversystem "TwitterServer" (TwitterServer)
//websocket receive handlers
type Connect () =
    inherit WebSocketBehavior()
    override x.OnMessage message = 
        printfn "Register request: %s \n" message.Data
        let parameters = JsonSerializer.Deserialize<RegisterReq>(message.Data)
        server <! Register (parameters.UserId, parameters.Password, parameters.NumTweets, x.Sessions, x.ID)

type LoginUs () =
    inherit WebSocketBehavior()
    override x.OnMessage message =
        printfn "Login requested through websocketID: %A" x.ID
        let parameters = JsonSerializer.Deserialize<LoginReq>(message.Data)
        server <! Login(parameters.UserId, parameters.Password, x.Sessions, x.ID)

type LogoutUs () =
    inherit WebSocketBehavior()
    override x.OnMessage message =
        printfn "Logout requested through websocketID: %A" x.ID
        let parameters = JsonSerializer.Deserialize<LogoutReq>(message.Data)
        server <! Logout(parameters.UserId, x.Sessions, x.ID)

type Subs () =
    inherit WebSocketBehavior()
    override x.OnMessage message =
        printfn "Subscribe requested through websocketID: %A" x.ID
        printfn "message in sub:%A" message.Data
        let parameters = JsonSerializer.Deserialize<SubsReq>(message.Data)
        server <! AddFollower(parameters.UserId, parameters.ToFollow, x.Sessions, x.ID)

type Twet () =
    inherit WebSocketBehavior()
    override x.OnMessage message =
        printfn "Tweet requested through websocketID: %A" x.ID
        let parameters = JsonSerializer.Deserialize<TweetReq>(message.Data)
        server <! Tweet(parameters.UserId, parameters.Tweet, x.Sessions, x.ID, false)

type QueryMention () =
    inherit WebSocketBehavior()
    override x.OnMessage message =
        printfn "QueryMentionsTweet requested through websocketID: %A" x.ID
        let parameters = JsonSerializer.Deserialize<QueryMenReq>(message.Data)
        server <! GetAllMentions(parameters.key, x.Sessions, x.ID)

type QueryHasH () =
    inherit WebSocketBehavior()
    override x.OnMessage message =
        printfn "QueryHashtagTweets requested through websocketID: %A" x.ID
        let parameters = JsonSerializer.Deserialize<QueryMenReq>(message.Data)
        server <! GetAllHashTags(parameters.key, x.Sessions, x.ID)

type Disconnect () =
    inherit WebSocketBehavior()
    override x.OnMessage message = 
        printfn "Server rx: [Disconnect] %s" message.Data 
        printfn "ID: %A" x.ID
        printfn "Session: %A" x.Sessions.IDs
        x.Send (message.Data + " [from server]")
        server <! Terminate

type Retweet () =
    inherit WebSocketBehavior()
    override x.OnMessage message =
        printfn "Retweet requested through websocketID: %A" x.ID
        let parameters = JsonSerializer.Deserialize<RetweetReq>(message.Data)
        server <! RetweetNow(parameters.UserId, parameters.FromId, x.Sessions, x.ID)

type QueryTweets () =
    inherit WebSocketBehavior()
    override x.OnMessage message =
        printfn "Query tweets requested through websocketID: %A" x.ID
        let parameters = JsonSerializer.Deserialize<QueryTweetsReq>(message.Data)
        server <! QueryTweetsNow(parameters.Id, x.Sessions, x.ID)


[<EntryPoint>]
let main argv =

    wssv.AddWebSocketService<Connect> ("/Register")
    wssv.AddWebSocketService<LoginUs> ("/Login")
    wssv.AddWebSocketService<LogoutUs> ("/Logout")
    wssv.AddWebSocketService<Subs> ("/Subscribe")
    wssv.AddWebSocketService<Twet> ("/Tweet")
    wssv.AddWebSocketService<QueryMention> ("/QueryMentions")
    wssv.AddWebSocketService<QueryHasH> ("/QueryHashTags")
    wssv.AddWebSocketService<Disconnect> ("/Disconnect")
    wssv.AddWebSocketService<Retweet> ("/Retweet")
    wssv.AddWebSocketService<QueryTweets> ("/QueryTweets")
    wssv.Start ()
    printfn "-----------------------------------------------"
    printfn "Twitter Server started...."
    printfn "-----------------------------------------------"
    Console.ReadLine() |> ignore

    wssv.Stop ()



    0 // return an integer exit code