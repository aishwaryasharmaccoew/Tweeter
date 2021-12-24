#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#r "nuget: Akka.TestKit"

open System
open System.Threading
open System.IO
open Akka.FSharp
open Akka.Actor
open System.Collections.Generic
open System.Text

// Register account
// Send tweet. Tweets can have hashtags (e.g. #COP5615isgreat) and mentions (@bestuser)
// Subscribe to user's tweets
// Re-tweets (so that your subscribers get an interesting tweet you got by other means)
// Allow querying tweets subscribed to, tweets with specific hashtags, tweets in which the user is mentioned (my mentions)
// If the user is connected, deliver the above types of tweets live (without querying)




let configuration =
    Configuration.parse
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = localhost
                port = 9007
            }
        }"

let system = ActorSystem.Create("RemoteFSharp", configuration)

let mutable username_list = List.empty: String list 


type Command =
    | ClientMsg of String
    | RegisterUser of String
    | Useraction of String
    | Subscribemsg of String
    | Tweetmsg of String
    | Retweet of String
    | Querymsg of String


//here is the class for tweet
type Tweet(name:string ,content:string,TweetID:string) = 
    let mutable Hashtag_list = List.empty: String list     // List of tags in this tweet
    let mutable Mention_list = List.empty: String list // List of mentions in this tweet
    let mutable message = ""
    let ID = TweetID
    let OwnerName = name
    let tweetcontent = content


    member this.SetMessage msg = 
        message <- msg

    member this.GetMessage() = 
        message

    member this.GetID() =
        ID

    member this.GetOwnerName ()= 
        OwnerName

    member this.GetContent() = 
        tweetcontent

    member this.set_tag taglist = 
        Hashtag_list <- List.append Hashtag_list [taglist]
    member this.set_mention menlist = 
        Mention_list <- List.append Mention_list [menlist]
    member this.get_tag =
        Hashtag_list
    member this.get_mention = 
        Mention_list


//here is the class for user
type  User(name:string,password:string) = 
    let mutable subscribes = List.empty: User list //collection of all subscribes under this user
    let mutable tweets = List.empty : Tweet list //collection of all tweets under this user
    let mutable follower = List.empty : User list // collection of all follower under this user

    member this.Name = name
    member this.Password = password

    //subscribes adder and getter. while this user subscribe other user, other user will add this user to follower
    member this.AddSubscribes user= 
        subscribes <- List.append subscribes [user]
        user.AddFollower(this)

    member this.GetAllSubscribes() = 
        subscribes

    //follower
    member this.AddFollower user = 
        follower <- List.append follower [user]

    member this.GetAllFollower() = 
        follower


    //tweet adder and getter
    member this.AddTweet tweet= 
        tweets <- List.append tweets [tweet]
        //todo push the changes to all followers


    member this.GetAllTweets()=
        tweets

let mutable totalusers = new Map<String, User>([])           // <user_name,       user_obj>
let mutable totaltweets = new Map<String, Tweet>([])         // <tweet_id,        tweet_obj>

let mutable totalhashtags = new Map<String, String list>([])     // <tag_content,     list of tweet_id>
let mutable totalmentions = new Map<String, String list>([]) // <mention_content, list of tweet_id>


//Create tweet and process tweet message
let TweetActor(mailbox: Actor<_>) =

    let rec loop()= actor{
        let! message = mailbox.Receive();
        let sender = mailbox.Sender()
        match message with
        | Tweetmsg usermessage->
                let textSplit = usermessage.Split(',')
                let length = textSplit.Length
                match length with 
                | 3 ->
                    let username = textSplit.[1]
                    let content = textSplit.[2]
                    let tweetID = totaltweets.Count.ToString()
                    //printf "tweetId : %s"tweetID
                    let tweet = new Tweet(username,content,tweetID)
                    let user = totalusers.[username]
                    user.AddTweet tweet
                    totaltweets <- totaltweets.Add(tweetID,tweet)
                    printf "%A" totaltweets

                | 4 ->
                    let username = textSplit.[1]
                    let content = textSplit.[2]
                    let tweetID = totaltweets.Count.ToString()
                    let tweet = new Tweet(username,content,tweetID)
                    let checkhashmen = textSplit.[3]
                    let char = checkhashmen.[0]
                    //printf "%c" char
                    if char.Equals('#') then 
                        let hashtagSplit = textSplit.[3].Split('#')
                        //printf "%A" hashtagSplit 
                        for i in 1 .. hashtagSplit.Length-1 do
                            tweet.set_tag(hashtagSplit.[i])
                        let mutable prevlist = List.empty: String list                       
                        let mutable curtag = ""
                        let taglist = tweet.get_tag
                        //printf "%A" taglist    
                        for i in 0..taglist.Length-1 do
                            curtag <- taglist.[i]
                            if not (totalhashtags.ContainsKey(curtag)) then
                                let mutable tmplist = List.empty: String list
                                tmplist <- [tweetID] |> List.append tmplist
                                totalhashtags <- totalhashtags.Add(curtag, tmplist)
                            else
                                prevlist <- totalhashtags.[curtag]
                                //printf "%A" prevlist
                                prevlist <- [tweetID] |> List.append prevlist
                                totalhashtags <- totalhashtags.Add(curtag, prevlist)
                        let user = totalusers.[username]
                        user.AddTweet tweet
                        totaltweets <- totaltweets.Add(tweetID,tweet)
                        printf "%A\n" totalhashtags
                    else 
                        let mentionSplit = textSplit.[3].Split('@')
                        for i in 1 .. mentionSplit.Length-1 do
                            tweet.set_mention(mentionSplit.[i]) 
                        let mutable prevlist = List.empty: String list 
                        //let mutable tmplist = List.empty: String list
                        let mutable curtag = ""
                        let menlist = tweet.get_mention      
                        for i in 0..menlist.Length-1 do
                            curtag <- menlist.[i]
                            if not (totalmentions.ContainsKey(curtag)) then
                                //tmplist <- List.empty: String list
                                let mutable tmplist = List.empty: String list
                                tmplist <- [tweetID] |> List.append tmplist
                                totalmentions <- totalmentions.Add(curtag, tmplist)
                            else
                                prevlist <- totalmentions.[curtag]
                                prevlist <- [tweetID] |> List.append prevlist
                                totalmentions <- totalmentions.Add(curtag, prevlist)
                        let user = totalusers.[username]
                        user.AddTweet tweet
                        totaltweets <- totaltweets.Add(tweetID,tweet)
                        printf "%A\n" totalmentions
                    printf "%A" totaltweets
                | 5 ->
                    let username = textSplit.[1]
                    let content = textSplit.[2]
                    //let tweetID = textSplit.[3]
                    let tweetID = totaltweets.Count.ToString()
                    let hashtagSplit = textSplit.[3].Split('#')
                    let mentionSplit = textSplit.[4].Split('@')
                    let tweet = new Tweet(username,content,tweetID)
                    for i in 1 .. hashtagSplit.Length-1 do
                        tweet.set_tag(hashtagSplit.[i])
                    for i in 1 .. mentionSplit.Length-1 do
                        tweet.set_mention(mentionSplit.[i])

                    let mutable prevlist = List.empty: String list 
                    let mutable tmplist = List.empty: String list
                    let mutable curtag = ""
                    let taglist = tweet.get_tag      
                    for i in 0..taglist.Length-1 do
                         curtag <- taglist.[i]
                         if not (totalhashtags.ContainsKey(curtag)) then
                             let mutable tmplist = List.empty: String list
                             tmplist <- [tweetID] |> List.append tmplist
                             totalhashtags <- totalhashtags.Add(curtag, tmplist)
                         else
                             prevlist <- totalhashtags.[curtag]
                             //printf "%A" prevlist
                             prevlist <- [tweetID] |> List.append prevlist
                             totalhashtags <- totalhashtags.Add(curtag, prevlist)         

                    let mutable mprevlist = List.empty: String list 
                    let mutable mtmplist = List.empty: String list
                    let mutable mcurtag = ""
                    let menlist = tweet.get_mention      
                    for i in 0..menlist.Length-1 do
                        mcurtag <- menlist.[i]
                        if not (totalmentions.ContainsKey(mcurtag)) then
                            //tmplist <- List.empty: String list
                            let mutable mtmplist = List.empty: String list
                            mtmplist <- [tweetID] |> List.append mtmplist
                            totalmentions <- totalmentions.Add(mcurtag, mtmplist)
                        else
                            mprevlist <- totalmentions.[mcurtag]
                            mprevlist <- [tweetID] |> List.append mprevlist
                            totalmentions <- totalmentions.Add(mcurtag, mprevlist)
                    let user = totalusers.[username]
                    user.AddTweet tweet

                    totaltweets <- totaltweets.Add(tweetID,tweet)
                    printf "%A\n" totaltweets
                    printf "%A\n" totalhashtags
                    printf "%A\n" totalmentions


        | _ -> printf("Unknown message")

        return! loop()
    }            
    loop()
let tweetactor = spawn system "TweetActor" TweetActor  

//Actor for the register
let RegisterActor(mailbox: Actor<_>) =

    let rec loop()= actor{
        let! message = mailbox.Receive();
        match message with
        | RegisterUser userCreds-> 
                printf("In Register Actor")
                let textSplit = userCreds.Split(',')
                let username = textSplit.[1]
                let password = textSplit.[2]
                
                if totalusers.ContainsKey(username) then
                    printfn("\n User already exists")
                else
                    let user = new User(username,password)
                    totalusers <- totalusers.Add(username,user)
                    printfn("\n User registered")
                printf "%A" totalusers


        | _ -> ()

        return! loop()
    }            
    loop()
let registeractor = spawn system "RegisterActor" RegisterActor 





//The actor to do retweet
let ReTweetActor(mailbox: Actor<_>) =

    let rec loop()= actor{
        let! message = mailbox.Receive();
        match message with
        | Retweet usermessage->
                let textSplit = usermessage.Split(',')
                let username= textSplit.[1]
                let tweetId= textSplit.[2]
                let (retweetMsg: Tweet)=totaltweets.[tweetId]
                let tweetIDnew = totaltweets.Count.ToString()
                let tweet = new Tweet(username,retweetMsg.GetContent(),tweetIDnew)
                let user = totalusers.[username]
                user.AddTweet tweet
                totaltweets <- totaltweets.Add(tweetIDnew,tweet)
                printf "%A" totaltweets

        | _ -> printf("Unknown Message")

        return! loop()
    }            
    loop()
let retweetactor = spawn system "ReTweetActor" ReTweetActor  

//The actor to handle subscribe
let SubscribeActor(mailbox: Actor<_>) =

    let rec loop()= actor{
        let! message = mailbox.Receive();
        match message with
        | Subscribemsg receivemsg->  
            let text = receivemsg.Split(',')
            let user1 = text.[1]
            let user2 = text.[2]
            if totalusers.ContainsKey(user1)&&totalusers.ContainsKey(user2) then
                let user = totalusers.[user1]
                let sub = totalusers.[user2]
                user.AddSubscribes sub
            else 
                if not(totalusers.ContainsKey(user1)) then 
                    printfn"The user does not exist"
                else 
                    printfn"The user you want to subscribe does not"

        | _ -> ()

        return! loop()
    }            
    loop()
let subscribeactor = spawn system "SubscribeActor" SubscribeActor 

//The actor to handle query
let QueryActor(mailbox: Actor<_>) =

    let rec loop()= actor{
        let! message = mailbox.Receive();
        match message with
        | Querymsg recivemsg->
            let testSplit = recivemsg.Split(',')
            let querytype = testSplit.[1]
            match querytype with 
            |"subscribed" ->
                let username = testSplit.[2]
                if not(totalusers.ContainsKey(username)) then
                    printfn"the user not found"
                else
                    let user = totalusers.[username]
                    let subscribelist = user.GetAllSubscribes() |> List.map (fun x -> x.GetAllTweets()) |> List.concat |> List.map (fun x -> x.GetContent()) |> String.concat "\n"
                    printfn"Tweets Subscribed :\n %A"subscribelist

            |"hashtag" ->
                let tag = testSplit.[2]
                if not(totalhashtags.ContainsKey(tag)) then 
                    printfn"The hashtag not found"
                else 
                    let hashtagprint = totalhashtags.[tag] |> List.map (fun x -> totaltweets.[x]) |> List.map(fun x -> x.GetContent()) |> String.concat "\n"
                    printfn"Tweets contains this tag: \n %A" hashtagprint
            |"mention" ->
                let username = testSplit.[2]
                if not(totalmentions.ContainsKey(username)) then 
                    printfn"The mention user not found"
                else 
                    let mentionprint = totalmentions.[username] |> List.map (fun x -> totaltweets.[x]) |> List.map(fun x -> x.GetContent()) |> String.concat "\n"
                    printfn"Tweets mention this username: \n %A" mentionprint


        | _ -> ()

        return! loop()
    }            
    loop()
let queryactor = spawn system "QueryActor" QueryActor  

let UserActor(mailbox: Actor<_>) =

    let rec loop()= actor{
        let! message = mailbox.Receive();
        let selection = mailbox.Context.System.ActorSelection("akka.tcp://server-system@localhost:9003/user/Boss")
        match message with
        | Querymsg recivemsg->
            let testSplit = recivemsg.Split(',')
            let username = testSplit.[1]
            if not(totalusers.ContainsKey(username)) then
                printfn"the user not found"
            else
                let user = totalusers.[username]
                let tweetlist= user.GetAllTweets() |> List.map (fun x -> string(x.GetID())+":"+ string(x.GetContent())) |> String.concat "\n"
                printfn"User Tweets :\n %A"tweetlist 
                               
                let subscribelist = user.GetAllSubscribes() |> List.map (fun x -> x.GetAllTweets()) |> List.concat |> List.map (fun x -> string(x.GetID())+":"+ string(x.GetContent())) |> String.concat "\n"
                printfn"Tweets Subscribed :\n %A"subscribelist
                let sendList="Servermsg,"+tweetlist+","+subscribelist
                selection<! sendList



        | _ -> ()

        return! loop()
    }            
    loop()
let useractor = spawn system "UserActor" UserActor 

let Monitor(mailbox: Actor<_>) =

    let rec loop()= actor{
        let! msg = mailbox.Receive();
        printf "Server is up!\n"
        printfn "%s"  msg
        let (text: String) = msg
        let textSplit = text.Split(',')
        let action = textSplit.[0]
        match action with
        |(null|"")-> printfn"No input"
        |("Register")-> 
            registeractor <! RegisterUser text
            printfn"Sent to register actor"

        |("Subscribe")->
            subscribeactor <! Subscribemsg text
            printfn"Sent to subscribe actor"
        |("Tweet")->         
            tweetactor <! Tweetmsg text
            printfn"Sent to the Tweet actor"
        |("Retweet")->         
            retweetactor <! Retweet text
            printfn"Sent to the Retweet actor"
        |("Query")-> 
            queryactor <! Querymsg text
            printfn"Sent to the query actor"
        |("Connect")-> 
            useractor <! Querymsg text
            printfn"Sent to the query actor"
        | _ -> printf "Invalid Input" 

        
(*        match msg with
        | ClientMsg receivemsg->
                printf "%s" receivemsg
                let text = receivemsg
                let textSplit = text.Split(',')
                let action = textSplit.[0]
                match action with
                |(null|"")-> printfn"No input"
                |("Register")-> printfn"Send to register actor"
                |("Subscribe")->
                    printfn"Send to subscribe actor"
                |("Tweet")-> 
                    tweetactor <! Tweetmsg text
                    printfn"Send to the Tweet actor"
                |("Retweet")-> printfn"Send to the Retweet actor"
                |("Query")-> printfn"Send to the query actor"

        | _ -> printf "Client msg recieved" *)

        return! loop()
    }            
    loop()

 
let moniter = spawn system "Monitor" Monitor


system.WhenTerminated.Wait()
