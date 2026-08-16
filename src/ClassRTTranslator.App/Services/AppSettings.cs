using System.IO;
using System.Text.Json;

namespace ClassRTTranslator.App.Services;

/// <summary>应用设置（JSON 持久化于 %LOCALAPPDATA%/ClassRTTranslator/settings.json）。</summary>
public sealed class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClassRTTranslator",
        "settings.json");

    /// <summary>DeepSeek API Key（与审阅共用）。</summary>
    public string DeepSeekApiKey { get; set; } = "";

    /// <summary>是否显示悬浮字幕窗。</summary>
    public bool OverlayEnabled { get; set; } = true;

    /// <summary>悬浮窗透明度（0.3 ~ 1.0）。</summary>
    public double OverlayOpacity { get; set; } = 0.9;

    /// <summary>悬浮窗位置（未设置时为 NaN）。</summary>
    public double OverlayX { get; set; } = double.NaN;

    public double OverlayY { get; set; } = double.NaN;

    /// <summary>主窗口背景图路径（可选）。</summary>
    public string? BackgroundImagePath { get; set; }

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath)) return new AppSettings();
        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 忽略写入失败。
        }
    }
}
