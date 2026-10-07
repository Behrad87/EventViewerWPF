using System.Text.Json.Serialization;

namespace EventViewerWPF.Models;

public class EventLogItem
{
    public DateTime? TimeCreated { get; set; }
    public string Level { get; set; } = "Information";
    public int EventId { get; set; }
    public string Source { get; set; } = "";
    public string LogName { get; set; } = "";
    public string? MachineName { get; set; }
    public string? TaskCategory { get; set; }
    public string? Keywords { get; set; }
    public string Message { get; set; } = "";

    [JsonIgnore]
    public string? Xml { get; set; }

    [JsonIgnore]
    public string LevelColor => Level.ToLowerInvariant() switch
    {
        "critical" => "#E53935", // Red
        "error" => "#EF5350",    // Soft Red
        "warning" => "#FFA726",  // Orange
        "information" => "#42A5F5", // Blue
        "verbose" => "#AB47BC",  // Purple
        _ => "#9E9E9E"           // Gray
    };
}
