using System.Text.Json;
using ClassRTTranslator.Core.Models;

namespace ClassRTTranslator.Core.Glossary;

/// <summary>
/// 术语表管理器：负责术语的增删、JSON 持久化，以及从 Markdown 词库表格导入。
/// 术语表用于 DeepSeek 在线翻译时强制遵循，提升专业词汇的译文一致性。
/// </summary>
public class GlossaryManager
{
    private readonly string _storagePath;

    /// <summary>当前全部术语（调用方应通过 TermsChanged 事件刷新 UI）。</summary>
    public List<GlossaryTerm> Terms { get; private set; } = new();

    /// <summary>术语列表发生变化时触发（增删、清空、导入、加载）。</summary>
    public event Action? TermsChanged;

    public GlossaryManager(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassRTTranslator",
            "glossary.json");
        Load();
    }

    /// <summary>新增术语（英文大小写不敏感去重）。返回是否添加成功。</summary>
    public bool Add(string source, string target, string note = "")
    {
        var src = source.Trim();
        var dst = target.Trim();
        if (src.Length == 0 || dst.Length == 0) return false;
        if (Terms.Any(t => string.Equals(t.Source, src, StringComparison.OrdinalIgnoreCase)))
            return false;

        Terms.Add(new GlossaryTerm { Source = src, Target = dst, Note = note.Trim() });
        Save();
        return true;
    }

    public void Remove(GlossaryTerm term)
    {
        Terms.RemoveAll(t => t.Id == term.Id);
        Save();
    }

    public void Clear()
    {
        Terms.Clear();
        Save();
    }

    /// <summary>从 Markdown 表格词库文件导入术语，返回新增条数。</summary>
    public int ImportFromMarkdownFile(string path)
    {
        if (!File.Exists(path)) return 0;
        var content = File.ReadAllText(path);
        var rows = ParseMarkdownTable(content);
        var added = 0;
        foreach (var row in rows)
        {
            if (Terms.Any(t => string.Equals(t.Source, row.Source, StringComparison.OrdinalIgnoreCase)))
                continue;
            Terms.Add(new GlossaryTerm { Source = row.Source, Target = row.Target, Note = row.Note });
            added++;
        }
        Save();
        return added;
    }

    /// <summary>
    /// 解析 Markdown 表格（| 英文 | 中文 | 注释 |）前两列及第三列注释（可选），
    /// 跳过表头与分隔行。
    /// </summary>
    public static List<(string Source, string Target, string Note)> ParseMarkdownTable(string content)
    {
        var result = new List<(string, string, string)>();
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("|")) continue;
            if (line.Contains("---")) continue;

            var cells = line.Split('|', StringSplitOptions.None)
                .Select(c => c.Trim())
                .ToArray();
            if (cells.Length < 3) continue;

            var source = cells[1];
            var target = cells[2];
            if (source.Length == 0 || target.Length == 0) continue;
            if (source == "英文" || target == "中文") continue;

            var note = cells.Length >= 4 ? cells[3] : "";
            result.Add((source, target, note));
        }
        return result;
    }

    private void Load()
    {
        if (!File.Exists(_storagePath)) return;
        try
        {
            var json = File.ReadAllText(_storagePath);
            var loaded = JsonSerializer.Deserialize<List<GlossaryTerm>>(json);
            if (loaded != null) Terms = loaded;
        }
        catch
        {
            // 文件损坏时忽略，使用空术语表。
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_storagePath,
                JsonSerializer.Serialize(Terms, new JsonSerializerOptions { WriteIndented = true }));
            TermsChanged?.Invoke();
        }
        catch
        {
            // 忽略写入失败（磁盘/权限异常）。
        }
    }
}
