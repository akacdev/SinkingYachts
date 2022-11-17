# Sinking Yachts 🐟

![](https://raw.githubusercontent.com/actually-akac/SinkingYachts/master/SinkingYachts/icon.png)

A C# library for detecting Discord/Steam phishing links using the Sinking Yachts API. 



> **Warning**

> Sinking Yachts is currently in the process of being discontinued and replaced with the upstream [Fish Fish API](https://fishfish.gg).

> Expect this library to eventually become archived and marked as deprecated.

> Daily statistics have already been shut down.

## Usage
Available on NuGet as `SinkingYachts`, methods are available under the public class `YachtsClient`.

https://www.nuget.org/packages/SinkingYachts

## Features
- Made with **.NET 6**
- Fully async
- Access to a Discord-related phishing database of over `15 500` confirmed malicious domains
- Regex matching of domains and automatic phishing detection
- Different modes for storing and loading phishing domains
- Instant updates through **WebSocket events**
- Domain whitelisting to decrease false positives
- Customizable caching to decrease load

## Example Project
Under the `Example` directory you can find a working demo Discord bot that implements this library.
```rust
07.09. 19:13:59 [Discord] Discord.Net v3.8.0 (API v9)
07.09. 19:13:59 [Gateway] Connecting
07.09. 19:14:01 [Gateway] Connected
07.09. 19:14:02 [Bot] Ready to protect your server from 15601 phishing domains
07.09. 19:14:02 [Bot] Domains added within the past day: 8
07.09. 19:14:02 [Bot] Domains deleted within the past day: 0
07.09. 19:14:02 [Gateway] Ready
```

## Code Samples

### Check message content
```csharp
bool isPhishing = await Yachts.IsPhishing("hello https://hypesquadacademy-apply.ml");
//👉 True
```

### Check a domain
```csharp
bool isPhishing = await Yachts.IsPhishingDomain("warning-selectioneventhype.gq");
//👉 True
```

### Get the latest domains
```csharp
string[] domains = (await Yachts.GetRecent(TimeSpan.FromDays(1))).Where(x => x.Type == ChangeType.Add).SelectMany(x => x.Domains).ToArray();
//👉 steamcommunitysiv.top, wvwww-roblox.com, discord-download.win, steamcoumunity.eu, streamcummonity.com, streamcommunity.org, join-event-hypesquad.com, steamcommunityzowe.top
```

### Get the database size
```csharp
int size = await Yachts.GetDatabaseSize();
//👉 15601
```

## Available methods
- Task<Change[]> GetRecent(TimeSpan time)
- Task<Change[]> GetRecent(int seconds)
- Task<bool> IsPhishing(string content)
- Task<bool> IsPhishingDomain(string domain)
- Task<int> GetDatabaseSize()
- Task<string[]> GetPhishingDomains()

## Available events (requires `StorageMode.LocalWS`)
- EventHandler\<string> DomainAdded
- EventHandler\<string> DomainDeleted

## Missing domains
Found a Discord/Steam phishing domain that isn't yet present in the database? Send it into the `#domain-reports` channel on our Discord server or open an **issue** in this repository. 
  
## Resources
Need help, want to discuss phishing or have a suggestion? Feel free to join our Discord server: https://discord.gg/d63pvY28HU (temporarily closed)

- Official website: https://sinking.yachts
- Email: admin@fishfish.gg, sinkingyachts@gmail.com
- GitHub: https://github.com/SinkingYachts
- Blog: https://sinking.yachts/blog/