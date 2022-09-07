using System;
using System.Text.Json;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace SinkingYachts
{
    /// <summary>
    /// The class for connecting and receiving messages from the real-time WebSocket server.
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// The WebSocket URI to connect to.
        /// </summary>
        public static readonly Uri Feed = new("wss://phish.sinking.yachts/feed");

        /// <summary>
        /// How long to wait before reconnecting after a connection is lost.
        /// </summary>
        public const int ReconnectionDelay = 10000;

        private ClientWebSocket WS;
        private readonly CancellationTokenSource Source = new();

        /// <summary>
        /// Whether there is currently a connection to the phishing feed.
        /// </summary>
        public bool Connected = false;

        /// <summary>
        /// Executes whenever a phishing domain is added into the database.
        /// </summary>
        public EventHandler<string> DomainAdded;

        /// <summary>
        /// Executes whenever a phishing domain is removed from the database.
        /// </summary>
        public EventHandler<string> DomainDeleted;

        private readonly string Identity;

        /// <summary>
        /// Default constructor for the connection class.
        /// </summary>
        /// <param name="identity"></param>
        public Connection(string identity)
        {
            Identity = identity;

            Connect();
        }

        /// <summary>
        /// Connects to the remote WebSocket server to start receiving updates.
        /// </summary>
        public async void Connect()
        {
            Connected = false;
            WS = new ClientWebSocket();
            WS.Options.SetRequestHeader("X-Identity", Identity);

            try
            {
                await WS.ConnectAsync(Feed, Source.Token);
            }
            catch
            {
                Connected = false;
                await Task.Delay(ReconnectionDelay);
                Connect();
                return;
            }

            while (WS.State == WebSocketState.Open)
            {
                Connected = true;
                var receiveBuffer = new byte[1024];
                var offset = 0;

                while (true)
                {
                    try
                    {
                        ArraySegment<byte> bytesReceived = new(receiveBuffer, offset, receiveBuffer.Length);

                        WebSocketReceiveResult result = await WS.ReceiveAsync(bytesReceived, Source.Token);
                        offset += result.Count;

                        if (result.EndOfMessage) break;
                    }
                    catch { break; };
                }

                if (offset != 0) OnMessage(Encoding.UTF8.GetString(receiveBuffer, 0, offset));
            }

            Connected = false;
            await Task.Delay(ReconnectionDelay);
            Connect();
        }

        /// <summary>
        /// Called when a WebSocket message is received.
        /// </summary>
        public void OnMessage(string msg)
        {
            Change data;

            try
            {
                JsonSerializerOptions opt = new();
                opt.Converters.Add(new JsonStringEnumConverter());

                data = JsonSerializer.Deserialize<Change>(msg, opt);
            }
            catch (Exception ex)
            {
                throw new($"Failed to deserialize database change: {ex.GetType().Name} => {ex.Message}\nMessage: {msg}");
            }

            if (data.Domains is null) throw new($"Domains in the update event are null.\nMessage: {msg}");

            foreach (string domain in data.Domains)
            {
                if (data.Type == ChangeType.Add) DomainAdded.Invoke(this, domain);
                else if (data.Type == ChangeType.Delete) DomainDeleted.Invoke(this, domain);
            }
        }
    }
}