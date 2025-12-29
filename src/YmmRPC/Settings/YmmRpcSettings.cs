using System.ComponentModel;
using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Plugin;

namespace YmmRPC.Settings;

public class YmmRpcSettings : SettingsBase<YmmRpcSettings>, INotifyPropertyChanged
{
    public override SettingsCategory Category => SettingsCategory.None;
    public override string Name => "YMM4 Discord RPC";
    public override bool HasSettingView => true;
    public override object SettingView => new YmmRpcSettingsView();

    public new event PropertyChangedEventHandler? PropertyChanged;

    public bool IsEnabled
    {
        get;
        set => SetField(ref field, value);
    } = true;

    public bool CustomRpcEnabled
    {
        get;
        set => SetField(ref field, value);
    }

    public string CustomRpcDetails
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public string CustomRpcState
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public string CustomRpcLargeImageKey
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public string CustomRpcLargeImageText
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public string CustomRpcSmallImageKey
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public string CustomRpcSmallImageText
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public bool CustomRpcEnableButtons
    {
        get;
        set => SetField(ref field, value);
    }

    public string CustomRpcButton1Label
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public string CustomRpcButton1Url
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public string CustomRpcButton2Label
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public string CustomRpcButton2Url
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public override void Initialize()
    {
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        YmmRpcPlugin.RequestUpdate();
    }
}