# EventViewerWPF

A modern WPF application for reading, inspecting, and exporting Windows Event Viewer logs from remote servers (e.g. `10.131.3.34`) or local machines.

## Key Features

1. **Remote & Local Machine Connection**:
   - Query remote servers (e.g., `10.131.3.34`) via Windows RPC (`EventLogSession`).
   - Supports custom Windows Administrator credentials (`Domain`, `Username`, `Password`) or current Windows logon session.
   - Also supports `localhost` / `.` for local machine investigation.

2. **Targeted Date & Time Range Filtering**:
   - Preset with one-click shortcut for **`2026-09-27`** (`From: 2026-09-27 00:00:00` to `To: 2026-09-27 23:59:59`).
   - Sizable event limit (`Max Events`).

3. **Severity Level Filters**:
   - Checkboxes for `Critical`, `Error`, `Warning`, `Information`, and `Verbose`.

4. **Live In-Memory Search**:
   - Filter loaded events in real-time by keyword, Event ID, Provider/Source name, or message text.

5. **Exporting Capabilities**:
   - **Save as JSON**: Exports full structured JSON format with complete metadata, timestamps, and messages.
   - **Save as Text**: Exports formatted, human-readable text file report.

6. **Event Inspector**:
   - Formatted description viewer.
   - Raw XML data tab.
   - Metadata and properties panel.

## How to Run

### Via dotnet CLI:
```powershell
dotnet run --project C:\repos\EventViewerWPF\EventViewerWPF.csproj
```

### Via Executable:
```powershell
C:\repos\EventViewerWPF\bin\Debug\net10.0-windows\EventViewerWPF.exe
```

## Remote Connection Requirements
When connecting to a remote server like `10.131.3.34`:
- Ensure the Windows Remote Event Log Management firewall rules are open on the target server (port 135 & RPC dynamic ports).
- If your current workstation user is not an administrator on the remote server, check **Use Custom Credentials** and enter the remote Administrator username and password.
