namespace ClassRTTranslator.App.Services;

/// <summary>
/// 语音识别引擎抽象。底层可替换：
/// 当前提供 Windows 系统识别（WindowsSpeechRecognizer）；
/// 后续可替换为 sherpa-onnx 离线流式识别（支持热词、边说边出字）。
/// </summary>
public interface ISpeechRecognizer : IAsyncDisposable
{
    bool IsRunning { get; }

    /// <summary>识别过程中的中间结果（实时滚动文本，可选）。</summary>
    event Action<string>? PartialResult;

    /// <summary>一句话识别完成。</summary>
    event Action<string>? FinalResult;

    /// <summary>发生错误（提示信息）。</summary>
    event Action<string>? ErrorOccurred;

    Task StartAsync();

    Task StopAsync();
}
