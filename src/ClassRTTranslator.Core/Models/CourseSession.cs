namespace ClassRTTranslator.Core.Models;

/// <summary>一节课程会话：从开始到结束期间的翻译记录集合。</summary>
public class CourseSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = "";

    public DateTime StartDate { get; set; } = DateTime.Now;

    public DateTime EndDate { get; set; }

    public List<TranslationEntry> Entries { get; set; } = new();

    /// <summary>本节课同步保存的课堂录音文件路径（WAV，边录边存；无录音时为 null）。</summary>
    public string? RecordingPath { get; set; }

    public TimeSpan Duration => EndDate == default ? TimeSpan.Zero : EndDate - StartDate;

    public string DurationString =>
        $"{(int)Duration.TotalHours:D2}:{Duration.Minutes:D2}:{Duration.Seconds:D2}";

    public string StartTimeString => StartDate.ToString("yyyy-MM-dd HH:mm:ss");
}
