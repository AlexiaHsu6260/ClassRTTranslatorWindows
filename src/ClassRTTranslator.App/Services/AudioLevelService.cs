using NAudio.Wave;

namespace ClassRTTranslator.App.Services;

/// <summary>
/// 基于 NAudio 的麦克风电平采集（WASAPI 共享模式），用于 UI 电平指示。
/// 与语音识别引擎共存时依赖系统的共享音频模式。
/// </summary>
public sealed class AudioLevelService : IDisposable
{
    private WaveInEvent? _waveIn;

    /// <summary>电平变化（0.0 ~ 1.0）。</summary>
    public event Action<float>? LevelChanged;

    public void Start()
    {
        if (_waveIn != null) return;
        var waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 50,
        };
        waveIn.DataAvailable += OnDataAvailable;
        try
        {
            waveIn.StartRecording();
            _waveIn = waveIn;
        }
        catch
        {
            waveIn.Dispose();
            // 麦克风被占用或未插好时静默降级（不影响语音识别本身）。
        }
    }

    public void Stop()
    {
        if (_waveIn is null) return;
        var waveIn = _waveIn;
        _waveIn = null;
        waveIn.DataAvailable -= OnDataAvailable;
        try { waveIn.StopRecording(); } catch { /* 忽略 */ }
        waveIn.Dispose();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded < 2) return;
        double sumSquares = 0;
        var samples = e.BytesRecorded / 2;
        for (var i = 0; i < e.BytesRecorded; i += 2)
        {
            var sample = BitConverter.ToInt16(e.Buffer, i) / 32768f;
            sumSquares += sample * sample;
        }
        var rms = (float)Math.Sqrt(sumSquares / samples);
        LevelChanged?.Invoke(Math.Clamp(rms * 3f, 0f, 1f));
    }

    public void Dispose() => Stop();
}
