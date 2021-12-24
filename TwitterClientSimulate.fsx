#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#r "nuget: Akka.TestKit"
open Akka.FSharp
open Akka.Remote
open Akka.Actor
open System
open System.Threading
open System.Security.Cryptography
open System.Text
open Akka.Configuration

let config =
    Configuration.parse
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = localhost
                port = 9003
            }
        }"

let system = System.create "server-system" config

type Command =
    | ClientMsg of String
    | RegisterUser of String
    | Useraction of String
    | Subscribemsg of String
    | Tweetmsg of String
    | Retweet of String
    | Querymsg of String

//boss

let Client (mailbox: Actor<_> ) =
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        let (text: String) = msg
        let textmsg=text.Split(',')
        let selection = mailbox.Context.System.ActorSelection("akka.tcp://RemoteFSharp@localhost:9007/user/Monitor")
        
        let sendm = "Register,"+msg+",pqr"
        printf "Msg of %s Sent to server\n"msg
        selection<! sendm
        //System.Threading.Thread.Sleep(10)
        let sendmx = "Subscribe,node,"+msg
        selection<! sendmx

    }
    loop()
let Boss (mailbox: Actor<_> ) =
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        let (text: String) = msg
        let textmsg=text.Split(',')
        let selection = mailbox.Context.System.ActorSelection("akka.tcp://RemoteFSharp@localhost:9007/user/Monitor")
        match textmsg.[0] with
        | ("Clientmsg")-> 
            let mutable sendm="Register,node,node1"
            selection<! sendm
            System.Threading.Thread.Sleep(300)
            let numActors = textmsg.[1] |> int
            let clients = [for i in 1..numActors do yield (spawn system ("worker" + (string i) ) Client)]
            for i=0 to (clients.Length-1) do
                clients.Item(i|>int )<! "node"+string(i)
            System.Threading.Thread.Sleep(300)
            sendm="Tweet,node,node1"
            selection<! sendm
           
        | ("Servermsg") ->
            printfn"User Tweets :\n %A"textmsg.[1] 
            printfn"Tweets Subscribed :\n %A"textmsg.[2]
            

        | _ ->  printfn "Finish" 
        return! loop()
    }
    loop()


let boss = spawn system "Boss" Boss
printf("Client is up!\n")
let mutable flag=1

//while flag=1 do
printf "Enter number of actors to Spwan :\n"
let (mymsg: string) =  System.Console.ReadLine()
let command= "Clientmsg,"+mymsg
boss<! command
System.Threading.Thread.Sleep(90000)
(*    printf "Do You wish to continue (Yes/No):"
    let mymsg =  System.Console.ReadLine()
    let (text: String) = mymsg
    match text with
    |(null|"")-> 
        printfn"No input"
    |("Yes")-> 
        flag<-1
               //System.Threading.Thread.Sleep(100)
               //system.WhenTerminated.Wait()
    |("No")-> 
        flag<-0
              
    | _ -> printf "Invalid Input"  *)
    
//system.Dispose






//boss<! "Start,1,#abc,@aish"
