using System.Diagnostics;
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

    private static readonly string ClientId = IsLiteEdition() ? "1455192227734098075" : "1353376132732420136";
    private const string Version = "0.3.0";
    private const int UpdateIntervalMs = 15000;

    private static DiscordRpcClient? _client;
    private static Timer? _updateTimer;
    private static DateTime _startTime;
    private static volatile bool _updateRequested;
    private bool _disposed;

    public YmmRpcPlugin()
    {
        _startTime = DateTime.UtcNow;
        InitializeClient();
        StartUpdateTimer();
    }

    private static void InitializeClient()
    {
        if (_client is { IsDisposed: false }) return;

        _client = new DiscordRpcClient(ClientId)
        {
            Logger = new ConsoleLogger { Level = LogLevel.Warning }
        };

        _client.OnReady += (_, e) => Console.WriteLine($"[YMM-RPC] Connected: {e.User.Username}");
        _client.OnError += (_, e) => Console.WriteLine($"[YMM-RPC] Error: {e.Message}");

        _client.Initialize();
        UpdatePresence();
    }

    private static void StartUpdateTimer()
    {
        _updateTimer?.Dispose();
        _updateTimer = new Timer(_ =>
        {
            if (!_updateRequested) return;
            _updateRequested = false;
            UpdatePresence();
        }, null, UpdateIntervalMs, UpdateIntervalMs);
    }

    public static void RequestUpdate() => _updateRequested = true;

    private static void UpdatePresence()
    {
        if (_client is not { IsInitialized: true }) return;

        var settings = YmmRpcSettings.Default;

        if (!settings.IsEnabled)
        {
            _client.ClearPresence();
            return;
        }

        var presence = settings.CustomRpcEnabled 
            ? BuildCustomPresence(settings) 
            : BuildDefaultPresence();

        _client.SetPresence(presence);
    }

    private static RichPresence BuildDefaultPresence()
    {
        return new RichPresence
        {
            Details = "動画を編集中...",
            State = IsLiteEdition() ? "Working on YMM4 Lite" : "Working on YMM4",
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
        var presence = new RichPresence
        {
            Details = NullIfEmpty(settings.CustomRpcDetails),
            State = NullIfEmpty(settings.CustomRpcState),
            Assets = new Assets
            {
                LargeImageKey = string.IsNullOrEmpty(settings.CustomRpcLargeImageKey) ? "icon" : settings.CustomRpcLargeImageKey,
                LargeImageText = NullIfEmpty(settings.CustomRpcLargeImageText),
                SmallImageKey = NullIfEmpty(settings.CustomRpcSmallImageKey),
                SmallImageText = NullIfEmpty(settings.CustomRpcSmallImageText)
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

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _updateTimer?.Dispose();
        _updateTimer = null;

        if (_client is { IsDisposed: false })
        {
            _client.ClearPresence();
            _client.Dispose();
        }
        _client = null;

        GC.SuppressFinalize(this);
    }
    
    private static bool IsLiteEdition()
    {
        var exeName = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        if (exeName.Contains("Lite", StringComparison.OrdinalIgnoreCase))
            return true;

        var title = Application.Current?.MainWindow?.Title ?? "";
        return title.Contains("Lite", StringComparison.OrdinalIgnoreCase);
    }
}