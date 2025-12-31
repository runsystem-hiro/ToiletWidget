using System;

namespace ToiletWidget;

public sealed class StatusNotificationPolicy
{
    public bool ShouldFocus(
        string? lastStatus,
        string newStatus,
        Settings settings,
        DateTime nowUtc,
        DateTime lastNotifiedAtUtc,
        out DateTime newLastNotifiedAtUtc)
    {
        newLastNotifiedAtUtc = lastNotifiedAtUtc;

        // 変化した時だけ
        if (string.Equals(lastStatus, newStatus, StringComparison.Ordinal))
            return false;

        // 通知OFF
        if (settings.Mode == NotificationMode.Off)
            return false;

        // 対象判定
        if (newStatus == "busy" && !settings.NotifyOnBusy)
            return false;

        if (newStatus == "free" && !settings.NotifyOnFree)
            return false;

        // クールダウン
        var cooldown = TimeSpan.FromSeconds(Math.Max(0, settings.CooldownSeconds));
        if (nowUtc - lastNotifiedAtUtc < cooldown)
            return false;

        newLastNotifiedAtUtc = nowUtc;
        return true;
    }
}
