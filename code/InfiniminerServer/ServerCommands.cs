using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Lidgren.Network;
using Lidgren.Network.Xna;
using Microsoft.Xna.Framework;

//Contains commands, mainly console related (but not dealing with the console itself)

namespace Infiniminer
{
    public partial class InfiniminerServer
    {
        public void MessageAll(string text)
        {
            if (announceChanges)
                SendServerMessage(text);
            ConsoleWrite(text);
        }


        public Dictionary<string, short> LoadAdminList()
        {
            Dictionary<string, short> temp = new Dictionary<string, short>();

            try
            {
                if (!File.Exists("admins.txt"))
                {
                    FileStream fs = File.Create("admins.txt");
                    StreamWriter sr = new StreamWriter(fs);
                    sr.WriteLine("#A list of all admins - just add one ip per line");
                    sr.Close();
                    fs.Close();
                }
                else
                {
                    FileStream file = new FileStream("admins.txt", FileMode.Open, FileAccess.Read);
                    StreamReader sr = new StreamReader(file);
                    string line = sr.ReadLine();
                    while (line != null)
                    {
                        if (line.Trim().Length != 0 && line.Trim().ToCharArray()[0] != '#')
                            temp.Add(line.Trim(), (short)2); //This will be changed to note authority too
                        line = sr.ReadLine();
                    }
                    sr.Close();
                    file.Close();
                }
            }
            catch
            {
                ConsoleWrite("Unable to load admin list.");
            }

            return temp;
        }

        public bool SaveAdminList()
        {
            try
            {
                FileStream file = new FileStream("admins.txt", FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(file);
                sw.WriteLine("#A list of all admins - just add one ip per line\n");
                foreach (string ip in banList)
                    sw.WriteLine(ip);
                sw.Close();
                file.Close();
                return true;
            }
            catch { }
            return false;
        }

        public List<string> LoadBanList()
        {
            List<string> retList = new List<string>();

            try
            {
                FileStream file = new FileStream("banlist.txt", FileMode.Open, FileAccess.Read);
                StreamReader sr = new StreamReader(file);
                string line = sr.ReadLine();
                while (line != null)
                {
                    retList.Add(line.Trim());
                    line = sr.ReadLine();
                }
                sr.Close();
                file.Close();
            }
            catch { }

            return retList;
        }

        public void SaveBanList(List<string> banList)
        {
            try
            {
                FileStream file = new FileStream("banlist.txt", FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(file);
                foreach (string ip in banList)
                    sw.WriteLine(ip);
                sw.Close();
                file.Close();
            }
            catch { }
        }

        public void KickPlayer(string ip)
        {
            KickPlayer(ip, false);
        }

        public void KickPlayer(string ip, bool name)
        {
            List<Player> playersToKick = new List<Player>();
            foreach (IClient p in playerList.Values)
            {
                if ((p.IP == ip && !name) || (p.Handle.ToLower().Contains(ip.ToLower()) && name))
                    playersToKick.Add(p);
            }
            foreach (IClient p in playersToKick)
            {
                p.NetConn.Disconnect("", 0);
                p.Kicked = true;
            }
        }

        public void BanPlayer(string ip)
        {
            BanPlayer(ip, false);
        }

        public void BanPlayer(string ip, bool name)
        {
            string realIp = ip;
            if (name)
            {
                foreach (IClient p in playerList.Values)
                {
                    if ((p.Handle == ip && !name) || (p.Handle.ToLower().Contains(ip.ToLower()) && name))
                    {
                        realIp = p.IP;
                        break;
                    }
                }
            }
            if (!banList.Contains(realIp))
            {
                banList.Add(realIp);
                SaveBanList(banList);
            }
        }

        public short GetAdmin(string ip)
        {
            if (admins.ContainsKey(ip.Trim()))
                return admins[ip.Trim()];
            return (short)0;
        }

        public void AdminPlayer(string ip)
        {
            AdminPlayer(ip, false, (short)2);
        }

        public void AdminPlayer(string ip, bool name)
        {
            AdminPlayer(ip, name, (short)2);
        }

        public void AdminPlayer(string ip, bool name, short authority)
        {
            string realIp = ip;
            if (name)
            {
                foreach (IClient p in playerList.Values)
                {
                    if ((p.Handle == ip && !name) || (p.Handle.ToLower().Contains(ip.ToLower()) && name))
                    {
                        realIp = p.IP;
                        break;
                    }
                }
            }
            if (!admins.ContainsKey(realIp))
            {
                admins.Add(realIp, authority);
                SaveAdminList();
            }
        }


        public void SaveLevel(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            for (int x = 0; x < MAPSIZE; x++)
                for (int y = 0; y < MAPSIZE; y++)
                    for (int z = 0; z < MAPSIZE; z++)
                        sw.WriteLine((byte)blockList[x, y, z] + "," + (byte)blockCreatorTeam[x, y, z]);
            sw.Close();
            fs.Close();
        }
        public void BackupLevel()
        {
            SaveLevel("autoBK.lvl");
        }

        public bool LoadLevel(string filename)
        {
            try
            {
                if (!File.Exists(filename))
                {
                    ConsoleWrite("Unable to load level - " + filename + " does not exist!");
                    return false;
                }
                SendServerMessage("Changing map to " + filename + "!");
                disconnectAll();

                FileStream fs = new FileStream(filename, FileMode.Open);
                StreamReader sr = new StreamReader(fs);
                for (int x = 0; x < MAPSIZE; x++)
                    for (int y = 0; y < MAPSIZE; y++)
                        for (int z = 0; z < MAPSIZE; z++)
                        {
                            string line = sr.ReadLine();
                            string[] fileArgs = line.Split(",".ToCharArray());
                            if (fileArgs.Length == 2)
                            {
                                blockList[x, y, z] = (BlockType)int.Parse(fileArgs[0], System.Globalization.CultureInfo.InvariantCulture);
                                blockCreatorTeam[x, y, z] = (PlayerTeam)int.Parse(fileArgs[1], System.Globalization.CultureInfo.InvariantCulture);
                            }
                        }
                sr.Close();
                fs.Close();
                ConsoleWrite("Level loaded successfully - now playing " + filename + "!");
                return true;
            }
            catch { }
            return false;
        }

        public void ResetLevel()
        {
            disconnectAll();
            newMap();
        }

        public void disconnectAll()
        {
            foreach (IClient p in playerList.Values)
            {
                p.NetConn.Disconnect("", 0);
            }
        }
        public void status()
        {
            ConsoleWrite(varGetS("name"));//serverName);
            ConsoleWrite(playerList.Count + " / " + varGetI("maxplayers") + " players");
            foreach (string name in varBoolBindings.Keys)
            {
                ConsoleWrite(name + " = " + varBoolBindings[name]);
            }
        }
    }
}