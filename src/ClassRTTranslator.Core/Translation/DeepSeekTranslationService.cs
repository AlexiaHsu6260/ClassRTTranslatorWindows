using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClassRTTranslator.Core.Models;

namespace ClassRTTranslator.Core.Translation;

/// <summary>
/// 基于 DeepSeek 的在线翻译服务：质量优于系统离线翻译，且支持术语表强制遵循。
/// 与审阅服务共用同一个 API Key。
/// </summary>
public static class DeepSeekTranslationService
{
    private const string Endpoint = "https://api.deepseek.com/chat/completions";
    private const string Model = "deepseek-chat";
    private const int MaxBatchSize = 20;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(45);

    /// <summary>DeepSeek 返回 JSON 为 camelCase，解析时启用大小写不敏感。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 批量翻译英文句子为中文，严格遵循术语表。返回与输入等长的译文列表。
    /// </summary>
    /// <param name="courseContext">
    /// 可选。传整节课的上下文说明（如"这些句子来自同一节课的连续课堂记录"），
    /// 用于课后重新翻译时提示模型保持术语、人名与前后表达一致，获得比实时逐句翻译更好的质量。
    /// </param>
    public static async Task<List<string>> TranslateAsync(
        IReadOnlyList<string> sentences,
        IReadOnlyList<GlossaryTerm> glossary,
        string apiKey,
        string? courseContext = null,
        CancellationToken cancellationToken = default)
    {
        if (sentences.Count == 0) return new List<string>();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("未配置 DeepSeek API Key，请在设置中填写。");

        var systemPrompt = BuildSystemPrompt(glossary, courseContext);
        var results = new List<string>();

        for (var start = 0; start < sentences.Count; start += MaxBatchSize)
        {
            var batch = sentences.Skip(start).Take(MaxBatchSize).ToList();
            var userPrompt = "待翻译的英文句子：\n" + string.Join("\n",
                batch.Select((s, i) => $"[{i + 1}] {s}"));

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

            var apiResponse = await PostAsync(body, apiKey, cancellationToken);
            var content = apiResponse?.Choices?.FirstOrDefault()?.Message?.Content
                          ?? throw new InvalidOperationException("DeepSeek 未返回有效翻译结果。");

            var parsed = JsonSerializer.Deserialize<TranslationResponse>(content, JsonOptions)
                         ?? throw new InvalidOperationException("DeepSeek 返回的翻译 JSON 解析失败。");
            results.AddRange(parsed.Translations);
        }

        // 对齐数量：不足补空串，多余截断。
        while (results.Count < sentences.Count) results.Add("");
        return results.Take(sentences.Count).ToList();
    }

    private static string BuildSystemPrompt(IReadOnlyList<GlossaryTerm> glossary, string? courseContext = null)
    {
        var prompt = new StringBuilder(
            "你是一位专业的中英文实时翻译引擎。将用户提供的英文句子翻译成简体中文。" +
            "要求：忠实原文、通顺自然、符合中文表达习惯，保留专有名词与数字。");

        if (glossary.Count > 0)
        {
            var lines = string.Join("\n", glossary.Select(g => $"{g.Source} → {g.Target}"));
            prompt.Append("\n\n必须遵循以下用户自定义术语表：术语表中出现的英文词汇必须使用指定中文翻译，不得意译或省略：\n")
                .Append(lines);
        }

        if (!string.IsNullOrWhiteSpace(courseContext))
            prompt.Append("\n\n").Append(courseContext);

        prompt.Append("\n\n严格只输出 JSON，格式：{\"translations\":[\"译文1\",\"译文2\",...]}，" +
                      "译文数量必须与输入句子数量一致，不要输出任何其他文字或 markdown 标记。");
        return prompt.ToString();
    }

    private static async Task<ChatCompletionResponse?> PostAsync(
        JsonObject body, string apiKey, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = RequestTimeout };
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"DeepSeek 翻译 API 错误（{(int)response.StatusCode}）：{text[..Math.Min(text.Length, 160)]}");
        }
        return JsonSerializer.Deserialize<ChatCompletionResponse>(text, JsonOptions);
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

    private sealed class TranslationResponse
    {
        public List<string> Translations { get; set; } = new();
    }
}
