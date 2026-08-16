using System.Windows;
using ClassRTTranslator.App.Services;

namespace ClassRTTranslator.App.Views;

/// <summary>置顶悬浮字幕窗（不抢焦点、不显示在任务栏）。</summary>
public partial class CaptionOverlayWindow : Window
{
    private readonly AppSettings _settings;

    public CaptionOverlayWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        // 恢复上次位置；未设置时默认右下角。
        var workArea = SystemParameters.WorkArea;
        if (!double.IsNaN(_settings.OverlayX) && !double.IsNaN(_settings.OverlayY))
        {
            Left = _settings.OverlayX;
            Top = _settings.OverlayY;
        }
        else
        {
            Left = workArea.Right - Width - 32;
            Top = workArea.Bottom - Height - 48;
        }
        Opacity = _settings.OverlayOpacity;
    }

    /// <summary>更新字幕内容（原文 + 译文）。</summary>
    public void SetTranslation(string source, string target)
    {
        TxtSource.Text = source;
        TxtTarget.Text = target;
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (IsVisible)
        {
            _settings.OverlayX = Left;
            _settings.OverlayY = Top;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _settings.Save();
        base.OnClosed(e);
    }
}
