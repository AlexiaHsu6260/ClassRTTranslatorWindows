using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClassRTTranslator.App.Services;
using ClassRTTranslator.App.Views;
using ClassRTTranslator.Core.Glossary;
using ClassRTTranslator.Core.Models;
using ClassRTTranslator.Core.Review;
using ClassRTTranslator.Core.Translation;
using NAudio.Wave;

namespace ClassRTTranslator.App;

/// <summary>记录列表展示项。</summary>
public class TranslationDisplayItem
{
    public string Time { get; set; } = "";
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
}

/// <summary>主窗口：语音识别 → 实时翻译 → 记录列表 + 悬浮字幕 + 课程审阅。</summary>
public partial class MainWindow : Window
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly GlossaryManager _glossary = new();
    private readonly ISpeechRecognizer _recognizer = new WindowsSpeechRecognizer();
    private readonly AudioLevelService _audioLevel = new();
    private readonly ObservableCollection<TranslationDisplayItem> _records = new();
    private readonly SemaphoreSlim _queueConsumerLock = new(1, 1);

    private Channel<string> _sentences = Channel.CreateUnbounded<string>();
    private CourseSession? _course;
    private CaptionOverlayWindow? _caption;
    private SettingsWindow? _settingsWindow;
    private DispatcherTimer? _courseTimer;
    private DateTime _courseStartTime;

    // 课堂录音播放（课后重听）。
    private AudioFileReader? _playbackReader;
    private WaveOutEvent? _playbackOutput;

    public MainWindow()
    {
        InitializeComponent();
        ListRecords.ItemsSource = _records;
        ApplyBackground();
        UpdateOverlay();
    }

    // MARK: - 课程控制

    private async void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_course is null)
        {
            StartCourse();
        }
        else
        {
            await StopCourseAsync();
        }
    }

    private void StartCourse()
    {
        if (string.IsNullOrWhiteSpace(_settings.DeepSeekApiKey))
        {
            MessageBox.Show(
                "翻译依赖 DeepSeek 在线服务，请先点击右上角「设置」填写 API Key。\n" +
                "（没有 API Key 也能识别语音，但不会产出译文）",
                "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        _course = new CourseSession();
        _records.Clear();
        _sentences = Channel.CreateUnbounded<string>();

        _recognizer.FinalResult += OnFinalResult;
        _recognizer.ErrorOccurred += OnRecognizerError;
        _ = _recognizer.StartAsync();

        _audioLevel.LevelChanged += OnLevelChanged;
        // 边录边存：课程进行中把麦克风音频实时写入 WAV，供课后重听。
        _audioLevel.Start(BuildRecordingPath());
        _course.RecordingPath = _audioLevel.LastRecordingPath;

        _courseStartTime = DateTime.Now;
        _courseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _courseTimer.Tick += OnTimerTick;
        _courseTimer.Start();

        UpdateState(true);
    }

    private async Task StopCourseAsync()
    {
        _courseTimer?.Stop();
        _courseTimer = null;
        _audioLevel.Stop();
        _audioLevel.LevelChanged -= OnLevelChanged;
        _recognizer.ErrorOccurred -= OnRecognizerError;
        _recognizer.FinalResult -= OnFinalResult;
        await _recognizer.StopAsync();
        _sentences.Writer.TryComplete();

        if (_course is not null)
        {
            _course.EndDate = DateTime.Now;
            _course.RecordingPath = _audioLevel.LastRecordingPath;
            LblTimer.Text = _course.DurationString;
        }
        UpdateState(false);
    }

    /// <summary>生成课堂录音文件保存路径：桌面/课程记录/课堂录音/课堂录音_yyyy-MM-dd_HH-mm-ss.wav。</summary>
    private string BuildRecordingPath()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var folder = Path.Combine(desktop, "课程记录", "课堂录音");
        var name = $"课堂录音_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.wav";
        return Path.Combine(folder, name);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _courseStartTime;
        LblTimer.Text = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
    }

    private void UpdateState(bool running)
    {
        BtnToggle.Content = running ? "停止课程" : "开始课程";
        DotStatus.Fill = running
            ? new SolidColorBrush(Color.FromRgb(0x39, 0xFF, 0x88))
            : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        LblStatus.Text = running ? "识别中… 正在实时翻译" : "已停止 — 点击「开始课程」";
        BtnReview.IsEnabled = !running && (_course?.Entries.Count ?? 0) > 0;
        var hasRecording = _course?.RecordingPath is { } p && !string.IsNullOrEmpty(p) && File.Exists(p);
        BtnPlayback.IsEnabled = !running && hasRecording;
        BtnRetranslate.IsEnabled = !running && (_course?.Entries.Count ?? 0) > 0;
        UpdateOverlay();
    }

    // MARK: - 识别与翻译

    private void OnFinalResult(string sentence)
    {
        _sentences.Writer.TryWrite(sentence);
        // 单个后台消费者处理队列（防抖 + 批量翻译）。
        _ = Task.Run(ConsumeQueueSafeAsync);
    }

    private void OnRecognizerError(string message)
    {
        Dispatcher.Invoke(() => LblStatus.Text = message);
    }

    private async Task ConsumeQueueSafeAsync()
    {
        if (!await _queueConsumerLock.WaitAsync(0)) return; // 已有消费者在跑
        try
        {
            await ConsumeQueueAsync();
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => LblStatus.Text = $"翻译管线异常：{ex.Message}");
        }
        finally
        {
            _queueConsumerLock.Release();
        }
    }

    private async Task ConsumeQueueAsync()
    {
        while (await _sentences.Reader.WaitToReadAsync())
        {
            var batch = new List<string>();
            while (batch.Count < 10 && _sentences.Reader.TryRead(out var s)) batch.Add(s);
            if (batch.Count == 0) continue;

            // 防抖：短暂等待以合并连续句子，减少 API 调用次数。
            await Task.Delay(400);
            while (batch.Count < 10 && _sentences.Reader.TryRead(out var s)) batch.Add(s);

            if (_course is null) return;
            await TranslateBatchAsync(batch);
        }
    }

    private async Task TranslateBatchAsync(List<string> sentences)
    {
        var apiKey = _settings.DeepSeekApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Dispatcher.Invoke(() => LblStatus.Text = "未配置 API Key，已跳过翻译。");
            return;
        }

        try
        {
            var results = await DeepSeekTranslationService.TranslateAsync(sentences, _glossary.Terms, apiKey);
            for (var i = 0; i < sentences.Count && i < results.Count; i++)
            {
                AppendRecord(sentences[i], results[i]);
            }
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => LblStatus.Text = $"翻译失败：{ex.Message}");
        }
    }

    private void AppendRecord(string source, string target)
    {
        Dispatcher.Invoke(() =>
        {
            var item = new TranslationDisplayItem
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Source = source,
                Target = target,
            };
            _records.Add(item);
            if (_records.Count > 500) _records.RemoveAt(0);
            ListRecords.ScrollIntoView(item);

            _course?.Entries.Add(new TranslationEntry { Source = source, Target = target });
            LblCount.Text = $"{_course?.Entries.Count ?? 0} 条";
            _caption?.SetTranslation(source, target);
        });
    }

    private void OnLevelChanged(float level)
    {
        Dispatcher.Invoke(() =>
        {
            BarLevel.Width = 220 * level;
            BarLevel.Opacity = 0.35 + 0.65 * level;
        });
    }

    // MARK: - 审阅

    private async void BtnReview_Click(object sender, RoutedEventArgs e)
    {
        if (_course is null || _course.Entries.Count == 0)
        {
            MessageBox.Show("当前没有可审阅的课程记录。", "审阅", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        BtnReview.IsEnabled = false;
        LblStatus.Text = "正在调用 DeepSeek 审阅本节课记录…";
        try
        {
            var result = await DeepSeekReviewService.ReviewAsync(_course.Entries, _settings.DeepSeekApiKey);
            var path = DeepSeekReviewService.SaveDocument(_course, result);
            LblStatus.Text = "审阅完成";
            MessageBox.Show($"审阅完成！文档已保存到：\n{path}", "审阅完成",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LblStatus.Text = "审阅失败";
            MessageBox.Show(ex.Message, "审阅失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        BtnReview.IsEnabled = true;
    }

    // MARK: - 课堂录音重听 / 课后重新翻译

    private void BtnPlayback_Click(object sender, RoutedEventArgs e)
    {
        if (_playbackOutput is not null)
        {
            StopPlayback();
            return;
        }

        var path = _course?.RecordingPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            MessageBox.Show("未找到本节课的课堂录音文件。\n录音保存在「桌面/课程记录/课堂录音」目录。",
                "播放录音", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _playbackReader = new AudioFileReader(path);
            _playbackOutput = new WaveOutEvent();
            _playbackOutput.PlaybackStopped += OnPlaybackStopped;
            _playbackOutput.Init(_playbackReader);
            _playbackOutput.Play();
            BtnPlayback.Content = "⏹ 停止播放";
            LblStatus.Text = $"正在播放课堂录音：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StopPlayback();
            MessageBox.Show($"无法播放录音：{ex.Message}", "播放录音", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopPlayback()
    {
        var output = _playbackOutput;
        _playbackOutput = null;
        if (output is not null)
        {
            output.PlaybackStopped -= OnPlaybackStopped;
            try { output.Stop(); } catch { /* 忽略 */ }
            output.Dispose();
        }
        _playbackReader?.Dispose();
        _playbackReader = null;
        BtnPlayback.Content = "▶ 播放录音";
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.Invoke(StopPlayback);
    }

    private async void BtnRetranslate_Click(object sender, RoutedEventArgs e)
    {
        if (_course is null || _course.Entries.Count == 0)
        {
            MessageBox.Show("当前没有可重新翻译的课程记录。", "重新翻译", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(_settings.DeepSeekApiKey))
        {
            MessageBox.Show("重新翻译依赖 DeepSeek 在线服务，请先在「设置」中填写 API Key。",
                "重新翻译", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        BtnRetranslate.IsEnabled = false;
        LblStatus.Text = "正在用 DeepSeek 重新翻译本节课…（带整课上下文，质量优于实时逐句翻译）";
        try
        {
            var sentences = _course.Entries.Select(x => x.Source).ToList();
            var context = "这些句子来自同一节英语课的连续课堂记录，按时间顺序排列。" +
                          "请结合整节课的上下文进行翻译，保持术语、人名与表达前后一致，避免逐句生硬直译，使整体连贯自然。";
            var results = await DeepSeekTranslationService.TranslateAsync(
                sentences, _glossary.Terms, _settings.DeepSeekApiKey, context);

            var updated = 0;
            for (var i = 0; i < _course.Entries.Count && i < results.Count; i++)
            {
                if (string.IsNullOrEmpty(results[i])) continue;
                _course.Entries[i].Target = results[i];
                if (i < _records.Count) _records[i].Target = results[i];
                updated++;
            }

            LblStatus.Text = $"重新翻译完成，已更新 {updated} 条译文（可再次「审阅」生成文档）";
            MessageBox.Show($"重新翻译完成，共更新 {updated} 条译文。\n可点击「审阅」生成更高质量的审阅文档。",
                "重新翻译", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LblStatus.Text = "重新翻译失败";
            MessageBox.Show(ex.Message, "重新翻译失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        BtnRetranslate.IsEnabled = true;
    }

    // MARK: - 设置 / 悬浮窗 / 背景

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settings, _glossary)
            {
                Owner = this,
            };
            _settingsWindow.SettingsChanged += OnSettingsChanged;
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnSettingsChanged()
    {
        UpdateOverlay();
        ApplyBackground();
    }

    private void UpdateOverlay()
    {
        if (_settings.OverlayEnabled)
        {
            if (_caption is null)
            {
                _caption = new CaptionOverlayWindow(_settings);
                _caption.Closed += (_, _) => _caption = null;
                _caption.Show();
            }
            _caption.Opacity = _settings.OverlayOpacity;
        }
        else
        {
            _caption?.Hide();
        }
    }

    private void ApplyBackground()
    {
        var path = _settings.BackgroundImagePath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                BgImage.ImageSource = bitmap;
                return;
            }
            catch
            {
                // 图片损坏时回退到纯色背景。
            }
        }
        BgImage.ImageSource = null;
    }

    protected override void OnClosed(EventArgs e)
    {
        _courseTimer?.Stop();
        StopPlayback();
        _audioLevel.Dispose();
        _ = _recognizer.DisposeAsync();
        _caption?.Close();
        _settings.Save();
        base.OnClosed(e);
    }
}
