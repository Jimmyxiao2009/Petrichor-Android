# 开发与编译环境

本文记录构建 Petrichor for Android 所需的工具链、本机安装位置和排查方法。全部命令在仓库根目录执行。

## 依赖版本

| 组件 | 版本 | 用途 |
| --- | --- | --- |
| .NET SDK | 10.0.403 | 编译 `net10.0-android`、`netstandard1.4` 与测试工程 |
| .NET 工作负载 | `maui-android`（10.0.110 清单） | MAUI 控件、Android 打包与 AOT |
| JDK | 21（当前 Zulu 21.0.9） | Android 资源的 `javac`/`d8`/AOT 工具链 |
| Android SDK Platform | android-36 | `TargetPlatformVersion=36` |
| Android SDK Build-Tools | 36.0.0 | `aapt2`、`d8`、`zipalign`、签名 |
| Android SDK Platform-Tools | 37.0.1 | `adb` 安装与调试 |
| PowerShell | 7（`pwsh`） | 运行 `scripts/*.ps1` |

项目最低支持 Android 8.0（API 26），目标 API 36，只打包 APK。

## 本机安装位置

| 内容 | 路径 |
| --- | --- |
| .NET SDK | `C:\Program Files\dotnet` |
| JDK | `C:\Program Files\Android\openjdk\zulu-21` |
| JDK 兼容入口 | `C:\Program Files\Android\openjdk\jdk-21.0.8`（指向 `zulu-21` 的目录联接） |
| Android SDK | `C:\Program Files (x86)\Android\android-sdk` |
| `pwsh` | `%USERPROFILE%\.dotnet\tools\pwsh.exe`（.NET 全局工具） |

`scripts/Build-Android.ps1` 与 `scripts/Check-DevEnvironment.ps1` 默认使用上述 JDK 与 Android SDK 路径，用 `-JavaSdkDirectory`、`-AndroidSdkDirectory` 可以覆盖。

## 环境变量

已设置为机器级：

| 变量 | 值 |
| --- | --- |
| `JAVA_HOME` | `C:\Program Files\Android\openjdk\zulu-21` |
| `ANDROID_HOME` | `C:\Program Files (x86)\Android\android-sdk` |
| `ANDROID_SDK_ROOT` | 同上 |
| `Path` | 追加 `C:\Program Files\dotnet`、JDK 的 `bin`、Android SDK 的 `cmdline-tools\latest\bin` 与 `platform-tools`、`%USERPROFILE%\.dotnet\tools` |

改动机器级变量后需要重开终端、Visual Studio 或 Rider 才会生效。

## 检查与构建

```powershell
# 检查工具链（缺少项会给出修复提示，失败返回 1）
pwsh -NoProfile -File scripts/Check-DevEnvironment.ps1

# Release APK，输出到 artifacts/android/Petrichor-Android.apk
pwsh -NoProfile -File scripts/Build-Android.ps1

# Debug 构建
pwsh -NoProfile -File scripts/Build-Android.ps1 -Configuration Debug

# 完全重新生成
pwsh -NoProfile -File scripts/Build-Android.ps1 -Rebuild
```

调试部署需要真机或模拟器都通过 `adb` 看到设备，然后执行 `scripts/Install-Android.ps1`。

## 测试

```powershell
dotnet run --project tests/Petrichor.Core.Tests        # 128 项业务检查
dotnet run --project tests/Petrichor.Playback.Tests    # 26 项播放生命周期检查
```

`dotnet run --project tests/Petrichor.Core.Tests -- --live-migration` 只访问公开搜索与详情接口，不发送短信、不修改远程账号。

IDE 可以直接打开 `Petrichor.Android.sln`，启动项目选择 `src/Petrichor.Android`。

## 环境陷阱

这些是本机实际踩到的问题，重装或换机后按顺序检查。

### `Version` 环境变量导致还原静默失败

ZTE 云桌面代理（Ice）会写入机器级环境变量 `Version=V3.4.2.0`。MSBuild 把所有环境变量映射为同名属性，于是 `$(Version)` 被污染，NuGet 解析项目版本失败，但**只报 `error MSB4181: RestoreTask 返回了 false，但未记录错误`**，真实原因藏在 `obj/project.nuget.cache` 的 `NU1105` 里。

修复：删除该变量后重开终端。

```powershell
[Environment]::SetEnvironmentVariable('Version',$null,'Machine')
```

`scripts/Build-Android.ps1` 在构建前会清除自己进程内的 `Version`，`scripts/Check-DevEnvironment.ps1` 会报告它是否又出现了。直接运行 `dotnet build` 或 IDE 构建不受此保护。

### JDK 版本号必须是三段式

Microsoft OpenJDK 报 `openjdk version "21.0.13.0.2"`，四段版本让 .NET Android 探测失败并刷 `warning XA0033`。当前使用 Zulu 21.0.9（`21.0.9`），构建不再出现该警告。换成其他 JDK 时用 `java -version` 确认版本号是三段式。

### 中文路径

Android 打包工具不兼容中文路径。`scripts/Build-Android.ps1` 会在需要时用 `subst` 临时映射一个空闲英文盘符（P: 到 U:），构建结束后解除自己建立的映射。

### 还原缓存不能跨机器复用

从其他机器复制来的 `obj/` 会让 NuGet 认为“资产文件未改变”而跳过写入，随后还原失败。换机后删除所有 `obj`、`bin` 再还原。

### NuGet 网络

`api.nuget.org` 在本机会被运营商/代理 302 到 `nuget.azure.cn`，NuGet 客户端会正常跟随。`dl.google.com` 偶尔握手失败，重试即可。

### 模拟器

本机是虚拟机且未开启嵌套虚拟化（`Hyper-V` 关闭、`VirtualizationFirmwareEnabled=False`），Android 模拟器没有硬件加速会非常慢。调试请用 USB 真机，或开启嵌套虚拟化后再装系统镜像。

## 从零重建

```powershell
# 1. .NET SDK 10（含 maui-android 工作负载）
#    https://dot.net/v1/dotnet-install.ps1 或官方安装器
dotnet workload install maui-android

# 2. JDK 21（解压到 C:\Program Files\Android\openjdk\zulu-21）
#    https://cdn.azul.com/zulu/bin/

# 3. Android SDK 命令行工具，解压到 <sdk>\cmdline-tools\latest
#    https://developer.android.com/studio#command-line-tools-only
& "<sdk>\cmdline-tools\latest\bin\android.bat" sdk install platform-tools
& "<sdk>\cmdline-tools\latest\bin\android.bat" sdk install platforms/android-36
& "<sdk>\cmdline-tools\latest\bin\android.bat" sdk install build-tools/36.0.0

# 4. PowerShell 7（GitHub 下载不稳定时可用 .NET 全局工具）
dotnet tool install --global PowerShell

# 5. 复检
pwsh -NoProfile -File scripts/Check-DevEnvironment.ps1
```

## 相关说明

- 构建、功能与验证：[ANDROID.md](ANDROID.md)
- Release 需要关闭 `AndroidEnableMarshalMethods`（已写入 csproj）：[dotnet/maui#35209](https://github.com/dotnet/maui/issues/35209)
- `Petrichor.Core` 保留 `netstandard1.4` 以兼容复制来的 UWP 工程，构建会出现 `NETSDK1215` 提示，属预期。
