using System.Text.Json;

namespace ToiletWidget;

public static class WebMessageParser
{
    // 期待JSON: { "type":"status_changed", "status":"busy" }
    public static bool TryGetStatusChanged(string json, out string status)
    {
        status = "";

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeEl)) return false;
            if (typeEl.GetString() != "status_changed") return false;

            status = root.TryGetProperty("status", out var stEl) ? (stEl.GetString() ?? "") : "";
            return !string.IsNullOrWhiteSpace(status);
        }
        catch
        {
            return false;
        }
    }
}
