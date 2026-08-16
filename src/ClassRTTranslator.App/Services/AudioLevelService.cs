using System.IO;
using NAudio.Wave;

namespace ClassRTTranslator.App.Services;

/// <summary>
/// 基于 NAudio 的麦克风电平采集（WASAPI 共享模式），用于 UI 电平指示；
/// 同时支持「边录边存」：把采集到的音频实时写入 WAV 文件，供课后重听。
/// 与语音识别引擎共存时依赖系统的共享音频模式。
/// </summary>
public sealed class AudioLevelService : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;

    /// <summary>电平变化（0.0 ~ 1.0）。</summary>
    public event Action<float>? LevelChanged;

    /// <summary>最近一次录音保存的完整文件路径；未录音或无可听数据时为 null。</summary>
    public string? LastRecordingPath { get; private set; }

    /// <summary>
    /// 开始采集。传入 <paramref name="recordFilePath"/> 时同步开始边录边存到该 WAV 文件。
    /// </summary>
    public void Start(string? recordFilePath = null)
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
            if (!string.IsNullOrEmpty(recordFilePath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(recordFilePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    _writer = new WaveFileWriter(recordFilePath, waveIn.WaveFormat);
                    LastRecordingPath = recordFilePath;
                }
                catch
                {
                    _writer?.Dispose();
                    _writer = null;
                    LastRecordingPath = null;
                }
            }
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
        FinalizeWriter();
    }

    /// <summary>收尾录音文件：无任何数据时删除空文件。</summary>
    private void FinalizeWriter()
    {
        if (_writer is null) return;
        try
        {
            _writer.Dispose();
            if (_writer.Length == 0 && LastRecordingPath is { } path && File.Exists(path))
            {
                File.Delete(path);
                LastRecordingPath = null;
            }
        }
        catch
        {
            LastRecordingPath = null;
        }
        _writer = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded < 2) return;
        // 边录边存：写入课堂录音 WAV 文件。
        try { _writer?.Write(e.Buffer, 0, e.BytesRecorded); } catch { /* 忽略 */ }
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
