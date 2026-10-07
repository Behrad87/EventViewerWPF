using System.Diagnostics.Eventing.Reader;
using System.Security;
using EventViewerWPF.Models;

namespace EventViewerWPF.Services;

public class EventLogFilterOptions
{
    public string Server { get; set; } = "10.131.3.34";
    public string? Domain { get; set; }
    public string? Username { get; set; }
    public SecureString? Password { get; set; }
    public bool UseCustomCredentials { get; set; }

    public string LogName { get; set; } = "Application";
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }

    public bool IncludeCritical { get; set; } = true;
    public bool IncludeError { get; set; } = true;
    public bool IncludeWarning { get; set; } = true;
    public bool IncludeInformation { get; set; } = true;
    public bool IncludeVerbose { get; set; } = false;

    public int MaxEvents { get; set; } = 2000;
}

public class EventLogReaderService
{
    private static EventLogSession CreateSession(EventLogFilterOptions options)
    {
        var server = options.Server?.Trim();

        if (string.IsNullOrWhiteSpace(server) ||
            server.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            server == ".")
        {
            return EventLogSession.GlobalSession;
        }

        if (options.UseCustomCredentials && !string.IsNullOrWhiteSpace(options.Username))
        {
            return new EventLogSession(
                server,
                string.IsNullOrWhiteSpace(options.Domain) ? null : options.Domain.Trim(),
                options.Username.Trim(),
                options.Password,
                SessionAuthentication.Default);
        }

        return new EventLogSession(server);
    }

    public Task<List<string>> GetAvailableChannelsAsync(EventLogFilterOptions options)
    {
        return Task.Run(() =>
        {
            var session = CreateSession(options);
            var names = session.GetLogNames()
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n)
                .ToList();

            var prioritized = new List<string> { "Application", "System", "Security", "Setup" };
            var result = prioritized
                .Concat(names.Except(prioritized, StringComparer.OrdinalIgnoreCase))
                .ToList();

            return result;
        });
    }

    public async Task<List<EventLogItem>> ReadEventsAsync(
        EventLogFilterOptions options,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var results = new List<EventLogItem>();
            var session = CreateSession(options);

            var channel = string.IsNullOrWhiteSpace(options.LogName) ? "Application" : options.LogName.Trim();

            var xpathQuery = BuildXPathQuery(options);
            var eventQuery = new EventLogQuery(channel, PathType.LogName, xpathQuery)
            {
                Session = session,
                ReverseDirection = true
            };

            using var reader = new EventLogReader(eventQuery);
            int count = 0;

            for (var record = reader.ReadEvent(); record != null; record = reader.ReadEvent())
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (record)
                {
                    var item = new EventLogItem
                    {
                        TimeCreated = record.TimeCreated?.ToLocalTime(),
                        LogName = channel,
                        Source = record.ProviderName ?? "",
                        EventId = record.Id,
                        MachineName = record.MachineName,
                        TaskCategory = record.TaskDisplayName,
                        Level = record.Level switch
                        {
                            1 => "Critical",
                            2 => "Error",
                            3 => "Warning",
                            4 => "Information",
                            5 => "Verbose",
                            _ => record.LevelDisplayName ?? record.Level?.ToString() ?? "Unknown"
                        },
                        Message = SafeFormatMessage(record),
                        Xml = SafeToXml(record)
                    };

                    results.Add(item);
                    count++;

                    if (count % 25 == 0)
                    {
                        progress?.Report(count);
                    }

                    if (options.MaxEvents > 0 && count >= options.MaxEvents)
                    {
                        break;
                    }
                }
            }

            progress?.Report(count);
            return results;
        }, cancellationToken);
    }

    private static string BuildXPathQuery(EventLogFilterOptions options)
    {
        var levels = new List<string>();
        if (options.IncludeCritical) levels.Add("Level=1");
        if (options.IncludeError) levels.Add("Level=2");
        if (options.IncludeWarning) levels.Add("Level=3");
        if (options.IncludeInformation) levels.Add("Level=4");
        if (options.IncludeVerbose) levels.Add("Level=5");

        var levelClause = levels.Count switch
        {
            0 => "Level=0",
            _ => $"({string.Join(" or ", levels)})"
        };

        var fromStr = options.FromUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var toStr = options.ToUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        var timeClause = $"TimeCreated[@SystemTime >= '{fromStr}' and @SystemTime <= '{toStr}']";

        return $"*[System[{levelClause} and {timeClause}]]";
    }

    private static string SafeFormatMessage(EventRecord record)
    {
        try
        {
            var desc = record.FormatDescription();
            if (!string.IsNullOrWhiteSpace(desc))
                return desc;
        }
        catch { }

        try
        {
            if (record.Properties is { Count: > 0 } props)
            {
                var values = props.Select(p => p.Value?.ToString() ?? "");
                return string.Join(" | ", values);
            }
        }
        catch { }

        return "(No formatted description available)";
    }

    private static string? SafeToXml(EventRecord record)
    {
        try
        {
            return record.ToXml();
        }
        catch
        {
            return null;
        }
    }
}
