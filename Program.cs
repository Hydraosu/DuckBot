﻿using System;
using System.Text;
using System.Collections.Generic;
using Discord;
using Discord.Commands;
using System.IO;

namespace DuckBot
{
    internal sealed class Program
    {
        public static readonly Random Rand = new Random();

        static void Main(string[] args)
        {
            File.AppendAllText(DuckData.LogFile.FullName, "[Notice] [" + DateTime.UtcNow.ToShortTimeString() + "] Starting DuckBot...");
            if (!DuckData.SessionsDir.Exists) DuckData.SessionsDir.Create();
            foreach (FileInfo fi in DuckData.SessionsDir.EnumerateFiles("session_*.dat"))
            {
                ulong id = ulong.Parse(fi.Name.Remove(fi.Name.Length - 4).Substring(8));
                Session s = new Session(id);
                using (BinaryReader br = new BinaryReader(fi.OpenRead()))
                    s.Load(br);
                DuckData.ServerSessions.Add(id, s);
            }
            new Program().Start();
        }

        private DiscordClient dclient;

        public void Start()
        {
            dclient = new DiscordClient(x =>
            {
                x.AppName = "DuckBot";
                x.LogLevel = LogSeverity.Info;
                x.LogHandler = Log;

            });
            dclient.UsingCommands(x =>
            {
                x.PrefixChar = '>';
                x.AllowMentionPrefix = true;
                x.HelpMode = HelpMode.Public;
            });
            CreateCommands();
            dclient.MessageReceived += MessageRecieved;
            dclient.UserUpdated += UserUpdated;
            dclient.ExecuteAndWait(async () =>
            {
                await dclient.Connect("MjY1MTEwNDEzNDIxNTc2MTky.C0qW1g.EkGzwhfFyVKI6qtBQOjFMGP0zNA", TokenType.Bot);
            });
        }

        public Session CreateSession(ulong sid)
        {
            lock (DuckData.ServerSessions)
                if (!DuckData.ServerSessions.ContainsKey(sid))
                {
                    Session s = new Session(sid);
                    DuckData.ServerSessions.Add(sid, s);
                    return s;
                }
                else return DuckData.ServerSessions[sid];
        }

        public void CreateCommands()
        {
            CommandService svc = dclient.GetService<CommandService>();

            svc.CreateCommand("add")
                .Description("Adds a function definition")
                .Parameter("type", ParameterType.Required)
                .Parameter("name", ParameterType.Required)
                .Parameter("content", ParameterType.Unparsed)
                .Do(async e =>
                {
                    try
                    {
                        Session s = CreateSession(e.Server.Id);
                        string cmd = e.Args[1].ToLowerInvariant();

                        string oldContent = "";
                        if (s.Cmds.ContainsKey(cmd))
                        {
                            oldContent = s.Cmds[cmd].Content;
                            s.Cmds.Remove(cmd);
                        }
                        if (e.Args[0].ToLower() == "csharp")
                        {
                            if (DuckData.CSharpCommandAdders.Contains(e.User.Id))
                            {
                                if (e.User.Id != DuckData.SuperUser && (e.Args[2].Contains("Assembly") || e.Args[2].Contains("System.IO") || e.Args[2].Contains("Environment.Exit")))
                                {
                                    await e.Channel.SendMessage("Someting in this code is not permitted to use in DuckBot");
                                    return;
                                }
                                else s.Cmds.Add(cmd, new Command(e.Args[0], e.Args[2], e.User.Name));
                            }
                            else
                            {
                                await e.Channel.SendMessage("You don't have sufficent permissons.");
                                return;
                            }
                        }
                        else if (e.Args[0].ToLower() == "whitelist")
                        {
                            if (e.User.Id == DuckData.SuperUser)
                                DuckData.CSharpCommandAdders.Add(ulong.Parse(e.Args[1]));
                            else await e.Channel.SendMessage("You don't have sufficent permissons.");
                            return;
                        }
                        else s.Cmds.Add(cmd, new Command(e.Args[0], e.Args[2], e.User.Name));
                        await s.SaveAsync();
                        string toSend;
                        if (s.ShowChanges) toSend = "Changed from : ```" + oldContent + "``` to ```" + s.Cmds[e.Args[1]].Content + "```";
                        else toSend = "Done!";
                        await e.Channel.SendMessage(toSend);
                    }
                    catch (Exception ex)
                    {
                        Log(ex.ToString(), true);
                    }
                });
            svc.CreateCommand("remove")
                .Description("Removes a function")
                .Parameter("name", ParameterType.Required)
                .Do(async e =>
                {
                    Session s = CreateSession(e.Server.Id);
                    string cmd = e.Args[0].ToLowerInvariant();
                    if (s.Cmds.ContainsKey(cmd))
                    {
                        string log = s.ShowChanges ? "\nContent: ```" + s.Cmds[cmd].Content + "```" : "";
                        s.Cmds.Remove(cmd);
                        await e.Channel.SendMessage("Removed command `" + cmd + "`" + log);
                        await s.SaveAsync();
                    }
                    else await e.Channel.SendMessage("No command to remove!");
                });

            svc.CreateCommand("info")
               .Description("Info about a function")
               .Parameter("name", ParameterType.Required)
               .Do(async e =>
               {
                   Session s = CreateSession(e.Server.Id);
                   string cmd = e.Args[0].ToLowerInvariant();
                   if (s.Cmds.ContainsKey(cmd))
                   {
                       Command c = s.Cmds[cmd];
                       string prefix = "";
                       if (c.Type == Command.CmdType.Lua) prefix = "lua";
                       else if (c.Type == Command.CmdType.CSharp) prefix = "cs";
                       await e.Channel.SendMessage("Command name: `" + cmd + "`\nCreated: `" + c.CreationDate.ToShortDateString() + "` by `" + c.Creator + "`\nType: `" + c.Type + "`\nContent: ```" + prefix + "\n" + c.Content + "```");
                   }
               });
            svc.CreateCommand("list")
               .Description("Lists all defined functions")
               .Do(async e =>
               {
                   Session s = CreateSession(e.Server.Id);
                   string toSend = "```";
                   int i = 0;
                   foreach (string name in s.Cmds.Keys)
                   {
                       toSend += (++i) + ". " + name + "\n";
                       if (toSend.Length > 1900)
                       {
                           toSend += "```";
                           await e.Channel.SendMessage(toSend);
                           toSend = "```";
                       }
                   }
                   toSend += "```";
                   await e.Channel.SendMessage(toSend);
               });
            svc.CreateCommand("changelog")
               .Description("Enables/Disables showing changes to commands")
               .Parameter("action", ParameterType.Required)
               .Do(async e =>
               {
                   if (e.User.ServerPermissions.Administrator)
                   {
                       Session s = CreateSession(e.Server.Id);
                       string arg = e.Args[0].ToLowerInvariant();
                       if (arg == "enable")
                       {
                           s.ShowChanges = true;
                           await e.Channel.SendMessage("Changelog enabled for server " + e.Server.Name);
                       }
                       else if (arg == "disable")
                       {
                           s.ShowChanges = false;
                           await e.Channel.SendMessage("Changelog disabled for server " + e.Server.Name);
                       }
                       await s.SaveAsync();
                   }
                   else await e.Channel.SendMessage("You don't have sufficent permissons.");
               });
            svc.CreateCommand("inform")
               .Description("Remembers a message to send to a specific user when they go online")
               .Parameter("user", ParameterType.Required)
               .Parameter("content", ParameterType.Unparsed)
               .Do(async e =>
               {
                   User u = FindUser(e.Server, e.Args[0]);
                   if (u != null)
                   {
                       Session s = CreateSession(e.Server.Id);
                       string msg = DateTime.UtcNow.ToShortDateString() + " - " + e.Channel.Name + ": " + e.Args[1];
                       string removed = s.AddMessage(e.User.Id, u.Id, msg);
                       await e.Channel.SendMessage("Okay, I'll deliver the message when " + u.Nickname + " goes online");
                       if (!string.IsNullOrWhiteSpace(removed))
                           await e.Channel.SendMessage("This message was removed from the inbox:\n" + removed);
                       await s.SaveAsync();
                   }
                   else await e.Channel.SendMessage("No user " + e.Args[0] + " found on this server!");
               });
        }

        private async void UserUpdated(object sender, UserUpdatedEventArgs e)
        {
            if ((e.Before.Status == UserStatus.Offline || e.Before.Status == UserStatus.Idle) && (e.After.Status == UserStatus.Online))
            {
                Session s = CreateSession(e.Server.Id);
                if (s.Msgs.ContainsKey(e.After.Id))
                {
                    Inbox i = s.Msgs[e.After.Id];
                    Channel toSend = await dclient.CreatePrivateChannel(e.After.Id);
                    i.Deliver(e.Server, toSend);
                    s.Msgs.Remove(e.After.Id);
                    await s.SaveAsync();
                }
            }
        }

        private async void MessageRecieved(object sender, MessageEventArgs e)
        {
            if (e.Message.Text.StartsWith(">") && e.User.Id != dclient.CurrentUser.Id)
            {
                string command = e.Message.Text.Substring(1);
                int ix = command.IndexOf(' ');
                if (ix != -1) command = command.Remove(ix);
                command = command.ToLowerInvariant();
                Session s = CreateSession(e.Server.Id);
                if (s.Cmds.ContainsKey(command))
                {
                    Command c = s.Cmds[command];
                    try
                    {
                        await e.Channel.SendIsTyping();
                        string result = await System.Threading.Tasks.Task.Run(() => c.Run(new CmdParams(e)));
                        await e.Channel.SendMessage(result);
                    }
                    catch (Exception ex)
                    {
                        await e.Channel.SendMessage("Ooops, seems like an exception happened: " + ex.Message);
                        Log(ex.ToString(), true);
                    }
                }
            }
        }

        public static void Log(object sender, LogMessageEventArgs e)
        {
            using (StreamWriter sw = DuckData.LogFile.AppendText())
                sw.WriteLine("[" + e.Severity + "] [" + e.Source + "] " + e.Message);
        }

        public static void Log(string text, bool isException)
        {
            using (StreamWriter sw = DuckData.LogFile.AppendText())
                if (isException)
                {
                    sw.WriteLine("[EXCEPTION]");
                    sw.WriteLine(text);
                    sw.WriteLine();
                }
                else sw.WriteLine(text);
        }

        public static User FindUser(Server srv, string user)
        {
            if (!string.IsNullOrWhiteSpace(user))
                foreach (User u in srv.FindUsers(user))
                    return u;
            return null;
        }
    }
}