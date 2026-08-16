using System.Windows;
using System.Windows.Controls;
using ClassRTTranslator.App.Services;
using ClassRTTranslator.Core.Glossary;
using ClassRTTranslator.Core.Models;
using Microsoft.Win32;

namespace ClassRTTranslator.App.Views;

/// <summary>设置窗口：API Key、悬浮窗选项、背景图、术语表管理。</summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly GlossaryManager _glossary;

    /// <summary>设置变更后通知主窗口立即应用。</summary>
    public event Action? SettingsChanged;

    public SettingsWindow(AppSettings settings, GlossaryManager glossary)
    {
        InitializeComponent();
        _settings = settings;
        _glossary = glossary;

        TxtApiKey.Password = settings.DeepSeekApiKey;
        ChkOverlay.IsChecked = settings.OverlayEnabled;
        SldOpacity.Value = settings.OverlayOpacity;
        LblOpacity.Text = settings.OverlayOpacity.ToString("0.00");
        LblBackground.Text = string.IsNullOrEmpty(settings.BackgroundImagePath)
            ? "未设置"
            : System.IO.Path.GetFileName(settings.BackgroundImagePath);

        RefreshTerms();
        _glossary.TermsChanged += RefreshTerms;
    }

    private void RefreshTerms()
    {
        ListTerms.ItemsSource = _glossary.Terms.ToList();
        LblStatus.Text = $"共 {_glossary.Terms.Count} 条术语";
    }

    private void TxtApiKey_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _settings.DeepSeekApiKey = TxtApiKey.Password;
        _settings.Save();
        SettingsChanged?.Invoke();
    }

    private void ChkOverlay_Changed(object sender, RoutedEventArgs e)
    {
        _settings.OverlayEnabled = ChkOverlay.IsChecked == true;
        _settings.Save();
        SettingsChanged?.Invoke();
    }

    private void SldOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblOpacity is null) return;
        LblOpacity.Text = SldOpacity.Value.ToString("0.00");
        _settings.OverlayOpacity = SldOpacity.Value;
        _settings.Save();
        SettingsChanged?.Invoke();
    }

    private void BtnPickBackground_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*",
            Title = "选择背景图",
        };
        if (dialog.ShowDialog(this) == true)
        {
            _settings.BackgroundImagePath = dialog.FileName;
            _settings.Save();
            LblBackground.Text = System.IO.Path.GetFileName(dialog.FileName);
            SettingsChanged?.Invoke();
        }
    }

    private void BtnClearBackground_Click(object sender, RoutedEventArgs e)
    {
        _settings.BackgroundImagePath = null;
        _settings.Save();
        LblBackground.Text = "未设置";
        SettingsChanged?.Invoke();
    }

    private void BtnAddTerm_Click(object sender, RoutedEventArgs e)
    {
        var source = TxtTermSource.Text.Trim();
        var target = TxtTermTarget.Text.Trim();
        if (_glossary.Add(source, target))
        {
            TxtTermSource.Clear();
            TxtTermTarget.Clear();
        }
        else
        {
            MessageBox.Show(this, "请输入英文原词与中文译词，且避免与已有术语重复。", "术语表",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnImportTerm_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Markdown 词库 (*.md;*.markdown)|*.md;*.markdown|所有文件 (*.*)|*.*",
            Title = "导入术语表（Markdown 表格）",
        };
        if (dialog.ShowDialog(this) == true)
        {
            var added = _glossary.ImportFromMarkdownFile(dialog.FileName);
            LblStatus.Text = $"已从 {System.IO.Path.GetFileName(dialog.FileName)} 导入 {added} 条术语，共 {_glossary.Terms.Count} 条";
        }
    }

    private void BtnRemoveTerm_Click(object sender, RoutedEventArgs e)
    {
        if (ListTerms.SelectedItem is GlossaryTerm term)
        {
            _glossary.Remove(term);
        }
    }

    private void BtnClearTerms_Click(object sender, RoutedEventArgs e)
    {
        if (_glossary.Terms.Count > 0 &&
            MessageBox.Show(this, $"确定清空全部 {_glossary.Terms.Count} 条术语？", "术语表",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            _glossary.Clear();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _glossary.TermsChanged -= RefreshTerms;
        base.OnClosed(e);
    }
}
