using Windows.Globalization;
using Windows.Media.SpeechRecognition;

namespace ClassRTTranslator.App.Services;

/// <summary>
/// 基于 Windows 系统语音识别（WinRT，Windows.Media.SpeechRecognition）的实现。
/// 使用系统默认麦克风，英语（美国）语言包。无需第三方引擎，免费、离线。
/// </summary>
public sealed class WindowsSpeechRecognizer : ISpeechRecognizer
{
    private SpeechRecognizer? _recognizer;
    private bool _stoppingByUser;

    public bool IsRunning { get; private set; }

    /// <summary>系统连续识别没有中间结果通道，此事件不触发（保留给未来 sherpa-onnx 使用）。</summary>
    public event Action<string>? PartialResult;

    public event Action<string>? FinalResult;

    public event Action<string>? ErrorOccurred;

    public async Task StartAsync()
    {
        if (IsRunning) return;
        _stoppingByUser = false;
        try
        {
            var status = await SpeechRecognizer.RequestPermissionAsync();
            if (status != SpeechRecognizerAccessStatus.Granted)
            {
                ErrorOccurred?.Invoke(
                    "语音识别权限被拒绝。请前往 设置 → 隐私和安全性 → 麦克风，允许本应用使用麦克风。");
                return;
            }

            var language = new Language("en-US");
            _recognizer = new SpeechRecognizer(language);

            var compilation = await _recognizer.CompileConstraintsAsync();
            if (compilation.Status != SpeechRecognitionResultStatus.Success)
            {
                ErrorOccurred?.Invoke(
                    "语音识别约束编译失败：请确认系统已安装「英语（美国）」语言包（设置 → 时间和语言 → 语言和区域）。");
                await DisposeRecognizerAsync();
                return;
            }

            _recognizer.ContinuousRecognitionSession.ResultGenerated += OnResultGenerated;
            _recognizer.ContinuousRecognitionSession.Completed += OnSessionCompleted;
            await _recognizer.ContinuousRecognitionSession.StartAsync();
            IsRunning = true;
        }
        catch (Exception ex)
        {
            await DisposeRecognizerAsync();
            ErrorOccurred?.Invoke($"启动语音识别失败：{ex.Message}");
        }
    }

    public async Task StopAsync()
    {
        _stoppingByUser = true;
        await DisposeRecognizerAsync();
        IsRunning = false;
    }

    public ValueTask DisposeAsync()
    {
        _stoppingByUser = true;
        return new ValueTask(DisposeRecognizerAsync());
    }

    private async Task DisposeRecognizerAsync()
    {
        if (_recognizer is null) return;
        var r = _recognizer;
        _recognizer = null;
        r.ContinuousRecognitionSession.ResultGenerated -= OnResultGenerated;
        r.ContinuousRecognitionSession.Completed -= OnSessionCompleted;
        try { await r.ContinuousRecognitionSession.StopAsync(); } catch { /* 忽略 */ }
        r.Dispose();
    }

    private void OnResultGenerated(
        SpeechContinuousRecognitionSession sender,
        SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        // 系统连续识别模式：每次 ResultGenerated 即一句完整的话（到静音为止）。
        var result = args.Result;
        if (result.Status == SpeechRecognitionResultStatus.Success &&
            !string.IsNullOrWhiteSpace(result.Text))
        {
            FinalResult?.Invoke(result.Text);
        }
    }

    private void OnSessionCompleted(
        SpeechContinuousRecognitionSession sender,
        SpeechContinuousRecognitionSessionCompletedEventArgs args)
    {
        if (IsRunning && !_stoppingByUser)
        {
            // 会话意外结束（例如长时间静音）时提示，由调用方决定是否重启。
            ErrorOccurred?.Invoke($"语音识别会话已结束（{args.Status}）。");
        }
    }
}
