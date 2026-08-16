# CLASSRT TRANSLATOR 2077 · Windows Edition

macOS 版（[ClassRTTranslator2077](https://github.com/AlexiaHsu6260/ClassRTTranslator2077)）的 Windows 移植版，独立项目，**不影响原 macOS 应用**。

实时英语语音识别 → DeepSeek 在线翻译 → 课堂记录列表 + 置顶悬浮字幕 + 课程审阅 HTML 文档。

## 项目结构

```
ClassRTTranslatorWindows/
├── ClassRTTranslator.sln                  # 解决方案（VS2022 打开即可）
└── src/
    ├── ClassRTTranslator.Core/            # 纯逻辑类库（跨平台，与 UI 无关）
    │   ├── Models/                        # 翻译记录、课程会话、术语条目
    │   ├── Glossary/GlossaryManager.cs    # 术语表（增删/去重/Markdown 导入/JSON 持久化）
    │   ├── Translation/                   # DeepSeek 在线翻译（术语表注入、分批）
    │   └── Review/                        # DeepSeek 审阅 + HTML 文档生成
    └── ClassRTTranslator.App/             # WPF 界面（仅 Windows）
        ├── MainWindow.xaml(.cs)           # 赛博朋克主窗口 + 课程控制 + 翻译管线
        ├── Views/
        │   ├── CaptionOverlayWindow       # 置顶透明悬浮字幕窗
        │   └── SettingsWindow             # API Key / 术语表 / 背景 / 悬浮窗选项
        └── Services/
            ├── ISpeechRecognizer.cs       # 识别引擎抽象（可替换）
            ├── WindowsSpeechRecognizer.cs # WinRT 系统识别（英语）
            ├── AudioLevelService.cs       # NAudio 麦克风电平
            └── AppSettings.cs             # 设置持久化（%LOCALAPPDATA%\ClassRTTranslator）
```

## 环境要求（构建）

- Windows 10 1809+ 或 Windows 11
- Visual Studio 2022（勾选「.NET 桌面开发」工作负载）或 `.NET 8 SDK`
- 系统已安装「英语（美国）」语言包（设置 → 时间和语言 → 语言和区域）

## 构建与运行

1. 用 Visual Studio 2022 打开 `ClassRTTranslator.sln`
2. 选择启动项目 `ClassRTTranslator.App`，F5 运行；或右键项目 → 发布（`PublishSingleFile` 单文件 exe）

命令行构建：

```bat
dotnet restore ClassRTTranslator.sln
dotnet build ClassRTTranslator.sln -c Release
```

## 使用步骤

1. 首次启动如提示麦克风权限，请允许；若误拒，前往 设置 → 隐私和安全性 → 麦克风 允许
2. 点击右上角「⚙ 设置」：
   - 填入 DeepSeek API Key（[platform.deepseek.com](https://platform.deepseek.com) 获取，密钥仅存本机）
   - 可导入 Markdown 术语表（`| 英文 | 中文 | 注释 |`，与 macOS 版格式一致）
   - 可选：设置背景图、悬浮窗透明度
3. 回到主窗口点击「开始课程」，对着麦克风说英语，即实时显示中文译文
4. 点击「停止课程」后，可点击「审阅」生成课堂审阅 HTML 文档（保存到 `桌面/课程记录/`）

## 与 macOS 版的功能对照

| 功能 | macOS 版 | Windows 版 |
|---|---|---|
| 语音识别 | SFSpeechRecognizer（本地离线） | Windows 系统识别（WinRT，英语） |
| 实时翻译 | 系统离线翻译 / DeepSeek | DeepSeek 在线翻译 |
| 悬浮字幕窗 | NSPanel | WPF 置顶透明窗 |
| 术语表 | ✅（Markdown 导入） | ✅（同格式） |
| 课程审阅 HTML | ✅ | ✅ |
| 自定义背景 | ✅ | ✅ |
| 电平指示 | ✅ | ✅（NAudio） |
| 输入设备切换 | ✅（CoreAudio） | ⏳ 待实现（仅枚举） |

## 已知限制与后续路线

- Windows 无免费离线翻译框架，翻译走 DeepSeek 在线（需 API Key 与网络）
- 系统识别的「边说边出字」体验不如 macOS 版；后续可接入 **sherpa-onnx 离线流式识别**（`SherpaOnnxSharp`），支持术语表热词、完全离线
- 切换系统默认音频设备需管理员权限，暂不实现，仅提供设备枚举
- 未签名 exe 首次运行会被 SmartScreen 拦截：点击「更多信息 → 仍要运行」

## 数据位置

- 设置：`%LOCALAPPDATA%\ClassRTTranslator\settings.json`
- 术语表：`%LOCALAPPDATA%\ClassRTTranslator\glossary.json`
- 审阅文档：`桌面/课程记录/*.html`
