# ClassRT Translator Windows 版 · 发布说明

本文档说明如何把 Windows 版整理为可分发的 package（单文件 exe + zip），并描述分发包的内容与校验方式。

## 分发包内容

执行打包后，`dist\` 目录下会生成：

| 文件 | 说明 |
|---|---|
| `ClassRTTranslator-Windows-x64\ClassRTTranslator.exe` | 单文件可执行程序（自包含，目标机无需安装 .NET） |
| `ClassRTTranslator-Windows-x64.zip` | 可分发的压缩包（解压即可运行） |
| `ClassRTTranslator-Windows-x64.sha256` | 全部产物的 SHA256 校验文件 |

## 构建要求

- **Windows 10 1809+ 或 Windows 11**（WPF 应用只能在 Windows 上构建）
- **.NET 8 SDK**（含 Windows 桌面运行时）：https://dotnet.microsoft.com/download/dotnet/8.0
- 或 Visual Studio 2022（勾选「.NET 桌面开发」工作负载）
- 系统已安装「英语（美国）」语言包（设置 → 时间和语言 → 语言和区域）——识别需要

## 一键打包（推荐）

在仓库根目录打开 PowerShell，运行：

```powershell
.\build-release.ps1
```

脚本会自动完成：检查 SDK → `dotnet publish`（Release / win-x64 / 自包含 / 单文件）→ 拷贝产物 → 生成 SHA256 → 压缩 zip。

仅重新打包已有产物（改完文档后不想重新编译）：

```powershell
.\build-release.ps1 -SkipPublish
```

> 若 PowerShell 提示「禁止运行脚本」，先执行一次：`Set-ExecutionPolicy -Scope Process Bypass` 再运行。

## 命令行手动发布

```bat
dotnet publish src\ClassRTTranslator.App\ClassRTTranslator.App.csproj -c Release -p:PublishProfile=Win-x64-SingleFile
```

产物输出到 `dist\publish\`。或使用 Visual Studio 2022：右键 `ClassRTTranslator.App` → 发布 → 选择配置文件 `Win-x64-SingleFile`。

## 校验分发包

```powershell
Get-FileHash dist\ClassRTTranslator-Windows-x64.zip -Algorithm SHA256
```

与 `dist\ClassRTTranslator-Windows-x64.sha256` 中的条目比对；该文件内每条为 `哈希  文件名`，逐文件核对即可。

## 运行要求（目标机）

- Windows 10 1809+ 或 Windows 11，64 位
- 无需安装 .NET（自包含）
- 需系统「英语（美国）」语言包（用于语音识别）
- 麦克风权限：首次启动如提示请允许；若误拒，前往 设置 → 隐私和安全性 → 麦克风

## 常见问题

- **SmartScreen 拦截**：未签名 exe 首次运行会被拦截，点击「更多信息 → 仍要运行」；正式分发建议购买代码签名证书后签名
- **没有语音识别结果**：确认已安装英语语言包、已授予麦克风权限、输入电平显示绿色
- **杀毒软件误报**：单文件自包含程序首次运行会在临时目录解压 native 库，个别杀软可能误报；将程序目录加入白名单即可

## 发布 checklist

- [ ] 版本号已更新（如需要）
- [ ] `.\build-release.ps1` 打包成功
- [ ] 在干净的 Windows 虚拟机/机器上解压 zip 并运行验证（识别、翻译、审阅）
- [ ] 核对 SHA256
- [ ] 上传 zip + sha256 到 GitHub Release
