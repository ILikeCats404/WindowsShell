using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Windows.System;

namespace DesktopWallpaper
{
    
    public static class DiscordB    
    {
        public static DiscordSocketClient client = new DiscordSocketClient();
        public static ulong selectedId = 0;
        public static async Task start()
        {
            //make config and start bot
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All
            };
            client = new DiscordSocketClient(config);
            await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("TOKEN"));
            await client.StartAsync();
            Channels = Config.channels.Split(", ").Select(ulong.Parse).ToList();
        }
        /// <summary>
        /// returns the messages in selected channel as html string.
        /// </summary>
        /// <returns></returns>
        public static async Task<List<string>> GetMessages()
        {

            Debug.WriteLine("running. may not be thread safe!");


            List<string> messages = new List<string>
                {
                "No messages found."
                };
            try
            {
                IEnumerable<IMessage> msg;


                var chan = await client.GetChannelAsync(selectedId) as ITextChannel;
                if (chan == null)
                {
                    var user = await client.GetUserAsync(selectedId);
                    var dmchan = await user.CreateDMChannelAsync();
                    msg = await dmchan.GetMessagesAsync(100).FlattenAsync();
                }
                else
                {
                    msg = await chan.GetMessagesAsync(100).FlattenAsync();
                }



                    messages.Clear();
                foreach (var m in msg)
                {

                    //get profile pic and add html for it before the message content
                    var outmsg = $"<img src='{m.Author.GetAvatarUrl()}' width='32' height='32' style='border-radius:50%;vertical-align:middle;margin-right:8px;' /><strong>{m.Author.Username}</strong><span style='color:rgba(255,255,255,0.5);font-size:11px;margin-left:6px;'>{m.CreatedAt.DateTime:ddd MMM d • h:mm tt}</span><br><span style='margin-left:40px;display:inline-block;'>{m.Content}</span>";
                    messages.Add(outmsg);
                }

            }
            catch (Exception ex)
            {
                //supress
                Debug.WriteLine(ex.Message + ex.StackTrace);
            }
            return messages;
        }
        public static async Task SendMessage(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    var chan = await client.GetChannelAsync(selectedId) as ITextChannel;
                    if (chan != null)
                        await chan.SendMessageAsync(text);
                    else
                    {
                        var user = await client.GetUserAsync(selectedId);
                        var dmchan = await user.CreateDMChannelAsync();
                        await dmchan.SendMessageAsync(text);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DISC] Error while sending message to discord \n [DISC] EX {ex.Message}");
                }
            }
        }

        public static async Task<string> GetAlert(string msg)
        {
            return $"<img src='https://banner2.cleanpng.com/20180418/gfq/kisspng-computer-icons-computer-software-system-integratio-5ad7a7e3584f06.7555428615240826593617.jpg' width='32' height='32' style='border-radius: 50%; margin-right: 8px;' /> <strong>SYSTEM</strong>: {msg}<br>";
        }
        public static List<ulong> Channels = new List<ulong>
        {

        };
        public static async Task<string> GetNameAsync(ulong id)
        {
            // 1. Fast cache lookup (never hangs)
            var cachedChannel = client.GetChannel(id);
            if (cachedChannel != null)
                return (cachedChannel as IChannel)?.Name ?? "<Channel>";

            var cachedUser = client.GetUser(id);
            if (cachedUser != null)
                return cachedUser.Username;

            // 2. REST fallback with timeout safety
            try
            {
                var userTask = client.Rest.GetUserAsync(id);
                var completed = await Task.WhenAny(userTask, Task.Delay(3000));

                if (completed == userTask)
                {
                    var user = await userTask;
                    if (user != null)
                        return user.Username;
                }
            }
            catch
            {
                // ignore REST errors
            }

            return "<Unknown>";
        }
    }
}
