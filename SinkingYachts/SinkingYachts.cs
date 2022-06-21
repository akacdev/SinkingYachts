using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;

namespace SinkingYachts
{
    /// <summary>
    /// <para>Storage Mode sets the different modes of storing and loading phishing domains.</para>
    /// <br><seealso cref="Remote"/></br> is the easiest to use, but sends a lot of HTTP requests.
    /// <br><seealso cref="Local"/></br> takes up a lot of memory, and some domains can be missed if they are aren't synced yet.
    /// <br><seealso cref="LocalWS"/></br> is the most powerful option if you want to precisely collect domains.
    /// </summary>
    public enum StorageMode
    {
        /// <summary>
        /// Domains are only cached after seen first. This option sends an API request for every non-cached domain.
        /// </summary>
        Remote,
        /// <summary>
        /// All domains are downloaded and cached immediately. The cache is updated every 15 minutes.
        /// </summary>
        Local,
        /// <summary>
        /// <para>Same as <seealso cref="Local"/>, but persists a WebSocket connection to Sinking Yachts, allowing it to receive new domains in real time.</para>
        /// <br>This makes sure that no domains slip through the detection due to not being synced yet.</br>
        /// </summary>
        LocalWS
    }

    /// <summary>
    /// An enum holding the type of a change. Can be either <b>add</b> or <b>delete</b>.
    /// </summary>
    public enum ChangeType
    {
        [JsonPropertyName("add")]
        Add,

        [JsonPropertyName("delete")]
        Delete
    }

    /// <summary>
    /// A little class that implements the domain add/update structure.
    /// </summary>
    public class Change
    {
        /// <summary>
        /// The type of the event. Can be either <b>add</b> or <b>delete</b>.
        /// </summary>
        [JsonPropertyName("type")]
        public ChangeType Type { get; set; }

        /// <summary>
        /// A list of domains that have changed in this event. This is always one domain, but it's sent in an array for potential bulk imports.
        /// </summary>
        [JsonPropertyName("domains")]
        public string[] Domains { get; set; }
    }

    /// <summary>
    /// The main class to run anti-phishing checks.
    /// </summary>
    public class YachtsClient
    {
        /// <summary>
        /// The official domains of Discord, Steam, Roblox and Github. If a sent domain is present in this array, it's immediately returned as safe.
        /// </summary>
        private static readonly string[] OfficialDomains = new[]
        {
            "discord.com",
            "discord.gg",
            "discordapp.com",
            "discordapp.net",
            "discord.media",
            "discordstatus.com",
            "steamcommunity.com",
            "steamgames.com",
            "steampowered.com",
            "valve.net",
            "valvesoftware.com",
            "roblox.com",
            "www.roblox.com",
            "github.com",
            "githubusercontent.com",
            "raw.githubusercontent.com"
        };
        /// <summary>
        /// A cache to store API response values.
        /// </summary>
        public readonly Dictionary<string, bool> Cache = new();

        private readonly HttpClient Client;
        private readonly Regex UrlRegex = new(@"(http|https):\/\/([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:\/~+#-]*[\w@?^=%&\/~+#-])", RegexOptions.Compiled);
        
        private const int RefreshInterval = 1000 * 60 * 15;
        private static Timer Refresher;
        private const string Api = "https://phish.sinking.yachts";
        private const int Version = 2;

        private static Connection Con;

        private readonly string _identity;
        private readonly TimeSpan _cachePeriod;
        private readonly StorageMode _mode;

        public EventHandler<string> DomainAdded;
        public EventHandler<string> DomainDeleted;

        /// <summary>
        /// Creates a new instance of the Sinking Yachts client.
        /// </summary>
        /// <param name="mode">The domain storage mode to use.</param>
        /// <param name="identity">A short string identifying your bot application. By default this is the name of your project.</param>
        /// <param name="cachePeriodHours">How long in hours should be API responses cached for.</param>
        public YachtsClient(StorageMode mode, int cachePeriodHours = 3, string identity = null)
        {
            _mode = mode;
            _identity = $"https://github.com/actually-akac/SinkingYachts | {identity ?? Assembly.GetEntryAssembly().GetName().Name}";
            _cachePeriod = TimeSpan.FromHours(cachePeriodHours);

            Client = new();
            Client.DefaultRequestHeaders.Add("X-Identity", _identity);

            switch (_mode)
            {
                case StorageMode.Local:
                    {
                        UpdateCache();

                        Refresher = new();
                        Refresher.Interval = RefreshInterval;
                        Refresher.Elapsed += (o, e) => UpdateCache();
                        Refresher.Start();

                        break;
                    }
                case StorageMode.LocalWS:
                    {
                        UpdateCache();

                        Refresher = new();
                        Refresher.Interval = RefreshInterval;
                        Refresher.Elapsed += (o, e) => UpdateCache();
                        Refresher.Start();

                        Con = new Connection(_identity);

                        Con.DomainAdded += (sender, domain) => Cache[domain] = true;
                        Con.DomainAdded += (sender, domain) => DomainAdded(sender, domain);

                        Con.DomainDeleted += (sender, domain) => Cache[domain] = false;
                        Con.DomainDeleted += (sender, domain) => DomainDeleted(sender, domain);

                        break;
                    }
            }
        }

        /// <summary>
        /// Updates the cache with fresh domains. Not used with <seealso cref="StorageMode.Local"/>.
        /// </summary>
        private async void UpdateCache()
        {
            string[] all = await GetPhishingDomains();

            foreach (string key in Cache.Keys)
            {
                if (all.Contains(key)) Cache[key] = true;
                else Cache[key] = false;
            }

            foreach (string domain in all)
            {
                bool exists = Cache.ContainsKey(domain);

                if (!exists) Cache[domain] = true;
            }
        }

        /// <summary>
        /// Checks whether a provided Discord message content contains phishing domains.
        /// </summary>
        /// <param name="content">The message content to check.</param>
        /// <returns></returns>
        public async Task<bool> IsPhishing(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;

            MatchCollection matches = UrlRegex.Matches(content);

            foreach (Match match in matches)
            {
                bool success = Uri.TryCreate(match.Value, UriKind.Absolute, out Uri uri);
                if (!success) continue;

                if (await IsPhishingDomain(uri.Host)) return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether a provided domain is known to be a phish site.
        /// </summary>
        /// <param name="domain">The domain to check.</param>
        /// <returns></returns>
        public async Task<bool> IsPhishingDomain(string domain)
        {
            if (OfficialDomains.Contains(domain)) return false;
            if (Cache.TryGetValue(domain, out bool output)) return output;

            HttpResponseMessage res = await Client.GetAsync($"{Api}/v{Version}/check/{domain}");
            string content = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode) throw new($"Unexpected response while checking {domain}: {res.StatusCode}, {content}");

            output = bool.Parse(content);
            Cache[domain] = output;

            Task remover = Task.Delay(_cachePeriod).ContinueWith(x =>
            {
                Cache.Remove(domain);
            });

            return output;
        }

        /// <summary>
        /// Gets the entire list of all known phishing domains.
        /// </summary>
        /// <returns></returns>
        public async Task<string[]> GetPhishingDomains()
        {
            HttpResponseMessage res = await Client.GetAsync($"{Api}/v{Version}/text");
            string content = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode) throw new($"Unexpected response while fetching all phishing domains: {res.StatusCode}, {content}");

            return content.Split('\n');
        }

        /// <summary>
        /// Fetches the total amount of flagged domains in the database.
        /// </summary>
        public async Task<int> DatabaseSize()
        {
            HttpResponseMessage res = await Client.GetAsync($"{Api}/v{Version}/dbsize");
            string content = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode) throw new($"Unexpected response while fetching databse size: {res.StatusCode}, {content}");

            bool success = int.TryParse(content, out int result);

            if (!success)
                throw new($"Couldn't parse string {content} as domain count.");

            return result;
        }

        /// <summary>
        /// Fetches the domains added or deleted within the last X seconds.
        /// </summary>
        public async Task<Change[]> Recent(int seconds)
        {
            if (seconds > 604800) throw new ArgumentException("Maximum value is 604800 seconds (7 days).", nameof(seconds));
            if (seconds <= 0) throw new ArgumentException("Argument has to be positive.", nameof(seconds));

            HttpResponseMessage res = await Client.GetAsync($"{Api}/v{Version}/recent/{seconds}");
            string content = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode) throw new($"Unexpected response while fetching recent changes: {res.StatusCode}, {content}");

            try
            {
                JsonSerializerOptions opt = new();
                opt.Converters.Add(new JsonStringEnumConverter());

                return JsonSerializer.Deserialize<Change[]>(content, opt);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to deserialize database changes: {ex.GetType().Name} => {ex.Message}\nJSON: {content}");
            }
        }

        /// <summary>
        /// Fetches the domains added or deleted within the provided TimeSpan.
        /// </summary>
        public async Task<Change[]> Recent(TimeSpan time)
        {
            return await Recent((int)time.TotalSeconds);
        }
    }
}