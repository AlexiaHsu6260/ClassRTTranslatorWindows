namespace ClassRTTranslator.Core.Models;

/// <summary>一条术语表记录：用户自定义的专业词汇（英文 → 中文）。</summary>
public class GlossaryTerm
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Source { get; set; } = "";

    public string Target { get; set; } = "";

    public string Note { get; set; } = "";
}
