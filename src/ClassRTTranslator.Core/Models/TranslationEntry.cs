namespace ClassRTTranslator.Core.Models;

/// <summary>一条翻译记录（英文原文 + 中文译文）。</summary>
public class TranslationEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string Source { get; set; } = "";

    public string Target { get; set; } = "";

    /// <summary>用于列表展示的时间（HH:mm:ss）。</summary>
    public string TimeString => Timestamp.ToString("HH:mm:ss");
}
