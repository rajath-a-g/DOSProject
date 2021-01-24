-----------
How to Run
-----------

Installation instructions:

We assume that VSCode and Ionide F# extension are installed before you start with these instructions.

1) Download the .zip and extract it.

2) Open the server and client folders in different VSCode windows.

3) Open a new terminal in server folder and run the following commands:
	dotnet build
	dotnet run

4) Repeat step 3 in client folder.
	
----------------------------------------
1) Team members:
1.	Prajwala Nagaraj -  1099-2662
2.	Rajath Ganesh – 5314 -1354
----------------------------------------

----------------------------------------
2) Implementation details:

- We have used the WebSocketSharp package,which is a prerelease version of a wrapper on the System.Net.WebSockets to implement a websocket
  interface on the server side and the client creates a seperate websocket for each of the operations(Register,Login,Logout,..).

- We have used the duplex architecture of websockets to both push and receive data on the client side.

- The Akka actor framework is used to orchestrate the whole twitter engine. The server actor has been modified to use websockets.
  The client actor now sends all its messages on the websocket interface.

- The messages sent and received are serialized into JSON format using System.Text.Json.

- The errors are handled at two levels- 1) websocket errors - the websocket interface provides the OnError handler, which we 
					display to the clients.
					2) twitter engine errors - this is included as a status in the response object as "Success"
					or "Failed" for all requests.

- The websocket interface can be used with other frameworks, which support websockets like python, javascript,etc.
---------------------------------------
---------------------------------------
3) Youtube Demo Link:

Link for demo:   https://youtu.be/RLi4ANJ_Cv4

---------------------------------------
---------------------------------------
4) Environment Used:

- Akka - 1.4.12
- Akka.FSharp - 1.4.12
- System.Text.Json - 5.0.0
- WebSocketSharp - 1.0.3-rc11

---------------------------------------
5) Output of a sample Run:

Server Output:

dotnet run
-----------------------------------------------
Twitter Server started....
-----------------------------------------------
Register request: Register request: {"UserId":7894,"Password":"string","NumTweets":10} 

{"UserId":12345,"Password":"string","NumTweets":10}

Logout requested through websocketID: "89c07b95146b4737943348d8e676ca9f"
Logout requested through websocketID: "c3e19b62efb04f23b1498322092f5ec9"
Login requested through websocketID: "8ab37ac1880e446bb97d6da6540e894d"
Login requested through websocketID: "f3cbc1acc1e4481aaaa65b312c467adc"
Tweet requested through websocketID: "6ec2876c545f40838d2d7a0f3cb27a14"
Tweet requested through websocketID: "6ec2876c545f40838d2d7a0f3cb27a14"
Tweet requested through websocketID: "6ec2876c545f40838d2d7a0f3cb27a14"
Tweet requested through websocketID: "6ec2876c545f40838d2d7a0f3cb27a14"
Tweet requested through websocketID: "317c10d10cb64366af3598859ec6188c"
Tweet requested through websocketID: "317c10d10cb64366af3598859ec6188c"
Tweet requested through websocketID: Tweet requested through websocketID: "6ec2876c545f40838d2d7a0f3cb27a14"
Tweet requested through websocketID: "6ec2876c545f40838d2d7a0f3cb27a14"
"317c10d10cb64366af3598859ec6188c"
Tweet requested through websocketID: "317c10d10cb64366af3598859ec6188c"
Tweet requested through websocketID: "317c10d10cb64366af3598859ec6188c"
Tweet requested through websocketID: "317c10d10cb64366af3598859ec6188c"
QueryMentionsTweet requested through websocketID: "1c4d4145016a40dcbd3b4321d0347433"
QueryHashtagTweets requested through websocketID: "9f8cc2a45b694079b1c68a4a0cf3b489"
Retweet requested through websocketID: "2da1a16c06304d2eb3553f22e77e684f"
Query tweets requested through websocketID: "16af026e21bf4808b912b85496dfbbda"


Client Output:

dotnet run
Server Response: {"Operation":"Register","Status":"Success","ReasonForError":"","Result":""}Server Response: {"Operation":"Register","Status":"Success","ReasonForError":"","Result":""}

Register Suceeded for Register Suceeded for "Client2"
"Client1"
Server Response: {"Operation":"Logout","Status":"Success","ReasonForError":"","Result":""}
Server Response: {"Operation":"Logout","Status":"Success","ReasonForError":"","Result":""}
Server Response: {"Operation":"Login","Status":"Success","ReasonForError":"","Result":""}
Server Response: {"Operation":"Login","Status":"Success","ReasonForError":"","Result":""}
Server Response: {"Operation":"Tweet","Status":"Success","ReasonForError":"","Result":""}
Server Response: {"Operation":"Tweet","Status":"Success","ReasonForError":"","Result":""}
Server Response: {"Operation":"Tweet","Status":"Success","ReasonForError":"","Result":""}
Server Response: {"Operation":"Tweet","Status":"Success","ReasonForError":"","Result":""}
Server Response: {"Operation":"Tweet","Status":"Success","ReasonForError":"","Result":""}
Server Response: {"Operation":"Tweet","Status":"Success","ReasonForError":"","Result":""}
Server Response: Server Response: {"Operation":"Tweet","Status":"Success","ReasonForError":"","Result":""}
{"Operation":"Tweet","Status":"Success","ReasonForError":"","Result":""}
Server Response: {"Operation":"Tweet","Status":"Success","ReasonForError":"","Result":""}
Server Response: {"Operation":"Tweet","Status":"Success","ReasonForError":"","Result":""}
Server Response: {"Operation":"Tweet","Status":"Success","ReasonForError":"","Result":""}
Server Response: {"Operation":"Tweet","Status":"Success","ReasonForError":"","Result":""}
Server Response: {"Operation":"QueryMentions","Status":"Success","ReasonForError":"","Result":"[\u0022@User7894 Hi, and welcome to twitter.\u0022,\u0022@User7894 Hi, and welcome to twitter.\u0022,\u0022@User7894 Stay safe #COVID\u0022,\u0022@User7894 Stay safe #COVID\u0022]"}
Got the queryMentions result: seq
  ["@User7894 Hi, and welcome to twitter.";
   "@User7894 Hi, and welcome to twitter."; "@User7894 Stay safe #COVID";
   "@User7894 Stay safe #COVID"]
Server Response: {"Operation":"QueryHashTags","Status":"Success","ReasonForError":"","Result":"[\u0022#DOS rocks\u0022,\u0022#DOS rocks\u0022]"}
Got the queryHashtags result: seq ["#DOS rocks"; "#DOS rocks"]
Server Response: {"Operation":"Retweet","Status":"Success","ReasonForError":"","Result":""}
Retweet success
Server Response: {"Operation":"QueryTweets","Status":"Success","ReasonForError":"","Result":"[\u0022@User7894 Hi, and welcome to twitter.\u0022,\u0022Great day today #GreatDay\u0022,\u0022#DOS rocks\u0022,\u0022#COP5615 is the subject to take.\u0022,\u0022@User7894 Stay safe #COVID\u0022,\u0022I love tweeting\u0022,\u0022Great day today #GreatDay\u0022]"}
Got the query tweets result: seq
  ["@User7894 Hi, and welcome to twitter."; "Great day today #GreatDay";
   "#DOS rocks"; "#COP5615 is the subject to take."; ...]
