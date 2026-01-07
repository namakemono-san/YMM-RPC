using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using DiscordRPC;
using DiscordRPC.Logging;
using YukkuriMovieMaker.Plugin;
using YmmRPC.Settings;

namespace YmmRPC;

[PluginDetails(AuthorName = "namakemono-san", ContentId = "")]
// ReSharper disable once ClassNeverInstantiated.Global
public class YmmRpcPlugin : IPlugin, IDisposable
{
    public string Name => "YMM-RPC";

    private const string ClientIdLite = "1455192227734098075";
    private const string ClientIdNormal = "1353376132732420136";
    private const string Version = "0.3.1";
    private const int UpdateIntervalMs = 15000;

    private static readonly object _lock = new();
    private static DiscordRpcClient? _client;
    private static Timer? _updateTimer;
    private static DateTime _startTime;
    private static bool? _isLiteEdition;
    private bool _disposed;

    public YmmRpcPlugin()
    {
        _startTime = DateTime.UtcNow;
        InitializeClient();
        StartUpdateTimer();
    }

    private static void InitializeClient()
    {
        DiscordRpcClient? newClient = null;
        bool shouldInitialize = false;
        
        lock (_lock)
        {
            if (_client is { IsDisposed: false }) return;
        }

        var clientId = GetIsLiteEdition() ? ClientIdLite : ClientIdNormal;

        newClient = new DiscordRpcClient(clientId)
        {
            Logger = new ConsoleLogger { Level = LogLevel.Warning }
        };

        newClient.OnReady += (_, e) => Console.WriteLine($"[YMM-RPC] Connected: {e.User.Username}");
        newClient.OnError += (_, e) => Console.WriteLine($"[YMM-RPC] Error: {e.Message}");

        lock (_lock)
        {
            // Double-check that client wasn't initialized by another thread
            if (_client is { IsDisposed: false })
            {
                // Another thread initialized it, dispose the one we created
                newClient.Dispose();
                return;
            }
            
            _client = newClient;
            shouldInitialize = true;
        }

        if (shouldInitialize)
        {
            try
            {
                newClient.Initialize();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[YMM-RPC] Failed to initialize client: {ex.Message}");
                // Clear the client on initialization failure
                lock (_lock)
                {
                    _client = null;
                }
                newClient.Dispose();
            }
        }
    }

    private static void StartUpdateTimer()
    {
        Timer? oldTimer = null;
        Timer? newTimer = null;
        
        try
        {
            newTimer = new Timer(_ =>
            {
                SafeUpdatePresence();
            }, null, UpdateIntervalMs, UpdateIntervalMs);
            
            lock (_lock)
            {
                oldTimer = _updateTimer;
                _updateTimer = newTimer;
            }
            
            oldTimer?.Dispose();
        }
        catch
        {
            // If timer creation or assignment failed, clean up
            newTimer?.Dispose();
            throw;
        }
    }

    private static void SafeUpdatePresence()
    {
        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                UpdatePresence();
                return;
            }
            dispatcher.Invoke(UpdatePresence);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YMM-RPC] Update error: {ex.Message}");
        }
    }

    private static void UpdatePresence()
    {
        DiscordRpcClient? client;
        bool isInitialized;
        
        lock (_lock)
        {
            client = _client;
            isInitialized = client is { IsInitialized: true };
        }
        
        if (!isInitialized) return;

        // At this point, client is guaranteed to be non-null and initialized
        var settings = YmmRpcSettings.Default;

        if (!settings.IsEnabled)
        {
            client!.ClearPresence();
            return;
        }

        var presence = settings.CustomRpcEnabled
            ? BuildCustomPresence(settings)
            : BuildDefaultPresence();

        client!.SetPresence(presence);
    }

    private static string? GetCurrentProjectName()
    {
        try
        {
            var mainWindow = Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(w =>
                    string.Equals(
                        w.GetType().FullName,
                        "YukkuriMovieMaker.Views.MainView",
                        StringComparison.Ordinal));

            if (mainWindow?.DataContext == null) return null;

            var vmType = mainWindow.DataContext.GetType();
            var filePathProp = vmType.GetProperty("ProjectFilePath", BindingFlags.Public | BindingFlags.Instance);

            if (filePathProp == null) return null;

            var filePathValue = filePathProp.GetValue(mainWindow.DataContext);
            var filePath = ExtractValue<string>(filePathValue);

            return string.IsNullOrEmpty(filePath) ? null : Path.GetFileNameWithoutExtension(filePath);
        }
        catch
        {
            return null;
        }
    }

    private static T? ExtractValue<T>(object? obj)
    {
        if (obj == null) return default;
        if (obj is T directValue) return directValue;

        var valueProperty = obj.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        if (valueProperty == null) return default;

        var innerValue = valueProperty.GetValue(obj);
        if (innerValue is T typedValue) return typedValue;
        return innerValue != null ? ExtractValue<T>(innerValue) : default;
    }

    private static RichPresence BuildDefaultPresence()
    {
        var settings = YmmRpcSettings.Default;
        var isLite = GetIsLiteEdition();
        
        string details;
        if (settings.IsShowProject)
        {
            var projectName = GetCurrentProjectName();
            var displayName = projectName ?? "無題";
            details = $"{displayName}.ymmp を編集中...";
        }
        else
        {
            details = "動画を編集中...";
        }

        return new RichPresence
        {
            Details = details,
            State = isLite ? "Working on YMM4 Lite" : "Working on YMM4",
            Assets = new Assets
            {
                LargeImageKey = "icon",
                LargeImageText = $"YMM-RPC v{Version}"
            },
            Timestamps = new Timestamps { Start = _startTime }
        };
    }

    private static RichPresence BuildCustomPresence(YmmRpcSettings settings)
    {
        var projectName = GetCurrentProjectName();
        var displayName = projectName != null ? $"{projectName}.ymmp" : "無題.ymmp";

        var details = ReplacePlaceholders(settings.CustomRpcDetails, displayName);
        var state = ReplacePlaceholders(settings.CustomRpcState, displayName);
        var largeImageText = ReplacePlaceholders(settings.CustomRpcLargeImageText, displayName);
        var smallImageText = ReplacePlaceholders(settings.CustomRpcSmallImageText, displayName);

        var presence = new RichPresence
        {
            Details = NullIfEmpty(details),
            State = NullIfEmpty(state),
            Assets = new Assets
            {
                LargeImageKey = string.IsNullOrEmpty(settings.CustomRpcLargeImageKey) ? "icon" : settings.CustomRpcLargeImageKey,
                LargeImageText = NullIfEmpty(largeImageText),
                SmallImageKey = NullIfEmpty(settings.CustomRpcSmallImageKey),
                SmallImageText = NullIfEmpty(smallImageText)
            },
            Timestamps = new Timestamps { Start = _startTime }
        };

        if (!settings.CustomRpcEnableButtons) return presence;

        var buttons = new List<Button>(2);

        if (!string.IsNullOrEmpty(settings.CustomRpcButton1Label) &&
            !string.IsNullOrEmpty(settings.CustomRpcButton1Url))
        {
            buttons.Add(new Button { Label = settings.CustomRpcButton1Label, Url = settings.CustomRpcButton1Url });
        }

        if (!string.IsNullOrEmpty(settings.CustomRpcButton2Label) &&
            !string.IsNullOrEmpty(settings.CustomRpcButton2Url))
        {
            buttons.Add(new Button { Label = settings.CustomRpcButton2Label, Url = settings.CustomRpcButton2Url });
        }

        if (buttons.Count > 0)
            presence.Buttons = buttons.ToArray();

        return presence;
    }

    private static string? ReplacePlaceholders(string? input, string displayName)
    {
        return string.IsNullOrEmpty(input) ? input : input.Replace("{project}", displayName);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    private static bool GetIsLiteEdition()
    {
        // Fast path - value already cached (volatile read)
        if (_isLiteEdition.HasValue) return _isLiteEdition.Value;

        // Slow path - determine if Lite edition (outside lock to prevent contention)
        var exeName = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        bool isLite;
        
        if (exeName.Contains("Lite", StringComparison.OrdinalIgnoreCase))
        {
            isLite = true;
        }
        else
        {
            try
            {
                isLite = Application.Current?.Dispatcher?.Invoke(() =>
                {
                    var title = Application.Current?.MainWindow?.Title ?? "";
                    return title.Contains("Lite", StringComparison.OrdinalIgnoreCase);
                }) ?? false;
            }
            catch
            {
                isLite = false;
            }
        }

        // Double-check and set value
        lock (_lock)
        {
            if (!_isLiteEdition.HasValue)
            {
                _isLiteEdition = isLite;
            }
            return _isLiteEdition.Value;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Timer? timerToDispose = null;
        DiscordRpcClient? clientToDispose = null;

        lock (_lock)
        {
            timerToDispose = _updateTimer;
            _updateTimer = null;

            clientToDispose = _client;
            _client = null;
        }

        timerToDispose?.Dispose();

        if (clientToDispose is { IsDisposed: false })
        {
            clientToDispose.ClearPresence();
            clientToDispose.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}