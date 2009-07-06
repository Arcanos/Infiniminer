using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Lidgren.Network;
using Lidgren.Network.Xna;
using Microsoft.Xna.Framework;

//Contains assorted server settings

namespace Infiniminer
{
    public partial class InfiniminerServer
    {
        
        
        int lavaBlockCount = 0;
        uint oreFactor = 10;
        uint prevMaxPlayers = 16;
        bool includeLava = true;
        string levelToLoad = "";
        string greeter = "";
        

        bool[, ,] tntExplosionPattern = new bool[0, 0, 0];
        bool announceChanges = true;



        uint teamCashRed = 0;
        uint teamCashBlue = 0;
        uint teamOreRed = 0;
        uint teamOreBlue = 0;

        uint winningCashAmount = 10000;
        PlayerTeam winningTeam = PlayerTeam.None;

        

        //Variable handling
        Dictionary<string, bool> varBoolBindings = new Dictionary<string, bool>();
        Dictionary<string, string> varStringBindings = new Dictionary<string, string>();
        Dictionary<string, int> varIntBindings = new Dictionary<string, int>();
        Dictionary<string, string> varDescriptions = new Dictionary<string, string>();
        Dictionary<string, bool> varAreMessage = new Dictionary<string, bool>();

        public void varBindingsInitialize()
        {
            //Bool bindings
            varBind("tnt", "TNT explosions", false, true);
            varBind("stnt", "Spherical TNT explosions", true, true);
            varBind("sspreads", "Lava spreading via shock blocks", true, false);
            varBind("roadabsorbs", "Letting road blocks above lava absorb it", true, false);
            varBind("insanelava", "Insane lava spreading, so as to fill any hole", false, false);
            varBind("minelava", "Lava pickaxe mining", true, false);
            varBind("mapeater", "Survive as the map slowly disappears", false,false);
            //***New***
            varBind("public", "Server publicity", true, false);
            varBind("sandbox", "Sandbox mode", true, false);
            //Announcing is a special case, as it will never announce for key name announcechanges
            varBind("announcechanges", "Toggles variable changes being announced to clients", true, false);

            //String bindings
            varBind("name", "Server name as it appears on the server browser", "Unnamed Server");
            varBind("greeter", "The message sent to new players", "");

            //Int bindings
            varBind("maxplayers", "Maximum player count", 16);
            varBind("explosionradius", "The radius of spherical tnt explosions", 3);
        }

        public void varBind(string name, string desc, bool initVal, bool useAre)
        {
            varBoolBindings[name] = initVal;
            varDescriptions[name] = desc;
            /*if (varBoolBindings.ContainsKey(name))
                varBoolBindings[name] = initVal;
            else
                varBoolBindings.Add(name, initVal);

            if (varDescriptions.ContainsKey(name))
                varDescriptions[name] = desc;
            else
                varDescriptions.Add(name, desc);*/

            varAreMessage[name] = useAre;
        }

        public void varBind(string name, string desc, string initVal)
        {
            varStringBindings[name] = initVal;
            varDescriptions[name] = desc;
            /*
            if (varStringBindings.ContainsKey(name))
                varStringBindings[name] = initVal;
            else
                varStringBindings.Add(name, initVal);

            if (varDescriptions.ContainsKey(name))
                varDescriptions[name] = desc;
            else
                varDescriptions.Add(name, desc);*/
        }

        public void varBind(string name, string desc, int initVal)
        {
            varIntBindings[name] = initVal;
            varDescriptions[name] = desc;
            /*if (varDescriptions.ContainsKey(name))
                varDescriptions[name] = desc;
            else
                varDescriptions.Add(name, desc);*/
        }

        public bool varChangeCheckSpecial(string name)
        {
            switch (name)
            {
                case "maxplayers":
                    //Check if smaller than player count
                    if (varGetI(name) < playerList.Count)
                    {
                        //Bail, set to previous value
                        varSet(name, (int)prevMaxPlayers, true);
                        return false;
                    }
                    else
                    {
                        prevMaxPlayers = (uint)varGetI(name);
                        netServer.Configuration.MaxConnections = varGetI(name);
                    }
                    break;
                case "explosionradius":
                    CalculateExplosionPattern();
                    break;
                case "greeter":
                    /*PropertyBag _P = new PropertyBag(new InfiniminerGame(new string[]{}));
                    string[] format = _P.ApplyWordrwap(varGetS("greeter"));
                    */
                    greeter = varGetS("greeter");
                    break;
            }
            return true;
        }

        public bool varGetB(string name)
        {
            if (varBoolBindings.ContainsKey(name) && varDescriptions.ContainsKey(name))
                return varBoolBindings[name];
            else
                return false;
        }

        public string varGetS(string name)
        {
            if (varStringBindings.ContainsKey(name) && varDescriptions.ContainsKey(name))
                return varStringBindings[name];
            else
                return "";
        }

        public int varGetI(string name)
        {
            if (varIntBindings.ContainsKey(name) && varDescriptions.ContainsKey(name))
                return varIntBindings[name];
            else
                return -1;
        }

        public int varExists(string name)
        {
            if (varDescriptions.ContainsKey(name))
                if (varBoolBindings.ContainsKey(name))
                    return 1;
                else if (varStringBindings.ContainsKey(name))
                    return 2;
                else if (varIntBindings.ContainsKey(name))
                    return 3;
            return 0;
        }

        public void varSet(string name, bool val)
        {
            varSet(name, val, false);
        }

        public void varSet(string name, bool val, bool silent)
        {
            if (varBoolBindings.ContainsKey(name) && varDescriptions.ContainsKey(name))
            {
                varBoolBindings[name] = val;
                string enabled = val ? "enabled!" : "disabled.";
                if (name != "announcechanges" && !silent)
                    MessageAll(varDescriptions[name] + (varAreMessage[name] ? " are " + enabled : " is " + enabled));
                if (!silent)
                {
                    varReportStatus(name, false);
                    varChangeCheckSpecial(name);
                }
            }
            else
                ConsoleWrite("Variable \"" + name + "\" does not exist!");
        }

        public void varSet(string name, string val)
        {
            varSet(name, val, false);
        }

        public void varSet(string name, string val, bool silent)
        {
            if (varStringBindings.ContainsKey(name) && varDescriptions.ContainsKey(name))
            {
                varStringBindings[name] = val;
                if (!silent)
                {
                    varReportStatus(name);
                    varChangeCheckSpecial(name);
                }
            }
            else
                ConsoleWrite("Variable \"" + name + "\" does not exist!");
        }

        public void varSet(string name, int val)
        {
            varSet(name, val, false);
        }

        public void varSet(string name, int val, bool silent)
        {
            if (varIntBindings.ContainsKey(name) && varDescriptions.ContainsKey(name))
            {
                varIntBindings[name] = val;
                if (!silent)
                {
                    MessageAll(name + " = " + val.ToString());
                    varChangeCheckSpecial(name);
                }
            }
            else
                ConsoleWrite("Variable \"" + name + "\" does not exist!");
        }

        public string varList()
        {
            return varList(false);
        }

        private void varListType(ICollection<string> keys, string naming)
        {

            const int lineLength = 3;
            if (keys.Count > 0)
            {
                ConsoleWrite(naming);
                int i = 1;
                string output = "";
                foreach (string key in keys)
                {
                    if (i == 1)
                    {
                        output += "\t" + key;
                    }
                    else if (i >= lineLength)
                    {
                        output += ", " + key;
                        ConsoleWrite(output);
                        output = "";
                        i = 0;
                    }
                    else
                    {
                        output += ", " + key;
                    }
                    i++;
                }
                if (i > 1)
                    ConsoleWrite(output);
            }
        }

        public string varList(bool autoOut)
        {
            if (!autoOut)
            {
                string output = "";
                int i = 0;
                foreach (string key in varBoolBindings.Keys)
                {
                    if (i == 0)
                        output += key;
                    else
                        output += "," + key;
                    i++;
                }
                foreach (string key in varStringBindings.Keys)
                {
                    if (i == 0)
                        output += "s " + key;
                    else
                        output += ",s " + key;
                    i++;
                }
                return output;
            }
            else
            {
                varListType((ICollection<string>)varBoolBindings.Keys, "Boolean Vars:");
                varListType((ICollection<string>)varStringBindings.Keys, "String Vars:");
                varListType((ICollection<string>)varIntBindings.Keys, "Int Vars:");

                return "";
            }
        }

        public void varReportStatus(string name)
        {
            varReportStatus(name, true);
        }

        public void varReportStatus(string name, bool full)
        {
            if (varDescriptions.ContainsKey(name))
            {
                if (varBoolBindings.ContainsKey(name))
                {
                    ConsoleWrite(name + " = " + varBoolBindings[name].ToString());
                    if (full)
                        ConsoleWrite(varDescriptions[name]);
                    return;
                }
                else if (varStringBindings.ContainsKey(name))
                {
                    ConsoleWrite(name + " = " + varStringBindings[name]);
                    if (full)
                        ConsoleWrite(varDescriptions[name]);
                    return;
                }
                else if (varIntBindings.ContainsKey(name))
                {
                    ConsoleWrite(name + " = " + varIntBindings[name]);
                    if (full)
                        ConsoleWrite(varDescriptions[name]);
                    return;
                }
            }
            ConsoleWrite("Variable \"" + name + "\" does not exist!");
        }

        public string varReportStatusString(string name, bool full)
        {
            if (varDescriptions.ContainsKey(name))
            {
                if (varBoolBindings.ContainsKey(name))
                {
                    return name + " = " + varBoolBindings[name].ToString();
                }
                else if (varStringBindings.ContainsKey(name))
                {
                    return name + " = " + varStringBindings[name];
                }
                else if (varIntBindings.ContainsKey(name))
                {
                    return name + " = " + varIntBindings[name];
                }
            }
            return "";
        }
    }
}
