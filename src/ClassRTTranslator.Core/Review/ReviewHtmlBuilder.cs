using System.Text;
using ClassRTTranslator.Core.Models;

namespace ClassRTTranslator.Core.Review;

/// <summary>
/// 将课程会话与审阅结果渲染为美观的 HTML 文档。
/// 包含总结、关键要点、主题分布图表、翻译改进对照与旁批、词汇表、完整记录。
/// </summary>
public static class ReviewHtmlBuilder
{
    public static string Build(CourseSession course, ReviewResult result)
    {
        var hero = HeroSection(course, result);
        var summary = Card("课程总结", 1, SummaryHtml(result.Summary));
        var keyPoints = Card("关键要点", 2, KeyPointsHtml(result.KeyPoints));
        var topics = result.Topics.Count == 0 ? "" : Card("主题分布图表", 3, TopicsChartHtml(result.Topics));
        var review = Card("翻译审阅与旁批", 4, ReviewTableHtml(result.ImprovedEntries));
        var vocab = result.Vocabulary.Count == 0 ? "" : Card("重点词汇", 5, VocabularyHtml(result.Vocabulary));
        var full = Card("完整课堂记录", 6, FullEntriesHtml(course.Entries));

        var title = result.Title.Length == 0 ? "课堂记录" : result.Title;

        return $$"""
        <!DOCTYPE html>
        <html lang="zh-CN">
        <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>{{EscapeHtml(title)}}</title>
        <style>
        *{box-sizing:border-box;}
        body{margin:0;font-family:-apple-system,BlinkMacSystemFont,"PingFang SC","Hiragino Sans GB","Segoe UI",sans-serif;background:#f6f7fb;color:#1f2937;line-height:1.6;}
        .page{max-width:920px;margin:0 auto;padding:36px 24px 64px;}
        .hero{background:linear-gradient(135deg,#ff7a18,#f43f5e 55%,#8b5cf6);color:#fff;border-radius:18px;padding:30px 34px;box-shadow:0 14px 34px rgba(244,63,94,.25);}
        .hero h1{margin:0 0 10px;font-size:27px;letter-spacing:.5px;}
        .hero .meta{opacity:.95;font-size:13.5px;}
        .badge{display:inline-block;background:rgba(255,255,255,.2);border:1px solid rgba(255,255,255,.35);border-radius:999px;padding:2px 11px;font-size:12px;margin:3px 6px 0 0;}
        .card{background:#fff;border:1px solid #e5e7eb;border-radius:14px;padding:22px 24px;margin-top:22px;box-shadow:0 2px 10px rgba(15,23,42,.05);}
        .card h2{margin:0 0 14px;font-size:17px;display:flex;align-items:center;gap:9px;}
        .card h2 .num{background:#fff7ed;color:#ea580c;border:1px solid #fed7aa;border-radius:8px;font-size:12px;padding:2px 9px;}
        .summary{font-size:15px;}
        ul.key{list-style:none;padding:0;margin:0;}
        ul.key li{padding:9px 14px;border-left:3px solid #f97316;background:#fffaf5;border-radius:0 8px 8px 0;margin-bottom:8px;font-size:14px;}
        table{width:100%;border-collapse:collapse;font-size:13px;}
        th,td{border:1px solid #e5e7eb;padding:9px 11px;text-align:left;vertical-align:top;}
        th{background:#f9fafb;font-weight:600;white-space:nowrap;}
        tbody tr:nth-child(even) td{background:#fcfcfd;}
        .src{color:#6b7280;}
        .improved{color:#059669;font-weight:600;}
        .note{color:#0891b2;font-size:12px;}
        .empty{color:#9ca3af;font-style:italic;font-size:14px;}
        .bar-row{display:flex;align-items:center;gap:10px;margin-bottom:9px;}
        .bar-label{width:130px;font-size:13px;flex-shrink:0;text-align:right;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
        .bar-track{flex:1;background:#eef0f4;border-radius:999px;height:20px;overflow:hidden;}
        .bar-fill{height:100%;border-radius:999px;background:linear-gradient(90deg,#fb923c,#f43f5e);min-width:6px;}
        .bar-count{width:56px;font-size:12px;color:#6b7280;}
        .vocab{display:grid;grid-template-columns:1fr 1fr;gap:12px;}
        .vocab-item{border:1px solid #e5e7eb;border-radius:10px;padding:13px 15px;background:#fbfcfe;}
        .vocab-item .w{font-weight:700;color:#ea580c;font-size:15px;}
        .vocab-item .m{font-size:13px;margin-top:4px;}
        .vocab-item .e{font-size:12px;color:#6b7280;font-style:italic;margin-top:6px;}
        details{margin-top:6px;}
        summary{cursor:pointer;font-weight:600;color:#f97316;font-size:14px;user-select:none;}
        .entry{display:flex;gap:12px;padding:9px 0;border-bottom:1px dashed #e5e7eb;font-size:13px;}
        .entry:last-child{border-bottom:none;}
        .entry .time{color:#9ca3af;width:66px;flex-shrink:0;font-variant-numeric:tabular-nums;}
        .entry .body{flex:1;}
        .entry .src{color:#6b7280;}
        .entry .dst{font-weight:500;}
        footer{text-align:center;color:#9ca3af;font-size:12px;margin-top:30px;}
        </style>
        </head>
        <body>
        <div class="page">
        {{hero}}
        {{summary}}
        {{keyPoints}}
        {{topics}}
        {{review}}
        {{vocab}}
        {{full}}
        <footer>由 DeepSeek 审阅生成 · 实时课堂翻译记录</footer>
        </div>
        </body>
        </html>
        """;
    }

    private static string HeroSection(CourseSession course, ReviewResult result)
    {
        var title = result.Title.Length == 0 ? "课堂翻译记录" : result.Title;
        return $$"""
        <div class="hero">
        <h1>{{EscapeHtml(title)}}</h1>
        <div class="meta">📅 {{FullDateString(course.StartDate)}} · 🕐 {{TimeString(course.StartDate)}} ～ {{TimeString(course.EndDate)}}（时长 {{DurationString(course.Duration)}}）</div>
        <div>
        <span class="badge">共 {{course.Entries.Count}} 条翻译</span>
        <span class="badge">{{result.ImprovedEntries.Count}} 处改进</span>
        <span class="badge">{{result.KeyPoints.Count}} 个要点</span>
        <span class="badge">DeepSeek 审阅</span>
        </div>
        </div>
        """;
    }

    private static string Card(string title, int number, string content)
    {
        return $$"""
        <div class="card">
        <h2><span class="num">{{number}}</span>{{title}}</h2>
        {{content}}
        </div>
        """;
    }

    private static string SummaryHtml(string summary)
    {
        if (summary.Length == 0) return "<p class=\"empty\">暂无总结。</p>";
        return $"<p class=\"summary\">{EscapeHtml(summary)}</p>";
    }

    private static string KeyPointsHtml(IReadOnlyList<string> points)
    {
        if (points.Count == 0) return "<p class=\"empty\">暂无要点。</p>";
        var items = string.Join("\n", points.Select(p => $"<li>{EscapeHtml(p)}</li>"));
        return $"<ul class=\"key\">\n{items}\n</ul>";
    }

    private static string TopicsChartHtml(IReadOnlyList<TopicCount> topics)
    {
        var maxCount = Math.Max(topics.Max(t => t.Count), 1);
        var rows = topics.Select(topic =>
        {
            var percent = Math.Max((int)((double)topic.Count / maxCount * 100), 3);
            return $"""
                    <div class="bar-row">
                    <div class="bar-label">{EscapeHtml(topic.Name)}</div>
                    <div class="bar-track"><div class="bar-fill" style="width:{percent}%"></div></div>
                    <div class="bar-count">{topic.Count} 条</div>
                    </div>
                    """;
        });
        return string.Join("\n", rows);
    }

    private static string ReviewTableHtml(IReadOnlyList<ImprovedEntry> entries)
    {
        if (entries.Count == 0)
            return "<p class=\"empty\">未发现明显翻译问题，本节课译文质量良好。</p>";

        var rows = entries.Select((entry, index) =>
        {
            var note = entry.Note.Length == 0
                ? ""
                : $"<div class=\"note\">💡 {EscapeHtml(entry.Note)}</div>";
            return $"""
                    <tr>
                    <td>{index + 1}</td>
                    <td class="src">{EscapeHtml(entry.Source)}</td>
                    <td>{EscapeHtml(entry.Translated)}</td>
                    <td class="improved">{EscapeHtml(entry.Improved)}</td>
                    <td>{note}</td>
                    </tr>
                    """;
        });

        return $$"""
        <table>
        <thead><tr><th style="width:36px">#</th><th>英文原文</th><th>原译文</th><th>改进译文</th><th>旁批</th></tr></thead>
        <tbody>
        {{string.Join("\n", rows)}}
        </tbody>
        </table>
        """;
    }

    private static string VocabularyHtml(IReadOnlyList<VocabularyItem> items)
    {
        var cards = items.Select(item =>
        {
            var example = item.Example.Length == 0
                ? ""
                : $"<div class=\"e\">例：{EscapeHtml(item.Example)}</div>";
            return $"""
                    <div class="vocab-item">
                    <div class="w">{EscapeHtml(item.Word)}</div>
                    <div class="m">{EscapeHtml(item.Meaning)}</div>
                    {example}
                    </div>
                    """;
        });
        return $"<div class=\"vocab\">\n{string.Join("\n", cards)}\n</div>";
    }

    private static string FullEntriesHtml(IReadOnlyList<TranslationEntry> entries)
    {
        if (entries.Count == 0) return "<p class=\"empty\">本节课没有翻译记录。</p>";

        var rows = entries.Select(entry =>
            $"""
             <div class="entry">
             <div class="time">{TimeString(entry.Timestamp)}</div>
             <div class="body">
             <div class="src">{EscapeHtml(entry.Source)}</div>
             <div class="dst">{EscapeHtml(entry.Target)}</div>
             </div>
             </div>
             """);

        return $$"""
        <details>
        <summary>展开 / 收起全部 {{entries.Count}} 条记录（点击切换）</summary>
        <div style="margin-top:12px">
        {{string.Join("\n", rows)}}
        </div>
        </details>
        """;
    }

    private static string EscapeHtml(string s)
    {
        return s.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    private static string TimeString(DateTime date) => date.ToString("HH:mm:ss");

    private static string FullDateString(DateTime date) => date.ToString("yyyy年M月d日");

    private static string DurationString(TimeSpan duration)
    {
        var total = (int)duration.TotalSeconds;
        return $"{(total / 3600):D2}:{(total % 3600) / 60:D2}:{total % 60:D2}";
    }
}
