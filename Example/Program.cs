using Discord;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;
using SinkingYachts;
using System.Linq;

namespace Example
{
    public static class Program
    {
        private static readonly DiscordSocketClient Client = new (new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages
        });

        public static readonly StorageMode Mode = StorageMode.LocalWS;

        private static readonly YachtsClient Yachts = new(Mode, 3, "Example Bot");

        private static readonly string Token = Environment.GetEnvironmentVariable("BOT_TOKEN");

        public static async Task Main()
        {
            if (string.IsNullOrEmpty(Token))
            {
                await Logger.Log("Bot", $"No bot token has been configured. Please set one as the \"BOT_TOKEN\" environment variable.", LogSeverity.Critical);
                throw new("Empty bot token");
            }

            Client.Log += Logger.Log;
            Client.Ready += Ready;
            Client.MessageReceived += OnMessageReceived;

            try
            {
                await Client.LoginAsync(TokenType.Bot, Token);
                await Client.StartAsync();
            }
            catch (Exception ex)
            {
                await Logger.Log("Bot", $"Exception while trying to log in: {ex.GetType().Name} => {ex.Message}", LogSeverity.Critical);
                throw;
            }

            if (Mode == StorageMode.LocalWS)
            {
                Yachts.DomainAdded += (o, domain) =>
                {
                    Console.WriteLine($"New domain added: {domain}");
                };

                Yachts.DomainDeleted += (o, domain) =>
                {
                    Console.WriteLine($"New domain deleted: {domain}");
                };
            }

            await Task.Delay(-1);
        }

        public static async Task Ready()
        {
            await Logger.Log("Bot", $"Bot is ready to protect your server from {await Yachts.DatabaseSize()} phishing domains", LogSeverity.Info);

            Change[] changes = await Yachts.Recent(TimeSpan.FromDays(1));

            int added = changes.Count(x => x.Type == ChangeType.Add);
            int deleted = changes.Count(x => x.Type == ChangeType.Delete);

            await Logger.Log("Bot", $"Domains added within the past day: {added}", LogSeverity.Info);
            await Logger.Log("Bot", $"Domains deleted within the past day: {deleted}", LogSeverity.Info);
        }

        public static async Task OnMessageReceived(SocketMessage msg)
        {
            if (msg is not SocketUserMessage) return;
            if (msg.Channel is not SocketTextChannel) return;
            if (msg.Author.IsBot) return;

            if (await Yachts.IsPhishing(msg.Content))
            {
                await msg.DeleteAsync();
                await msg.Channel.SendMessageAsync("Phishing links are not allowed.");
            }
        }
    }
}