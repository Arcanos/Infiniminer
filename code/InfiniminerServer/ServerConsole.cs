using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Lidgren.Network;
using Lidgren.Network.Xna;
using Microsoft.Xna.Framework;


//Contains core console code

namespace Infiniminer
{
    public partial class InfiniminerServer
    {

        const int CONSOLE_SIZE = 30;
        List<string> consoleText = new List<string>();
        string consoleInput = "";


        public void ConsoleWrite(string text)
        {
            consoleText.Add(text);
            if (consoleText.Count > CONSOLE_SIZE)
                consoleText.RemoveAt(0);
            ConsoleRedraw();
        }

        public void ConsoleProcessInput()
        {
            ConsoleWrite("> " + consoleInput);

            ProcessCommand(consoleInput, (short)2, null);

            consoleInput = "";
            ConsoleRedraw();
        }

        public bool ProcessCommand(string chat)
        {
            return ProcessCommand(chat, (short)1, null);
        }

        public bool ProcessCommand(string input, short authority, IClient sender)
        {
            if (authority == 0)
                return false;
            if (sender != null)
                sender.admin = GetAdmin(sender.IP);
            string[] args = input.Split(' '.ToString().ToCharArray(), 2);
            if (args[0].StartsWith("\\") && args[0].Length > 1)
                args[0] = args[0].Substring(1);
            switch (args[0].ToLower())
            {
                case "help":
                    {
                        if (sender == null)
                        {
                            ConsoleWrite("SERVER CONSOLE COMMANDS:");
                            ConsoleWrite(" announce");
                            ConsoleWrite(" players");
                            ConsoleWrite(" kick <ip>");
                            ConsoleWrite(" kickn <name>");
                            ConsoleWrite(" ban <ip>");
                            ConsoleWrite(" bann <name>");
                            ConsoleWrite(" say <message>");
                            ConsoleWrite(" save <mapfile>");
                            ConsoleWrite(" load <mapfile>");
                            ConsoleWrite(" toggle <var>");
                            ConsoleWrite(" <var> <value>");
                            ConsoleWrite(" <var>");
                            ConsoleWrite(" listvars");
                            ConsoleWrite(" status");
                            ConsoleWrite(" restart");
                            ConsoleWrite(" quit");
                        }
                        else
                        {
                            SendServerMessageToPlayer(sender.Handle + ", the " + args[0].ToLower() + " command is only for use in the server console.", sender.NetConn);
                        }
                    }
                    break;
                case "players":
                    {
                        if (sender == null)
                        {
                            ConsoleWrite("( " + playerList.Count + " / " + varGetI("maxplayers") + " )");
                            foreach (IClient p in playerList.Values)
                            {
                                string teamIdent = "";
                                if (p.Team == PlayerTeam.Red)
                                    teamIdent = " (R)";
                                else if (p.Team == PlayerTeam.Blue)
                                    teamIdent = " (B)";
                                if (p.IsAdmin)
                                    teamIdent += " (Admin)";
                                ConsoleWrite(p.Handle + teamIdent);
                                ConsoleWrite("  - " + p.IP);
                            }
                        }
                        else
                        {
                            SendServerMessageToPlayer(sender.Handle + ", the " + args[0].ToLower() + " command is only for use in the server console.", sender.NetConn);
                        }
                    }
                    break;
                case "admins":
                    {
                        ConsoleWrite("Admin list:");
                        foreach (string ip in admins.Keys)
                            ConsoleWrite(ip);
                    }
                    break;
                case "admin":
                    {
                        if (args.Length == 2)
                        {
                            if (sender == null || sender.admin >= 2)
                                AdminPlayer(args[1]);
                            else
                                SendServerMessageToPlayer("You do not have the authority to add admins.", sender.NetConn);
                        }
                    }
                    break;
                case "adminn":
                    {
                        if (args.Length == 2)
                        {
                            if (sender == null || sender.admin >= 2)
                                AdminPlayer(args[1], true);
                            else
                                SendServerMessageToPlayer("You do not have the authority to add admins.", sender.NetConn);
                        }
                    }
                    break;
                case "listvars":
                    if (sender == null)
                        varList(true);
                    else
                    {
                        SendServerMessageToPlayer(sender.Handle + ", the " + args[0].ToLower() + " command is only for use in the server console.", sender.NetConn);
                    }
                    break;
                case "status":
                    if (sender == null)
                        status();
                    else
                        SendServerMessageToPlayer(sender.Handle + ", the " + args[0].ToLower() + " command is only for use in the server console.", sender.NetConn);
                    break;
                case "announce":
                    {
                        PublicServerListUpdate(true);
                    }
                    break;
                case "kick":
                    {
                        if (authority >= 1 && args.Length == 2)
                        {
                            if (sender != null)
                                ConsoleWrite("SERVER: " + sender.Handle + " has kicked " + args[1]);
                            KickPlayer(args[1]);
                        }
                    }
                    break;
                case "kickn":
                    {
                        if (authority >= 1 && args.Length == 2)
                        {
                            if (sender != null)
                                ConsoleWrite("SERVER: " + sender.Handle + " has kicked " + args[1]);
                            KickPlayer(args[1], true);
                        }
                    }
                    break;

                case "ban":
                    {
                        if (authority >= 1 && args.Length == 2)
                        {
                            if (sender != null)
                                ConsoleWrite("SERVER: " + sender.Handle + " has banned " + args[1]);
                            BanPlayer(args[1]);
                            KickPlayer(args[1]);
                        }
                    }
                    break;

                case "bann":
                    {
                        if (authority >= 1 && args.Length == 2)
                        {
                            if (sender != null)
                                ConsoleWrite("SERVER: " + sender.Handle + " has bannec " + args[1]);
                            BanPlayer(args[1], true);
                            KickPlayer(args[1], true);
                        }
                    }
                    break;

                case "toggle":
                    if (authority >= 1 && args.Length == 2)
                    {
                        int exists = varExists(args[1]);
                        if (exists == 1)
                        {
                            bool val = varGetB(args[1]);
                            varSet(args[1], !val);
                        }
                        else if (exists == 2)
                            ConsoleWrite("Cannot toggle a string value.");
                        else
                            varReportStatus(args[1]);
                    }
                    else
                        ConsoleWrite("Need variable name to toggle!");
                    break;
                case "quit":
                    {
                        if (authority >= 2)
                        {
                            if (sender != null)
                                ConsoleWrite(sender.Handle + " is shutting down the server.");
                            keepRunning = false;
                        }
                    }
                    break;

                case "restart":
                    {
                        if (authority >= 2)
                        {
                            if (sender != null)
                                ConsoleWrite(sender.Handle + " is restarting the server.");
                            disconnectAll();
                            restartTriggered = true;
                            restartTime = DateTime.Now;
                        }
                    }
                    break;

                case "say":
                    {
                        if (args.Length == 2)
                        {
                            string message = "SERVER: " + args[1];
                            SendServerMessage(message);
                        }
                    }
                    break;

                case "save":
                    {
                        if (args.Length >= 2)
                        {
                            if (sender != null)
                                ConsoleWrite(sender.Handle + " is saving the map.");
                            SaveLevel(args[1]);
                        }
                    }
                    break;

                case "load":
                    {
                        if (args.Length >= 2)
                        {
                            if (sender != null)
                                ConsoleWrite(sender.Handle + " is loading a map.");
                            LoadLevel(args[1]);
                            /*if (LoadLevel(args[1]))
                                Console.WriteLine("Loaded level " + args[1]);
                            else
                                Console.WriteLine("Level file not found!");*/
                        }
                        else if (levelToLoad != "")
                        {
                            LoadLevel(levelToLoad);
                        }
                    }
                    break;
                default: //Check / set var
                    {
                        string name = args[0];
                        int exists = varExists(name);
                        if (exists > 0)
                        {
                            if (args.Length == 2)
                            {
                                try
                                {
                                    if (exists == 1)
                                    {
                                        bool newVal = false;
                                        newVal = bool.Parse(args[1]);
                                        varSet(name, newVal);
                                    }
                                    else if (exists == 2)
                                    {
                                        varSet(name, args[1]);
                                    }
                                    else if (exists == 3)
                                    {
                                        varSet(name, Int32.Parse(args[1]));
                                    }

                                }
                                catch { }
                            }
                            else
                            {
                                if (sender == null)
                                    varReportStatus(name);
                                else
                                    SendServerMessageToPlayer(sender.Handle + ": The " + args[0].ToLower() + " command is only for use in the server console.", sender.NetConn);
                            }
                        }
                        else
                        {
                            char first = args[0].ToCharArray()[0];
                            if (first == 'y' || first == 'Y')
                            {
                                string message = "SERVER: " + args[0].Substring(1);
                                if (args.Length > 1)
                                    message += (message != "SERVER: " ? " " : "") + args[1];
                                SendServerMessage(message);
                            }
                            else
                            {
                                if (sender == null)
                                    ConsoleWrite("Unknown command/var.");
                                return false;
                            }
                        }
                    }
                    break;
            }
            return true;
        }
        public void ConsoleRedraw()
        {
            Console.Clear();
            ConsoleDrawCentered("INFINIMINER SERVER " + Defines.INFINIMINER_VERSION, 0);
            ConsoleDraw("================================================================================", 0, 1);
            for (int i = 0; i < consoleText.Count; i++)
                ConsoleDraw(consoleText[i], 0, i + 2);
            ConsoleDraw("================================================================================", 0, CONSOLE_SIZE + 2);
            ConsoleDraw("> " + consoleInput, 0, CONSOLE_SIZE + 3);
        }

        public void ConsoleDraw(string text, int x, int y)
        {
            Console.SetCursorPosition(x, y);
            Console.Write(text);
        }

        public void ConsoleDrawCentered(string text, int y)
        {
            Console.SetCursorPosition(40 - text.Length / 2, y);
            Console.Write(text);
        }
    }
 }
