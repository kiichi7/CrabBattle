using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lidgren.Network;

enum PacketTypes
{
    Beat,
    AssignId,
    Ready,
    UpdateName,
    PlayerAttemptedStart,
    PlayIntro,
    StartGame,
    AddPlayer,
    RemovePlayer,
    PlayerAction,
    PlayerSpecial,
    HurtTarget,
    EnemyHealth,
    SelfHit,
    PlayerHit,
    EnemyPhaseChange,
    EnemyAction,
    EnemyTargetPosition,
    EnemyStartTargeting,
    EnemyEndTargeting,
    Message,
    SettingsChange,
    PlayerCount,
    Disconnect,
    LobbyMessage,
    EnemySync
}

enum GameState
{
    Lobby,
    Intro,
    InGame
}

class PlayerObject
{
    public NetConnection Connection;
    public int Id;
    public float X;
    public float Y;
    public float VelocityX;
    public float VelocityY;
    public bool Firing;
    public bool Ready;
    public string Name;
    public int LastBeat;

    public int dmgnormal = 0;
    public int dmgweakpoint = 0;
    public int hitstaken = 0;

    public PlayerObject(int id, NetConnection connection, string name, int curBeat)
    {
        Connection = connection;
        Id = id;
        X = 0;
        Y = 0;
        VelocityX = 0;
        VelocityY = 0;
        Ready = false;
        Name = name;
        LastBeat = curBeat;
    }
}

namespace CrabBattleServer
{
    class Program
    {
        private static NetServer server;
        private static NetPeerConfiguration config;
        public static List<PlayerObject> players;

        public static int healthMod;

        static public void CrabAction(int actionId, float speed)
        {
            Console.WriteLine("The crab is performing action " + Enum.GetName(typeof(CrabActions), actionId) + " with a modifier of "+speed+"x.");

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)PacketTypes.EnemyAction);
            outmsg.Write((Int16)actionId);
            outmsg.Write(speed);
            outmsg.Write((Int16)0f); //seed
            server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);

        }

        static public void SendLobbyMessage(string username, string message)
        {
            string msg = username + ": " + message;

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)PacketTypes.LobbyMessage);
            outmsg.Write(msg);
            server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);
        }

        static void Main(string[] args)
        {
            //Setup the policy server first.

            const string AllPolicy =
@"<?xml version='1.0'?>
<cross-domain-policy>
        <allow-access-from domain=""*"" to-ports=""*"" />
</cross-domain-policy>";

            SocketPolicyServer policyServer = new SocketPolicyServer(AllPolicy);
            int ret = policyServer.Start();
            if (ret != 0)
                Console.WriteLine("Failed to start policy server.");
            else
                Console.WriteLine("Policy service started.");

            //Alright, now onto the regular server.
            config = new NetPeerConfiguration("crab_battle");
            config.Port = 14248;
            config.MaximumConnections = 10;
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);

            server = new NetServer(config);
            server.Start();
            
            Console.WriteLine("Crab Battle server open for business.");

            NetIncomingMessage inc;
            
            DateTime time = DateTime.Now;
            DateTime lastBeat = DateTime.Now;
            DateTime introtime = DateTime.Now;

            TimeSpan timetopass = new TimeSpan(0,0,0,0,50);
            TimeSpan beatrate = new TimeSpan(0, 0, 1);

            TimeSpan introlength;

            bool skipintro = false;

            if(skipintro == true)
                introlength = new TimeSpan(0, 0, 3);
            else
                introlength = new TimeSpan(0, 0, 21);

            int beatnum = 0;

            players = new List<PlayerObject>();

            int playercount = 0;

            NetOutgoingMessage outmsg;

            int gamePhase = 0;

            int gameDifficulty = 1;
            healthMod = 1;

            CrabBehavior crab = new CrabBehavior();

            while(true)
            {
                //The gamestate stuff comes first because we drop a continue if no packet is recieved.
                
                if (gamePhase != (int)GameState.Lobby && players.Count == 0)
                {
                    Console.WriteLine("All players disconnected, returning to lobby gamestate.");
                    gamePhase = (int)GameState.Lobby;
                }

                if (gamePhase == (int)GameState.Intro && ((introtime + introlength) < DateTime.Now))
                {
                    //Intro has ran for it's length, lets start the game proper.
                    outmsg = server.CreateMessage();
                    outmsg.Write((byte)PacketTypes.StartGame);
                    server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);

                    crab.Direction = false;

                    gamePhase = (int)GameState.InGame;
                }
                
                
                if(gamePhase == (int)GameState.InGame)
                {
                    crab.GoGoBattleCrab();
                }

                //Handle dropping players.

                if ((time + timetopass) < DateTime.Now)
                {
                    for (int i = 0; i < players.Count; i++)
                    {
                        PlayerObject player = players[i];

                        //Make sure everyone is connected, and if not drop them.
                        if (player.Connection.Status == NetConnectionStatus.Disconnected || player.Connection.Status == NetConnectionStatus.Disconnecting)
                        {
                            Console.WriteLine(player.Name + " has disconnected, removing player object.");
                            SendLobbyMessage("Server", player.Name + " has disconnected.");

                            outmsg = server.CreateMessage();
                            outmsg.Write((byte)PacketTypes.RemovePlayer);
                            outmsg.Write((Int16)player.Id);
                            server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);

                            players.Remove(player);

                            outmsg = server.CreateMessage();
                            outmsg.Write((byte)PacketTypes.PlayerCount);
                            outmsg.Write((Int16)players.Count);
                            server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);
                        }
                        else
                        {
                            if (player.LastBeat + 5 < beatnum)
                            {
                                player.Connection.Disconnect("Timeout");
                                Console.WriteLine(player.Name + " has not responded in over 10 seconds.  Closing connection.");
                            }
                        }
                    }

                    time = DateTime.Now;
                }

                //Send update beats.

                if ((lastBeat + beatrate) < DateTime.Now)
                {
                    //Send a beat to all users.  Because the server doesn't really know if they disconnected unless we are sending packets to them.
                    //Beats go out every 2 seconds.
                    foreach (PlayerObject p in players)
                    {
                        outmsg = server.CreateMessage();
                        outmsg.Write((byte)PacketTypes.Beat);
                        outmsg.Write((Int16)beatnum);
                        outmsg.Write(p.Connection.AverageRoundtripTime/2f);
                        server.SendMessage(outmsg, p.Connection, NetDeliveryMethod.ReliableOrdered, 1);
                    }
                    beatnum++;

                    lastBeat = DateTime.Now;
                }

                // IT'S PACKET TIME

                if ((inc = server.ReadMessage()) == null) continue;
                
                switch(inc.MessageType)
                {
                    case NetIncomingMessageType.ConnectionApproval:
                        //A new connection! If the game is not currently ongoing, we'll accept this connection.

                        Console.WriteLine("Incoming login request. " + inc.SenderConnection.ToString());

                        if (gamePhase > 0)
                        {
                            inc.SenderConnection.Deny("The server is already running a game.");
                            break;
                        }

                        Console.WriteLine("Assigning new player the name of Player " + (playercount+1) + ".");

                        inc.SenderConnection.Approve();
                            
                        players.Add(new PlayerObject(playercount+1, inc.SenderConnection, "Player "+(playercount+1), beatnum));

                        SendLobbyMessage("Server", "Player " + (playercount + 1) + " has connected.  Connected players: " + players.Count);

                        outmsg = server.CreateMessage();
                        outmsg.Write((byte)PacketTypes.Message);
                        outmsg.Write("You are now connected to CrabBattle Server.");
                        server.SendMessage(outmsg, inc.SenderConnection, NetDeliveryMethod.ReliableOrdered, 1);

                        outmsg = server.CreateMessage();
                        outmsg.Write((byte)PacketTypes.AssignId);
                        outmsg.Write((Int32)(playercount + 1));
                        server.SendMessage(outmsg, inc.SenderConnection, NetDeliveryMethod.ReliableOrdered, 1);

                        //Send the current playercount to the client.
                        outmsg = server.CreateMessage();
                        outmsg.Write((byte)PacketTypes.PlayerCount);
                        outmsg.Write((Int16)players.Count);
                        server.SendMessage(outmsg, inc.SenderConnection, NetDeliveryMethod.ReliableOrdered, 1);

                        outmsg = server.CreateMessage();
                        outmsg.Write((byte)PacketTypes.PlayerCount);
                        outmsg.Write((Int16)players.Count);
                        server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);

                        //Send difficulty settings to the new player
                        
                        Console.WriteLine(gameDifficulty + " " + healthMod);

                        outmsg = server.CreateMessage();
                        outmsg.Write((byte)PacketTypes.SettingsChange);
                        outmsg.Write((Int16)gameDifficulty);
                        outmsg.Write((Int16)healthMod);
                        server.SendMessage(outmsg, inc.SenderConnection, NetDeliveryMethod.ReliableOrdered, 1);

                        playercount++;

                        break;

                    case NetIncomingMessageType.Data:

                        PlayerObject player = players.Find(p => p.Connection == inc.SenderConnection);
                        if (player == null)
                            break; //Don't accept data from connections that don't have a player attached.

                        switch ((PacketTypes)inc.ReadByte())
                        {
                            case PacketTypes.LobbyMessage:
                                {
                                    string msg = inc.ReadString();
                                    SendLobbyMessage(player.Name, msg);
                                    Console.WriteLine(player.Name + ": " + msg);
                                }
                                break;
                            case PacketTypes.SettingsChange:
                                {
                                    //Difficulty or health mod changed, broadcast changes to all clients.
                                    gameDifficulty = inc.ReadInt16();
                                    healthMod = inc.ReadInt16();

                                    Console.WriteLine(player.Name + " changed difficulty/healthmod to " + gameDifficulty + "/" + healthMod + ".");

                                    outmsg = server.CreateMessage();
                                    outmsg.Write((byte)PacketTypes.SettingsChange);
                                    outmsg.Write((Int16)gameDifficulty);
                                    outmsg.Write((Int16)healthMod);
                                    server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);
                                }
                                break;
                            case PacketTypes.Beat:
                                {
                                    player.LastBeat = inc.ReadInt16();
                                    player.X = inc.ReadFloat();
                                    player.Y = inc.ReadFloat();

                                    if (crab.CurrentTarget == player.Id)
                                    {
                                        float CrabX = inc.ReadFloat();
                                        float CrabZ = inc.ReadFloat();

                                        if (crab.random.Next(0, 10) > 8)
                                            crab.Direction = !crab.Direction;
                                        
                                        //Crab position and target sync.
                                        outmsg = server.CreateMessage();
                                        outmsg.Write((byte)PacketTypes.EnemySync);
                                        outmsg.Write((Int16)player.Id); //Id of the current crab controller.
                                        outmsg.Write(CrabX); //EnemyX
                                        outmsg.Write(CrabZ); //EnemyZ
                                        outmsg.Write(player.X);
                                        outmsg.Write(player.Y);
                                        outmsg.Write(crab.Direction);
                                        outmsg.Write(player.Connection.AverageRoundtripTime/2f); //Divide by 2 to get trip time.
                                        server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);
                                    }
                                }
                                break;
                            case PacketTypes.Ready:
                                {
                                    //Player is ready to start the game.
                                    player.Ready = inc.ReadBoolean();

                                    Console.WriteLine(player.Name + " changed their ready status to " + player.Ready);
                                    if (player.Ready)
                                        SendLobbyMessage("Server", player.Name + " is now Ready.");
                                    else
                                        SendLobbyMessage("Server", player.Name + " is no longer ready.");
                                }
                                break;
                            case PacketTypes.UpdateName:
                                {
                                    //Player changed their name.  Since the clients aren't aware of each other until the game starts,
                                    //there's no need to broadcast this message to other users.
                                    string newname = inc.ReadString();
                                    Console.WriteLine(player.Name + " changed their name to '" + newname + "'.");
                                    SendLobbyMessage("Server", player.Name + " changed their name to '" + newname + "'.");

                                    player.Name = newname;
                                }
                                break;
                            case PacketTypes.Disconnect:
                                {
                                    //Player requests to disconnect from the server.
                                    Console.WriteLine(player.Name + " has disconnected, removing player object.");

                                    outmsg = server.CreateMessage();
                                    outmsg.Write((byte)PacketTypes.RemovePlayer);
                                    outmsg.Write((Int16)player.Id);
                                    server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);

                                    players.Remove(player);

                                    outmsg = server.CreateMessage();
                                    outmsg.Write((byte)PacketTypes.PlayerCount);
                                    outmsg.Write((Int16)players.Count);
                                    server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);
                                }
                                break;
                            case PacketTypes.StartGame:
                                {
                                    //Someone clicked start game.  Lets make sure everyone is ready and we're in a state where we can start.
                                    if (gamePhase != (int)GameState.Lobby)
                                        break;

                                    bool ready = true;

                                    foreach (PlayerObject p in players)
                                        ready = ready & p.Ready;

                                    if (ready)
                                    {
                                        outmsg = server.CreateMessage();
                                        outmsg.Write((byte)PacketTypes.Message);
                                        outmsg.Write("All players ready, launching game.");
                                        server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);

                                        Console.WriteLine("All set, launching Game!");

                                        float numplayers = players.Count();
                                        float curplayer = ((numplayers-1) * 20f) / 2f * -1f;

                                        for (int i = 0; i < players.Count; i++)
                                        {
                                            PlayerObject p = players[i];

                                            //Lets create all the player objects for all players, 20 paces apart.
                                            outmsg = server.CreateMessage();
                                            outmsg.Write((byte)PacketTypes.AddPlayer);
                                            outmsg.Write((Int16)p.Id);

                                            p.X = curplayer;
                                            p.Y = -500f; //Hardcoded ftw!

                                            curplayer += 20f;

                                            outmsg.Write(p.X);
                                            outmsg.Write(p.Y);

                                            outmsg.Write(p.Name);

                                            server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);
                                        }

                                        outmsg = server.CreateMessage();
                                        outmsg.Write((byte)PacketTypes.PlayIntro);
                                        server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);

                                        gamePhase = (int)GameState.Intro;

                                        crab = new CrabBehavior(); //Prepare the CRAB!

                                        introtime = DateTime.Now;
                                    }
                                    else
                                    {
                                        outmsg = server.CreateMessage();
                                        outmsg.Write((byte)PacketTypes.Message);
                                        outmsg.Write("A player attempted to start the game, but not all players are ready.");
                                        server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);
                                        SendLobbyMessage("Server", "Game cannot start until all users are ready.");
                                    }
                                }
                                break;
                            case PacketTypes.PlayerSpecial:
                                {
                                    int shottype = inc.ReadInt16();

                                    outmsg = server.CreateMessage();
                                    outmsg.Write((byte)PacketTypes.PlayerSpecial);
                                    outmsg.Write((Int16)player.Id);
                                    outmsg.Write((Int16)shottype);
                                    server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableUnordered, 0);
                                    //Console.WriteLine("Relaying Special Action Message for " + player.Name + ".");
                                }
                                break;
                            case PacketTypes.PlayerAction:
                                {
                                    //Player hit a key or something!  Change their status, broadcast to other users.

                                    //Set player values
                                    //inc.ReadInt16(); //Player id is submitted, but not used.
                                    player.X = inc.ReadFloat();
                                    player.Y = inc.ReadFloat();
                                    player.VelocityX = inc.ReadFloat();
                                    player.VelocityY = inc.ReadFloat();
                                    player.Firing = inc.ReadBoolean();

                                    //Broadcast them to everyone else
                                    outmsg = server.CreateMessage();
                                    outmsg.Write((byte)PacketTypes.PlayerAction);
                                    outmsg.Write((Int16)player.Id);
                                    outmsg.Write(player.X);
                                    outmsg.Write(player.Y);
                                    outmsg.Write(player.VelocityX);
                                    outmsg.Write(player.VelocityY);
                                    outmsg.Write(player.Firing);
                                    outmsg.Write(player.Connection.AverageRoundtripTime/2f); //Not an exact science, but we'll use this to predict their position.
                                    server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 1);

                                    //Console.WriteLine("Relaying Action Message for " + player.Name + ". "+player.VelocityX + " " + player.VelocityY);
                                }
                                break;
                            case PacketTypes.HurtTarget:
                                {
                                    int damage = inc.ReadInt16();

                                    crab.CurrentHealth -= damage;
                                    bool hittype = inc.ReadBoolean();

                                    if (!hittype)
                                        player.dmgnormal += damage;
                                    else
                                        player.dmgweakpoint += damage;

                                    outmsg = server.CreateMessage();
                                    outmsg.Write((byte)PacketTypes.EnemyHealth);
                                    outmsg.Write((Int16)crab.CurrentHealth);
                                    server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.Unreliable, 0);
                                }
                                break;
                            case PacketTypes.PlayerHit:
                                {
                                    player.hitstaken += 1;

                                    outmsg = server.CreateMessage();
                                    outmsg.Write((byte)PacketTypes.PlayerHit);
                                    outmsg.Write((Int16)player.Id);
                                    server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableUnordered, 0);
                                }
                                break;
                        }
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        {
                            //Players connection status changed.
                            PlayerObject p1 = players.Find(p => p.Connection == inc.SenderConnection);
                            if (p1 == null)
                                break;

                            Console.WriteLine(p1.Name + " status changed to " + (NetConnectionStatus)inc.SenderConnection.Status + ".");
                        }
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
