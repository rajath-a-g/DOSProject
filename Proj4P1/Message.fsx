open System.Collections.Generic


type TwitterMessage = 
                    | Register of int64 * string * int
                    | Logout of int64
                    | Login of int64 * string
                    | AddFollower of int64 * int64
                    | Tweet of int64 * string
                    | GetAllFollowing of int64
                    | GetAllMentions of string * int64
                    | GetAllHashTags of string * int64
                    | LogoutUser 
                    | LoginUser
                    | TweetLive of string
                    | PrintQueryResult of List<string>
                    | Terminate
                    | Subscribe of int64
                    | SendTweet of string
                    | QuerySubscribed of int
                    | QueryTweetWithHashTag of string
                    | QueryTweetWithMention of string
                    | ReTweetSim of int
                    | SendRetweet of string
                    | CreateUsersDone of int64
                    | SubscribeDone
                    | SendTweetsDone
                    | SendWithHashTagDone
                    | SendWithMentioDone
                    | QueryFirstNDone
                    | QueryWithMentionDone of int64 * List<string>
                    | QueryWithHashtagDone of int64 * List<string>
                    | RetweetDone
                    | StartSim
                    | StartLogout
                    | LogoutDone 
                    | StartLogin
                    | LoginDone
                    | StartSubscribe
                    | StartTweeting
                    | StartQueryFirstN
                    | QueryResult of List<string> * int64
                    | StartQueryByMention
                    | StartQueryByHashTag
                    | StartReTweetSim
                    | SendRetweetDone
                    