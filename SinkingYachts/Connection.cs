using System;
using System.Text.Json;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SinkingYachts
{
    public class Connection
    {
        public const string URL = "wss://phish.sinking.yachts/feed";
        public const int ReconnectionInterval = 10000;

        private ClientWebSocket WS;
        private readonly CancellationTokenSource Source = new();

        public bool Connected = false;

        public EventHandler<string> DomainAdded;
        public EventHandler<string> DomainDeleted;

        private readonly string _identity;

        public Connection(string identity)
        {
            _identity = identity;

            Connect();
        }

        public async void Connect()
        {
            Connected = false;
            WS = new ClientWebSocket();
            WS.Options.SetRequestHeader("X-Identity", $"https://github.com/actually-akac/SinkingYachts | {_identity}");

            try
            {
                await WS.ConnectAsync(new Uri(URL), Source.Token);
            }
            catch
            {
                Connected = false;
                await Task.Delay(ReconnectionInterval);
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
                        if (result.EndOfMessage)
                            break;
                    }
                    catch { break; };
                }

                if (offset != 0) OnMessage(Encoding.UTF8.GetString(receiveBuffer, 0, offset));
            }

            Connected = false;
            await Task.Delay(ReconnectionInterval);
            Connect();
        }

        public void OnMessage(string msg)
        {
            Change data = JsonSerializer.Deserialize<Change>(msg);

            foreach (string domain in data.Domains)
            {
                if (data.Type == ChangeType.Add) DomainAdded.Invoke(this, domain);
                else if (data.Type == ChangeType.Delete) DomainDeleted.Invoke(this, domain);
            }
        }
    }
}