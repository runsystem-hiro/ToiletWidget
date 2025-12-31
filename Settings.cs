namespace ToiletWidget;

public enum NotificationMode
{
    Off = 0,
    FocusOnly = 1
}

public class Settings
{
    public NotificationMode Mode { get; set; } = NotificationMode.FocusOnly;

    public bool NotifyOnBusy { get; set; } = true;
    public bool NotifyOnFree { get; set; } = false;

    // 連打防止（秒）
    public int CooldownSeconds { get; set; } = 30;

    // 起動時にウィンドウを表示するか（必要なら後で使う）
    public bool ShowOnStartup { get; set; } = true;

    public bool AlwaysOnTop { get; set; } = false;
}
