using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Lidgren.Network;
using Lidgren.Network.Xna;
using Microsoft.Xna.Framework;

namespace Infiniminer
{
    public partial class InfiniminerServer
    {
        InfiniminerNetServer netServer = null;
        public Dictionary<NetConnection, IClient> playerList = new Dictionary<NetConnection, IClient>();
        public List<NetConnection> toGreet = new List<NetConnection>();
        public Dictionary<string, short> admins = new Dictionary<string, short>(); //Short represents power - 1 for mod, 2 for full admin

        DateTime lastServerListUpdate = DateTime.Now;
        DateTime lastMapBackup = DateTime.Now;
        public List<string> banList = null;

        bool keepRunning = true;

        // Server restarting variables.
        DateTime restartTime = DateTime.Now;
        bool restartTriggered = false;
        

        public InfiniminerServer()
        {
            Console.SetWindowSize(1, 1);
            Console.SetBufferSize(80, CONSOLE_SIZE + 4);
            Console.SetWindowSize(80, CONSOLE_SIZE + 4);
        }

        public string GetExtraInfo()
        {
            string extraInfo = "";
            if (varGetB("sandbox"))
                extraInfo += "sandbox";
            else
                extraInfo += string.Format("{0:#.##k}", winningCashAmount / 1000);
            if (!includeLava)
                extraInfo += ", !lava";
            if (!varGetB("tnt"))
                extraInfo += ", !tnt";
            if (varGetB("insanelava") || varGetB("sspreads") || varGetB("stnt"))
                extraInfo += ", MetMod";
/*            if (varGetB("insanelava"))//insaneLava)
                extraInfo += ", ~lava";
            if (varGetB("sspreads"))
                extraInfo += ", shock->lava";
            if (varGetB("stnt"))//sphericalTnt && false)
                extraInfo += ", stnt";*/
            return extraInfo;
        }

        public void PublicServerListUpdate()
        {
            PublicServerListUpdate(false);
        }

        public void PublicServerListUpdate(bool doIt)
        {
            if (!varGetB("public"))
                return;

            TimeSpan updateTimeSpan = DateTime.Now - lastServerListUpdate;
            if (updateTimeSpan.TotalMinutes >= 1 || doIt)
                CommitUpdate();
        }


        public bool Start()
        {
            //Setup the variable toggles
            varBindingsInitialize();

            int tmpMaxPlayers = 16;

            // Read in from the config file.
            DatafileWriter dataFile = new DatafileWriter("server.config.txt");
            if (dataFile.Data.ContainsKey("winningcash"))
                winningCashAmount = uint.Parse(dataFile.Data["winningcash"], System.Globalization.CultureInfo.InvariantCulture);
            if (dataFile.Data.ContainsKey("includelava"))
                includeLava = bool.Parse(dataFile.Data["includelava"]);
            if (dataFile.Data.ContainsKey("orefactor"))
                oreFactor = uint.Parse(dataFile.Data["orefactor"], System.Globalization.CultureInfo.InvariantCulture);
            if (dataFile.Data.ContainsKey("maxplayers"))
                tmpMaxPlayers = (int)Math.Min(32, uint.Parse(dataFile.Data["maxplayers"], System.Globalization.CultureInfo.InvariantCulture));
            if (dataFile.Data.ContainsKey("public"))
                varSet("public", bool.Parse(dataFile.Data["public"]), true);
            if (dataFile.Data.ContainsKey("servername"))
                varSet("name", dataFile.Data["servername"], true);
            if (dataFile.Data.ContainsKey("sandbox"))
                varSet("sandbox", bool.Parse(dataFile.Data["sandbox"]), true);
            if (dataFile.Data.ContainsKey("notnt"))
                varSet("tnt", !bool.Parse(dataFile.Data["notnt"]), true);
            if (dataFile.Data.ContainsKey("sphericaltnt"))
                varSet("stnt", bool.Parse(dataFile.Data["sphericaltnt"]), true);
            if (dataFile.Data.ContainsKey("insanelava"))
                varSet("insanelava", bool.Parse(dataFile.Data["insanelava"]), true);
            if (dataFile.Data.ContainsKey("shockspreadslava"))
                varSet("sspreads", bool.Parse(dataFile.Data["shockspreadslava"]), true);
            if (dataFile.Data.ContainsKey("roadabsorbs"))
                varSet("roadabsorbs", bool.Parse(dataFile.Data["roadabsorbs"]), true);
            if (dataFile.Data.ContainsKey("minelava"))
                varSet("minelava", bool.Parse(dataFile.Data["minelava"]), true);
            if (dataFile.Data.ContainsKey("levelname"))
                levelToLoad = dataFile.Data["levelname"];
            if (dataFile.Data.ContainsKey("greeter"))
                varSet("greeter", dataFile.Data["greeter"],true);

            bool autoannounce = true;
            if (dataFile.Data.ContainsKey("autoannounce"))
                autoannounce = bool.Parse(dataFile.Data["autoannounce"]);

            // Load the ban-list.
            banList = LoadBanList();

            // Load the admin-list
            admins = LoadAdminList();

            if (tmpMaxPlayers>=0)
                varSet("maxplayers", tmpMaxPlayers, true);

            // Initialize the server.
            NetConfiguration netConfig = new NetConfiguration("InfiniminerPlus");
            netConfig.MaxConnections = (int)varGetI("maxplayers");
            netConfig.Port = 5565;
            netServer = new InfiniminerNetServer(netConfig);
            netServer.SetMessageTypeEnabled(NetMessageType.ConnectionApproval, true);
            //netServer.SimulatedMinimumLatency = 0.1f;
            //netServer.SimulatedLatencyVariance = 0.05f;
            //netServer.SimulatedLoss = 0.1f;
            //netServer.SimulatedDuplicates = 0.05f;
            netServer.Start();

            

            // Store the last time that we did a flow calculation.
            DateTime lastFlowCalc = DateTime.Now;
            DateTime lastMapeaterCalc = DateTime.Now;

            //Check if we should autoload a level
            if (dataFile.Data.ContainsKey("autoload") && bool.Parse(dataFile.Data["autoload"]))
            {
                blockList = new BlockType[MAPSIZE, MAPSIZE, MAPSIZE];
                blockCreatorTeam = new PlayerTeam[MAPSIZE, MAPSIZE, MAPSIZE];
                LoadLevel(levelToLoad);
            }
            else
            {
                // Calculate initial lava flows.
                ConsoleWrite("CALCULATING INITIAL LAVA FLOWS");
                ConsoleWrite("TOTAL LAVA BLOCKS = " + newMap());
            }
            

            //Caculate the shape of spherical tnt explosions
            CalculateExplosionPattern();

            // Send the initial server list update.
            if (autoannounce)
                PublicServerListUpdate(true);

            lastMapBackup = DateTime.Now;
            ServerListener listener = new ServerListener(netServer,this);
            System.Threading.Thread listenerthread = new System.Threading.Thread(new ThreadStart(listener.start));
            listenerthread.Start();
            // Main server loop!
            ConsoleWrite("SERVER READY");
            Random randomizer = new Random(56235676);
            while (keepRunning)
            {
                
                // Process any messages that are here.
                

                //Time to backup map?
                TimeSpan mapUpdateTimeSpan = DateTime.Now - lastMapBackup;
                if (mapUpdateTimeSpan.TotalMinutes > 5)
                {
                    System.Threading.Thread backupthread = new System.Threading.Thread(new ThreadStart(BackupLevel));
                    backupthread.Start();
                    lastMapBackup = DateTime.Now;
                }

                // Time to send a new server update?
                PublicServerListUpdate(); //It checks for public server / time span

                //Time to terminate finished map sending threads?
                TerminateFinishedThreads();

                // Check for players who are in the zone to deposit.
                DepositForPlayers();

                // Is it time to do a lava calculation? If so, do it!
                
                if (varGetB("mapeater"))
                {
                    TimeSpan eaterSpan = DateTime.Now - lastMapeaterCalc;
                    if (eaterSpan.TotalMilliseconds > 500)
                    {
                        lastMapeaterCalc = DateTime.Now;
                        for (int i = 0; i < 200; i++)
                        {
                            ushort x = (ushort)randomizer.Next(0, 64);
                            ushort y = (ushort)randomizer.Next(0, 64);
                            for (ushort z = 62; z > 0; z--)
                            {
                                if (blockList[x, z, y] != BlockType.None)
                                {
                                    SetBlock(x, z, y, BlockType.None, PlayerTeam.None);
                                    break;
                                }
                            }

                        }
                    }

                }
                TimeSpan timeSpan = DateTime.Now - lastFlowCalc;
                if (timeSpan.TotalMilliseconds > 500)
                {
                    DoLavaStuff();
                    lastFlowCalc = DateTime.Now;
                }

                // Handle console keypresses.
                while (Console.KeyAvailable)
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey();
                    if (keyInfo.Key == ConsoleKey.Enter)
                        ConsoleProcessInput();
                    else if (keyInfo.Key == ConsoleKey.Backspace)
                    {
                        if (consoleInput.Length > 0)
                            consoleInput = consoleInput.Substring(0, consoleInput.Length - 1);
                        ConsoleRedraw();
                    }
                    else
                    {
                        consoleInput += keyInfo.KeyChar;
                        ConsoleRedraw();
                    }
                }

                // Is the game over?
                if (winningTeam != PlayerTeam.None && !restartTriggered)
                {
                    BroadcastGameOver();
                    restartTriggered = true;
                    restartTime = DateTime.Now.AddSeconds(10);
                }

                // Restart the server?
                if (restartTriggered && DateTime.Now > restartTime)
                {
                    SaveLevel("autosave_" + (UInt64)DateTime.Now.ToBinary() + ".lvl");
                    netServer.Shutdown("The server is restarting.");
                    return true;
                }

                // Pass control over to waiting threads.
                Thread.Sleep(1);
            }

            MessageAll("Server going down NOW!");

            netServer.Shutdown("The server was terminated.");
            return false;
        }

        

        public string GetTeamName(PlayerTeam team)
        {
            switch (team)
            {
                case PlayerTeam.Red:
                    return "RED";
                case PlayerTeam.Blue:
                    return "BLUE";
            }
            return "";
        }

        public void SendServerMessageToPlayer(string message, NetConnection conn)
        {
            if (conn.Status == NetConnectionStatus.Connected)
            {
                NetBuffer msgBuffer = netServer.CreateBuffer();
                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
                msgBuffer.Write((byte)ChatMessageType.SayAll);
                msgBuffer.Write(Defines.Sanitize(message));
                
                playerList[conn].AddQueMsg(msgBuffer, NetChannel.ReliableInOrder3);
            }
        }

        public void SendServerMessage(string message)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
            msgBuffer.Write((byte)ChatMessageType.SayAll);
            msgBuffer.Write(Defines.Sanitize(message));
            foreach (IClient player in playerList.Values)
                //if (netConn.Status == NetConnectionStatus.Connected)
                player.AddQueMsg(msgBuffer, NetChannel.ReliableInOrder3);
        }

        // Lets a player know about their resources.
        public void SendResourceUpdate(IClient player)
        {
            if (player.NetConn.Status != NetConnectionStatus.Connected)
                return;

            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ResourceUpdate);
            msgBuffer.Write((uint)player.Ore);
            msgBuffer.Write((uint)player.Cash);
            msgBuffer.Write((uint)player.Weight);
            msgBuffer.Write((uint)player.OreMax);
            msgBuffer.Write((uint)player.WeightMax);
            msgBuffer.Write((uint)(player.Team == PlayerTeam.Red ? teamOreRed : teamOreBlue));
            msgBuffer.Write((uint)teamCashRed);
            msgBuffer.Write((uint)teamCashBlue);
            player.AddQueMsg(msgBuffer, NetChannel.ReliableInOrder1);
        }

        List<MapSender> mapSendingProgress = new List<MapSender>();

        public void TerminateFinishedThreads()
        {
            List<MapSender> mapSendersToRemove = new List<MapSender>();
            foreach (MapSender ms in mapSendingProgress)
            {
                if (ms.finished)
                {
                    ms.stop();
                    mapSendersToRemove.Add(ms);
                }
            }
            foreach (MapSender ms in mapSendersToRemove)
            {
                mapSendingProgress.Remove(ms);
            }
        }

        public void SendCurrentMap(NetConnection client)
        {
            MapSender ms = new MapSender(client, this, netServer, MAPSIZE,playerList[client].compression);
            mapSendingProgress.Add(ms);
        }

        /*public void SendCurrentMapB(NetConnection client)
        {
            Debug.Assert(MAPSIZE == 64, "The BlockBulkTransfer message requires a map size of 64.");
            
            for (byte x = 0; x < MAPSIZE; x++)
                for (byte y=0; y<MAPSIZE; y+=16)
                {
                    NetBuffer msgBuffer = netServer.CreateBuffer();
                    msgBuffer.Write((byte)InfiniminerMessage.BlockBulkTransfer);
                    msgBuffer.Write(x);
                    msgBuffer.Write(y);
                    for (byte dy=0; dy<16; dy++)
                        for (byte z = 0; z < MAPSIZE; z++)
                            msgBuffer.Write((byte)(blockList[x, y+dy, z]));
                    if (client.Status == NetConnectionStatus.Connected)
                        netServer.SendMessage(msgBuffer, client, NetChannel.ReliableUnordered);
                }
        }*/

        public void SendPlayerPing(uint playerId)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerPing);
            msgBuffer.Write(playerId);

            foreach (IClient player in playerList.Values)
                //if (netConn.Status == NetConnectionStatus.Connected)
                player.AddQueMsg(msgBuffer, NetChannel.ReliableUnordered);
        }

        public void SendPlayerUpdate(IClient player)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerUpdate);
            msgBuffer.Write((uint)player.ID);
            msgBuffer.Write(player.Position);
            msgBuffer.Write(player.Heading);
            msgBuffer.Write((byte)player.Tool);

            if (player.QueueAnimationBreak)
            {
                player.QueueAnimationBreak = false;
                msgBuffer.Write(false);
            }
            else
                msgBuffer.Write(player.UsingTool);

            msgBuffer.Write((ushort)player.Score / 100);

            foreach (IClient iplayer in playerList.Values)
                //if (netConn.Status == NetConnectionStatus.Connected)
                iplayer.AddQueMsg(msgBuffer, NetChannel.UnreliableInOrder1);
        }

        public void SendSetBeacon(Vector3 position, string text, PlayerTeam team)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.SetBeacon);
            msgBuffer.Write(position);
            msgBuffer.Write(text);
            msgBuffer.Write((byte)team);
            foreach (IClient player in playerList.Values)
                //if (netConn.Status == NetConnectionStatus.Connected)
                player.AddQueMsg(msgBuffer, NetChannel.ReliableInOrder2);
        }

        public void SendPlayerJoined(IClient player)
        {
            NetBuffer msgBuffer;

            // Let this player know about other players.
            foreach (IClient p in playerList.Values)
            {
                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerJoined);
                msgBuffer.Write((uint)p.ID);
                msgBuffer.Write(p.Handle);
                msgBuffer.Write(p == player);
                msgBuffer.Write(p.Alive);
                //if (player.NetConn.Status == NetConnectionStatus.Connected)
                    player.AddQueMsg(msgBuffer, NetChannel.ReliableInOrder2);

                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerSetTeam);
                msgBuffer.Write((uint)p.ID);
                msgBuffer.Write((byte)p.Team);
                //if (player.NetConn.Status == NetConnectionStatus.Connected)
                    player.AddQueMsg(msgBuffer, NetChannel.ReliableInOrder2);
            }

            // Let this player know about all placed beacons.
            foreach (KeyValuePair<Vector3, Beacon> bPair in beaconList)
            {
                Vector3 position = bPair.Key;
                position.Y += 1; // beacon is shown a block below its actually position to make altitude show up right
                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.SetBeacon);
                msgBuffer.Write(position);
                msgBuffer.Write(bPair.Value.ID);
                msgBuffer.Write((byte)bPair.Value.Team);
                //if (player.NetConn.Status == NetConnectionStatus.Connected)
                    player.AddQueMsg(msgBuffer,  NetChannel.ReliableInOrder2);
            }

            // Let other players know about this player.
            msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerJoined);
            msgBuffer.Write((uint)player.ID);
            msgBuffer.Write(player.Handle);
            msgBuffer.Write(false);
            msgBuffer.Write(player.Alive);

            foreach (IClient iplayer in playerList.Values)
                //if (netConn.Status == NetConnectionStatus.Connected)
                iplayer.AddQueMsg(msgBuffer, NetChannel.ReliableInOrder2);

            // Send this out just incase someone is joining at the last minute.
            if (winningTeam != PlayerTeam.None)
                BroadcastGameOver();

            // Send out a chat message.
            msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
            msgBuffer.Write((byte)ChatMessageType.SayAll);
            msgBuffer.Write(player.Handle + " HAS JOINED THE ADVENTURE!");
            foreach (IClient iplayer in playerList.Values)
                //if (netConn.Status == NetConnectionStatus.Connected)
                iplayer.AddQueMsg(msgBuffer, NetChannel.ReliableInOrder3);
        }

        public void BroadcastGameOver()
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.GameOver);
            msgBuffer.Write((byte)winningTeam);
            foreach (IClient player in playerList.Values)
                //if (netConn.Status == NetConnectionStatus.Connected)
                player.AddQueMsg(msgBuffer, NetChannel.ReliableUnordered);    
        }

        public void SendPlayerLeft(IClient player, string reason)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerLeft);
            msgBuffer.Write((uint)player.ID);
            foreach (IClient iplayer in playerList.Values)
                if (player.NetConn != iplayer.NetConn)
                iplayer.AddQueMsg(msgBuffer, NetChannel.ReliableInOrder2);

            // Send out a chat message.
            msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
            msgBuffer.Write((byte)ChatMessageType.SayAll);
            msgBuffer.Write(player.Handle + " " + reason);
            foreach (IClient iplayer in playerList.Values)
                //if (netConn.Status == NetConnectionStatus.Connected)
                iplayer.AddQueMsg(msgBuffer, NetChannel.ReliableInOrder3);
        }

        public void SendPlayerSetTeam(IClient player)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerSetTeam);
            msgBuffer.Write((uint)player.ID);
            msgBuffer.Write((byte)player.Team);
            foreach (IClient iplayer in playerList.Values)
                //if (netConn.Status == NetConnectionStatus.Connected)
                iplayer.AddQueMsg(msgBuffer, NetChannel.ReliableInOrder2);
        }

        public void SendPlayerDead(IClient player)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerDead);
            msgBuffer.Write((uint)player.ID);
            foreach (IClient iplayer in playerList.Values)
                //if (netConn.Status == NetConnectionStatus.Connected)
                iplayer.AddQueMsg(msgBuffer, NetChannel.ReliableInOrder2);
        }

        public void SendPlayerAlive(IClient player)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerAlive);
            msgBuffer.Write((uint)player.ID);
            foreach (IClient iplayer in playerList.Values)
                //if (netConn.Status == NetConnectionStatus.Connected)
                iplayer.AddQueMsg(msgBuffer, NetChannel.ReliableInOrder2);
        }

        public void PlaySound(InfiniminerSound sound, Vector3 position)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlaySound);
            msgBuffer.Write((byte)sound);
            msgBuffer.Write(true);
            msgBuffer.Write(position);
            foreach (IClient player in playerList.Values)
                //if (netConn.Status == NetConnectionStatus.Connected)
                player.AddQueMsg(msgBuffer, NetChannel.ReliableUnordered);
        }

        Thread updater;
        bool updated = true;

        public void CommitUpdate()
        {
            try
            {
                if (updated)
                {
                    if (updater != null && !updater.IsAlive)
                    {
                        updater.Abort();
                        updater.Join();
                    }
                    updated = false;
                    updater = new Thread(new ThreadStart(this.RunUpdateThread));
                    updater.Start();
                }
            }
            catch { }
        }

        public void RunUpdateThread()
        {
            if (!updated)
            {
                Dictionary<string, string> postDict = new Dictionary<string, string>();
                postDict["name"] = varGetS("name");
                postDict["game"] = "INFINIMINER";
                postDict["player_count"] = "" + playerList.Keys.Count;
                postDict["player_capacity"] = "" + varGetI("maxplayers");
                postDict["extra"] = GetExtraInfo() + ";test";

                lastServerListUpdate = DateTime.Now;

                try
                {
                    HttpRequest.Post("http://apps.keithholman.net/post", postDict);
                    ConsoleWrite("PUBLICLIST: UPDATING SERVER LISTING");
                }
                catch (Exception)
                {
                    ConsoleWrite("PUBLICLIST: ERROR CONTACTING SERVER");
                }

                updated = true;
            }
        }
    }
}
