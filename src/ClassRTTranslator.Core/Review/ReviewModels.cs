namespace ClassRTTranslator.Core.Review;

/// <summary>DeepSeek 返回的课程审阅结果。</summary>
public class ReviewResult
{
    public string Title { get; set; } = "课堂记录";
    public string Summary { get; set; } = "";
    public List<ImprovedEntry> ImprovedEntries { get; set; } = new();
    public List<string> KeyPoints { get; set; } = new();
    public List<VocabularyItem> Vocabulary { get; set; } = new();
    public List<TopicCount> Topics { get; set; } = new();
}

public class ImprovedEntry
{
    public string Source { get; set; } = "";
    public string Translated { get; set; } = "";
    public string Improved { get; set; } = "";
    public string Note { get; set; } = "";
}

public class VocabularyItem
{
    public string Word { get; set; } = "";
    public string Meaning { get; set; } = "";
    public string Example { get; set; } = "";
}

public class TopicCount
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
}
