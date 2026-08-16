using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClassRTTranslator.Core.Models;

namespace ClassRTTranslator.Core.Review;

/// <summary>
/// DeepSeek 审阅服务：对课堂翻译记录进行审阅改进，并生成美观的格式化 HTML 文档。
/// 文档保存位置：桌面/课程记录/课程开始时间 课堂记录.html
/// </summary>
public static class DeepSeekReviewService
{
    private const string Endpoint = "https://api.deepseek.com/chat/completions";
    private const string Model = "deepseek-chat";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(180);

    /// <summary>DeepSeek 返回 JSON 为 camelCase，解析时启用大小写不敏感。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>单次提交给 DeepSeek 的最大记录条数（超出时取最新内容）。</summary>
    private const int MaxSubmittedEntries = 800;

    public static async Task<ReviewResult> ReviewAsync(
        IReadOnlyList<TranslationEntry> entries, string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("未配置 DeepSeek API Key，请在设置中填写。");

        const string systemPrompt =
            """
            你是一位资深的学术课堂记录审阅助手，精通英语与中文。
            用户的输入是一节课的实时翻译记录：每行格式为 [序号] 英文原文 || 中文译文。
            请完成以下任务：
            1. 总结：概括本节课的核心主题与内容要点（80-150 字，中文）。
            2. 审阅改进：找出明显翻译错误或生硬、不准确的条目，给出改进译文；仅列出需要改进的条目，最多 30 条。
            3. 旁批：为每个改进条目附加一条帮助理解的注释（术语解释、背景知识或易错点，20 字左右）。
            4. 关键要点：提炼 5-10 条本节课最重要的知识要点。
            5. 词汇表：提取 5-12 个重点词汇（专业术语或高频词），给出词义与中文例句。
            6. 主题统计：将全部记录按内容主题归类，返回主题名称与覆盖条目数量（用于绘制图表，4-8 个主题）。
            必须严格输出如下 JSON（不要输出任何其他文字，不要使用 markdown 代码块标记）：
            {"title":"本节课标题","summary":"总结","improvedEntries":[{"source":"英文原文","translated":"原译文","improved":"改进译文","note":"旁批"}],"keyPoints":["要点1"],"vocabulary":[{"word":"单词","meaning":"词义","example":"例句"}],"topics":[{"name":"主题","count":数字}]}
            """;

        var userPrompt = BuildPrompt(entries);
        var body = new JsonObject
        {
            ["model"] = Model,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = userPrompt },
            },
            ["temperature"] = 0.3,
            ["response_format"] = new JsonObject { ["type"] = "json_object" },
        };

        using var client = new HttpClient { Timeout = RequestTimeout };
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"DeepSeek 审阅 API 错误（{(int)response.StatusCode}）：{text[..Math.Min(text.Length, 200)]}");
        }

        var apiResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(text, JsonOptions);
        var content = apiResponse?.Choices?.FirstOrDefault()?.Message?.Content
                      ?? throw new InvalidOperationException("未收到有效审阅结果。");
        return JsonSerializer.Deserialize<ReviewResult>(content, JsonOptions) ?? new ReviewResult();
    }

    /// <summary>保存审阅文档到「桌面/课程记录」，返回文件完整路径。</summary>
    public static string SaveDocument(CourseSession course, ReviewResult result)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var folder = Path.Combine(desktop, "课程记录");
        Directory.CreateDirectory(folder);

        var fileName = $"{course.StartDate:yyyy-MM-dd HH-mm-ss} 课堂记录.html";
        var path = Path.Combine(folder, fileName);
        var html = ReviewHtmlBuilder.Build(course, result);
        File.WriteAllText(path, html, Encoding.UTF8);
        return path;
    }

    private static string BuildPrompt(IReadOnlyList<TranslationEntry> entries)
    {
        var limited = entries.Skip(Math.Max(0, entries.Count - MaxSubmittedEntries)).ToList();
        var lines = limited.Select((e, i) => $"[{i + 1}] {e.Source} || {e.Target}");
        return $"课程翻译记录（共 {entries.Count} 条，以下为全部或最新 {limited.Count} 条）：\n{string.Join("\n", lines)}";
    }

    private sealed class ChatCompletionResponse
    {
        public Choice[]? Choices { get; set; }
        public sealed class Choice
        {
            public Message? Message { get; set; }
        }
        public sealed class Message
        {
            public string? Content { get; set; }
        }
    }
}
