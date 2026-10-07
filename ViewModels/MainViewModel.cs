using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using EventViewerWPF.Models;
using EventViewerWPF.Services;
using Microsoft.Win32;

namespace EventViewerWPF.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly EventLogReaderService _readerService = new();
    private CancellationTokenSource? _cts;

    public MainViewModel()
    {
        AvailableLogNames = new ObservableCollection<string>
        {
            "Application",
            "System",
            "Security",
            "Setup",
            "Microsoft-Windows-Diagnostics-Performance/Operational"
        };

        Events = new ObservableCollection<EventLogItem>();
        FilteredEvents = CollectionViewSource.GetDefaultView(Events);
        FilteredEvents.Filter = FilterEventItem;

        LoadEventsCommand = new RelayCommand(async _ => await LoadEventsAsync(), _ => !IsLoading);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsLoading);
        ExportJsonCommand = new RelayCommand(async _ => await ExportJsonAsync(), _ => Events.Count > 0 && !IsLoading);
        ExportTextCommand = new RelayCommand(async _ => await ExportTextAsync(), _ => Events.Count > 0 && !IsLoading);
        SetTargetDateCommand = new RelayCommand(() => SetDate(new DateTime(2026, 9, 27)));
        SetTodayCommand = new RelayCommand(() => SetDate(DateTime.Today));
        RefreshChannelsCommand = new RelayCommand(async _ => await RefreshChannelsAsync(), _ => !IsLoading);

        StatusText = "Ready. Configure parameters and click 'Fetch Events'.";
    }

    public ObservableCollection<string> AvailableLogNames { get; }
    public ObservableCollection<EventLogItem> Events { get; }
    public ICollectionView FilteredEvents { get; }

    private string _server = "localhost";
    public string Server
    {
        get => _server;
        set { _server = value; OnPropertyChanged(nameof(Server)); }
    }

    private bool _useCustomCredentials;
    public bool UseCustomCredentials
    {
        get => _useCustomCredentials;
        set { _useCustomCredentials = value; OnPropertyChanged(nameof(UseCustomCredentials)); }
    }

    private string? _domain;
    public string? Domain
    {
        get => _domain;
        set { _domain = value; OnPropertyChanged(nameof(Domain)); }
    }

    private string? _username;
    public string? Username
    {
        get => _username;
        set { _username = value; OnPropertyChanged(nameof(Username)); }
    }

    public SecureString? SecurePassword { get; set; }

    private string _selectedLogName = "Application";
    public string SelectedLogName
    {
        get => string.IsNullOrWhiteSpace(_selectedLogName) ? "Application" : _selectedLogName;
        set
        {
            _selectedLogName = string.IsNullOrWhiteSpace(value) ? "Application" : value.Trim();
            OnPropertyChanged(nameof(SelectedLogName));
        }
    }

    private DateTime _fromDate = new(2026, 9, 27);
    public DateTime FromDate
    {
        get => _fromDate;
        set { _fromDate = value; OnPropertyChanged(nameof(FromDate)); }
    }

    private string _fromTime = "00:00:00";
    public string FromTime
    {
        get => _fromTime;
        set { _fromTime = value; OnPropertyChanged(nameof(FromTime)); }
    }

    private DateTime _toDate = new(2026, 9, 27);
    public DateTime ToDate
    {
        get => _toDate;
        set { _toDate = value; OnPropertyChanged(nameof(ToDate)); }
    }

    private string _toTime = "23:59:59";
    public string ToTime
    {
        get => _toTime;
        set { _toTime = value; OnPropertyChanged(nameof(ToTime)); }
    }

    private bool _includeCritical = true;
    public bool IncludeCritical
    {
        get => _includeCritical;
        set { _includeCritical = value; OnPropertyChanged(nameof(IncludeCritical)); }
    }

    private bool _includeError = true;
    public bool IncludeError
    {
        get => _includeError;
        set { _includeError = value; OnPropertyChanged(nameof(IncludeError)); }
    }

    private bool _includeWarning = true;
    public bool IncludeWarning
    {
        get => _includeWarning;
        set { _includeWarning = value; OnPropertyChanged(nameof(IncludeWarning)); }
    }

    private bool _includeInformation = true;
    public bool IncludeInformation
    {
        get => _includeInformation;
        set { _includeInformation = value; OnPropertyChanged(nameof(IncludeInformation)); }
    }

    private bool _includeVerbose;
    public bool IncludeVerbose
    {
        get => _includeVerbose;
        set { _includeVerbose = value; OnPropertyChanged(nameof(IncludeVerbose)); }
    }

    private int _maxRecords = 2000;
    public int MaxRecords
    {
        get => _maxRecords;
        set { _maxRecords = value; OnPropertyChanged(nameof(MaxRecords)); }
    }

    private string _searchQuery = "";
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            _searchQuery = value;
            OnPropertyChanged(nameof(SearchQuery));
            FilteredEvents.Refresh();
            OnPropertyChanged(nameof(FilteredCount));
        }
    }

    private EventLogItem? _selectedEvent;
    public EventLogItem? SelectedEvent
    {
        get => _selectedEvent;
        set { _selectedEvent = value; OnPropertyChanged(nameof(SelectedEvent)); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
    }

    private int _progressCount;
    public int ProgressCount
    {
        get => _progressCount;
        set { _progressCount = value; OnPropertyChanged(nameof(ProgressCount)); }
    }

    public int TotalCount => Events.Count;
    public int FilteredCount => FilteredEvents.Cast<object>().Count();

    public ICommand LoadEventsCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand ExportTextCommand { get; }
    public ICommand SetTargetDateCommand { get; }
    public ICommand SetTodayCommand { get; }
    public ICommand RefreshChannelsCommand { get; }

    public void SetDate(DateTime date)
    {
        FromDate = date.Date;
        FromTime = "00:00:00";
        ToDate = date.Date;
        ToTime = "23:59:59";
    }

    private async Task RefreshChannelsAsync()
    {
        try
        {
            StatusText = $"Discovering available log channels on {Server}...";
            var options = new EventLogFilterOptions
            {
                Server = Server,
                UseCustomCredentials = UseCustomCredentials,
                Domain = Domain,
                Username = Username,
                Password = SecurePassword
            };

            var channels = await _readerService.GetAvailableChannelsAsync(options);
            if (channels.Count > 0)
            {
                AvailableLogNames.Clear();
                foreach (var ch in channels)
                    AvailableLogNames.Add(ch);

                if (string.IsNullOrWhiteSpace(SelectedLogName) || !AvailableLogNames.Contains(SelectedLogName))
                {
                    SelectedLogName = "Application";
                }

                StatusText = $"Discovered {channels.Count} channel(s) on {Server}.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Channel discovery failed: {ex.Message}";
            MessageBox.Show($"Could not list log channels on '{Server}':\n\n{ex.Message}", "Channel Discovery", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool FilterEventItem(object obj)
    {
        if (obj is not EventLogItem item)
            return false;

        if (string.IsNullOrWhiteSpace(SearchQuery))
            return true;

        var query = SearchQuery.Trim();
        if (item.EventId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;
        if (item.Source.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;
        if (item.Level.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;
        if (item.Message.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;
        if (item.MachineName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        return false;
    }

    private async Task LoadEventsAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedLogName))
        {
            SelectedLogName = "Application";
        }

        if (!DateTime.TryParse($"{FromDate:yyyy-MM-dd} {FromTime}", out var fromDateTime))
        {
            MessageBox.Show("Invalid 'From' date/time format.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!DateTime.TryParse($"{ToDate:yyyy-MM-dd} {ToTime}", out var toDateTime))
        {
            MessageBox.Show("Invalid 'To' date/time format.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (fromDateTime > toDateTime)
        {
            MessageBox.Show("'From' time cannot be greater than 'To' time.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        ProgressCount = 0;
        StatusText = $"Connecting to {Server} (Channel: '{SelectedLogName}')...";
        Events.Clear();
        SelectedEvent = null;

        _cts = new CancellationTokenSource();
        var sw = Stopwatch.StartNew();

        try
        {
            var options = new EventLogFilterOptions
            {
                Server = Server,
                UseCustomCredentials = UseCustomCredentials,
                Domain = Domain,
                Username = Username,
                Password = SecurePassword,
                LogName = SelectedLogName,
                FromUtc = fromDateTime.ToUniversalTime(),
                ToUtc = toDateTime.ToUniversalTime(),
                IncludeCritical = IncludeCritical,
                IncludeError = IncludeError,
                IncludeWarning = IncludeWarning,
                IncludeInformation = IncludeInformation,
                IncludeVerbose = IncludeVerbose,
                MaxEvents = MaxRecords
            };

            var progress = new Progress<int>(count =>
            {
                ProgressCount = count;
                StatusText = $"Loaded {count} events...";
            });

            var list = await _readerService.ReadEventsAsync(options, progress, _cts.Token);

            foreach (var item in list)
            {
                Events.Add(item);
            }

            sw.Stop();
            StatusText = $"Successfully fetched {Events.Count} event(s) in {sw.Elapsed.TotalSeconds:F2}s.";
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(FilteredCount));

            if (Events.Count > 0)
            {
                SelectedEvent = Events[0];
            }
            else
            {
                StatusText = $"No events found in channel '{SelectedLogName}' for the specified date/time.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = $"Fetch cancelled by user. Loaded {Events.Count} event(s).";
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusText = "Access Denied.";
            MessageBox.Show(
                $"Unauthorized Access to server '{Server}':\n\n{ex.Message}\n\nTip: Enable 'Use Custom Credentials' and provide an Administrator username and password on the server.",
                "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to read events from '{Server}' channel '{SelectedLogName}':\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task ExportJsonAsync()
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"EventLog_{SelectedLogName}_{FromDate:yyyyMMdd}_{Server.Replace('.', '_')}.json"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                var itemsToExport = FilteredEvents.Cast<EventLogItem>().ToList();
                await ExportService.ExportToJsonAsync(itemsToExport, saveFileDialog.FileName);
                StatusText = $"Exported {itemsToExport.Count} items to JSON: {Path.GetFileName(saveFileDialog.FileName)}";
                MessageBox.Show($"Exported {itemsToExport.Count} event(s) to:\n{saveFileDialog.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export JSON: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task ExportTextAsync()
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"EventLog_{SelectedLogName}_{FromDate:yyyyMMdd}_{Server.Replace('.', '_')}.txt"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                var itemsToExport = FilteredEvents.Cast<EventLogItem>().ToList();
                await ExportService.ExportToTextAsync(itemsToExport, saveFileDialog.FileName);
                StatusText = $"Exported {itemsToExport.Count} items to TXT: {Path.GetFileName(saveFileDialog.FileName)}";
                MessageBox.Show($"Exported {itemsToExport.Count} event(s) to:\n{saveFileDialog.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export Text: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
