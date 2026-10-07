using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventViewerWPF.Models;

namespace EventViewerWPF.Services;

public static class ExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task ExportToJsonAsync(IEnumerable<EventLogItem> items, string filePath)
    {
        var list = items.ToList();
        var json = JsonSerializer.Serialize(list, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
    }

    public static async Task ExportToTextAsync(IEnumerable<EventLogItem> items, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine($" Windows Event Log Export - Generated at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("================================================================================");
        sb.AppendLine();

        int index = 1;
        foreach (var item in items)
        {
            sb.AppendLine($"[{index++}] ----------------------------------------------------------------------");
            sb.AppendLine($"TimeCreated : {item.TimeCreated:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Level       : {item.Level}");
            sb.AppendLine($"Event ID    : {item.EventId}");
            sb.AppendLine($"Source      : {item.Source}");
            sb.AppendLine($"Log Name    : {item.LogName}");
            sb.AppendLine($"Machine     : {item.MachineName}");
            if (!string.IsNullOrWhiteSpace(item.TaskCategory))
            {
                sb.AppendLine($"Task        : {item.TaskCategory}");
            }
            sb.AppendLine("Message     :");
            sb.AppendLine(item.Message);
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }
}
