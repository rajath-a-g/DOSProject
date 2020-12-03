#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit"
#r "nuget: Akka.Remote"
#r "nuget: Akka.Serialization.Hyperion"
#load "Message.fsx"

open Message
open System
open Akka.FSharp
open System.Collections.Generic
open System.Text.RegularExpressions
open Akka.Configuration
open Akka.Serialization

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                debug : {
                    receive : on
                    autoreceive : on
                    lifecycle : on
                    event-stream : on
                    unhandled : on
                }
                serializers {
                    hyperion = ""Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion""
                }
                serialization-bindings {
                    ""System.Object"" = hyperion
                }                
            }
            remote {
                helios.tcp {
                    port = 9001
                    hostname = 192.168.0.94
                }
            }
        }")

let serversystem = System.create "TwitterServer" configuration

let mutable flag = true

let TwitterServer (mailbox: Actor<_>) =
    let mutable userIdSet = Set.empty
    let mutable passwordTable = Dictionary<int64,string>()
    let mutable numTweetsofUser = Dictionary<int64,int>()
    let mutable followingTable = Dictionary<int64,List<int64>>()
    let mutable followersTable = Dictionary<int64,List<int64>>()
    let mutable tweets = Dictionary<int64,List<string>>()
    let mutable onlineUsers = Set.empty
    let mutable tagsList = Dictionary<string,List<string>>()
    

    let tweetLive tweet userList =
        for user in userList do
            if onlineUsers.Contains(user) then
                let userRef = select ("akka.tcp://Twitter@localhost:2552/user/Client" + (string user)) serversystem
                userRef <! TweetLive(tweet)

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
        | Register(userId, password, numTweets) -> //printfn "Registering for %d" userId
                                                   // We have to populate the initial tables with the details of the user provided.
                                                   if (not (userIdSet.Contains(userId))) then
                                                       onlineUsers <- onlineUsers.Add(userId)
                                                       userIdSet <- userIdSet.Add(userId)
                                                       passwordTable.Add(userId, password)
                                                       numTweetsofUser.Add(userId, numTweets)
                                                       followersTable.Add(userId, new List<int64>())
                                                       followingTable.Add(userId, new List<int64>())
                                                       tweets.Add(userId, new List<string>())
                                                       printfn "Registration Successful with Id: %d" userId
                                                       simRef <! CreateUsersDone(userId)
                                                   else
                                                       printfn "Registration Failed. UserID: %d is already in use." userId
        | Logout(userId) -> //printfn "inside logout for %d" userId
                            if (not (userIdSet.Contains(userId))) && (not (onlineUsers.Contains(userId))) then
                                printfn "User %d is not registered or not online, failed to logout." userId
                            else
                                onlineUsers <- onlineUsers.Remove(userId)
                                let userRef = select ("akka.tcp://Twitter@localhost:2552/user/Client" + (string userId)) serversystem
                                userRef <! LogoutUser
        | Login(userId, passwd) -> //printfn "inside login for %d" userId
                                   if (not (userIdSet.Contains(userId))) then
                                        printfn "User %d is not registered, Please register and try again" userId
                                   else
                                        if (not (onlineUsers.Contains(userId))) then
                                            if (passwordTable.ContainsKey(userId) && passwordTable.[userId].Equals(passwd)) then
                                                onlineUsers <- onlineUsers.Add(userId)
                                                let userRef = select ("akka.tcp://Twitter@localhost:2552/user/Client" + (string userId)) serversystem
                                                userRef <! LoginUser
                                            else
                                                printfn "Password incorrect!"
                                        else
                                            printfn "User %d is already logged in" userId
        | AddFollower(userId, toFollowId) -> //printfn "inside add follower for %d for follower %d" userId toFollowId
                                             if (not (userIdSet.Contains(toFollowId))) && userId <> toFollowId then
                                                followersTable.[toFollowId].Add(userId)
                                                followingTable.[userId].Add(toFollowId)
                                             simRef <! SubscribeDone
        | Tweet(userId, tweet) -> printfn "inside tweet for %d" userId
                                  tweets.[userId].Add(tweet)
                                  let followersList = followersTable.[userId]
                                  tweetLive tweet followersList 
                                  let hashtagsList = new List<string>()
                                  let mentionsList = new List<string>()
                                  let hashtagsMatchCollection = Regex.Matches(tweet, "#[a-zA-Z0-9_]+")
                                  for hastagMatch in hashtagsMatchCollection do
                                    hashtagsList.Add(hastagMatch.Value)
                                  for tag in hashtagsList do
                                    if tagsList.ContainsKey(tag) then
                                        tagsList.[tag].Add(tweet)
                                    else
                                        tagsList.Add(tag, new List<string>())
                                        tagsList.[tag].Add(tweet)
                                  let mentionsMatchCollection = Regex.Matches(tweet, "@User[0-9]+")
                                  for mentionsMatch in mentionsMatchCollection do
                                    mentionsList.Add(mentionsMatch.Value)
                                  for mention in mentionsList do
                                    if tagsList.ContainsKey(mention) then
                                        tagsList.[mention].Add(tweet)
                                    else
                                        tagsList.Add(mention, new List<string>())
                                        tagsList.[mention].Add(tweet)
                                //   let mentionsUserId = new List<int64>()
                                //   for id in mentionsList do
                                //     let uid = id.[4..] |> int64
                                //     mentionsUserId.Add(uid)
                                //   let validUserIds = checkForExistence mentionsUserId
                                  //tweetLive tweet validUserIds
                                  simRef <! SendTweetsDone
        | GetAllMentions(key, userId) -> printfn "inside get for %d key %s" userId key
                                                    //printfn "%A" tagsList
                                         if tagsList.ContainsKey(key) then
                                            let tweetList = tagsList.[key]
                                            let userRef = select ("akka.tcp://Twitter@localhost:2552/user/Client" + (string userId)) serversystem
                                            //userRef <! PrintQueryResult(tweetList)
                                            simRef <! QueryWithMentionDone(userId, tweetList)  
        | GetAllHashTags(key, userId) -> printfn "inside get for %d key %s" userId key
                                                    //printfn "%A" tagsList
                                         if tagsList.ContainsKey(key) then
                                            let tweetList = tagsList.[key]
                                            let userRef = select ("akka.tcp://Twitter@localhost:2552/user/Client" + (string userId)) serversystem
                                            //userRef <! PrintQueryResult(tweetList)
                                            simRef <! QueryWithHashtagDone(userId, tweetList)                
        | GetAllFollowing(userId) -> printfn "inside getall for %d" userId
                                     if userIdSet.Contains(userId) then
                                        printfn "User %d follows: %A" userId followingTable.[userId]
        | Terminate -> flag <- false
        | _ -> printfn "Wrong Message!"
        return! loop ()
    }
    loop ()

let server = spawn serversystem "TwitterServer" (TwitterServer)
printfn "server: %A" server.Path
while flag do
    Threading.Thread.Sleep 1000