# Petrichor for Android

面向 Android 的原生 .NET MAUI 10 网易云音乐客户端。使用 Material Design 3，Android 12 及以上可跟随系统壁纸配色，支持系统/浅色/深色主题；播放栏贴边固定。

1.0.1 的页面修正、稿件对照和验收范围见 [UI-REFINEMENT.md](UI-REFINEMENT.md) 与 `artifacts/ui-refinement/index.html`。`android-smoke` 保留初版验证资料。

## 安装与源码

- APK：`artifacts/android/Petrichor-Android.apk`。
- Android 8.0（API 26）及以上，支持 ARM64 手机和 x86_64 设备/模拟器。
- 启动解决方案：`Petrichor.Android.sln`；启动项目：`src/Petrichor.Android/Petrichor.Android.csproj`。
- 包名：`app.petrichor.music`；显示版本：1.0.3。
- 这是使用本机开发证书签名的 Release APK，可侧载安装；当前原生界面修复和性能检查见 [UI-POLISH.md](UI-POLISH.md) 与 `artifacts/polish/index.html`。历史登录说明见 [PASSWORD-LOGIN.md](PASSWORD-LOGIN.md)。

## 功能

- 发现、推荐歌单、排行榜、每日推荐、私人 FM。
- 单曲、歌单、歌手、专辑、播客与节目搜索。节目结果单独提供播放和进入所属播客的入口。
- 账号密码（手机号/网易邮箱）、手机短信验证码登录、真实二维码生成与状态轮询；会话使用设备安全存储，密码不持久化。
- 新建公开/私密歌单、收藏歌单、添加歌曲；移除自己歌单中的单首或多选歌曲；从「我喜欢」中移除会取消喜欢。
- 歌单修改前核对服务端返回的创建者；失败后重新加载远程歌单，不伪造本地成功状态。批量移除按100首拆分，避免一次请求过大。
- 原生音频播放、前台媒体服务、通知与锁屏控制、音频焦点与拔出耳机暂停、循环/随机、队列、睡眠定时。
- 歌词同步与点击跳转、评论排序/回复/点赞、歌手/专辑详情与收藏。
- 下载、Wi-Fi 下载选项、离线播放、本地音频导入、最近播放。文件导入使用 Android 文件选择器，保存应用内副本。
- 歌曲长按使用 Android 原生长按事件，绑定具体歌曲；更多按钮共用菜单；多选模式下点击歌曲只改变选择。

所有内容来自真实接口或本机曲库，界面不内置设计稿的样例歌单或账号。播放权限、试听与音质以当前账号及服务端返回为准。

## 构建

需要 .NET 10 SDK、`maui-android` 工作负载、Android SDK 36（platform、build-tools、platform-tools）和 JDK 21。本机已按 [DEV-ENVIRONMENT.md](DEV-ENVIRONMENT.md) 配置完毕，换机或重装后先执行下面的检查：

```powershell
# 检查工具链：
pwsh -NoProfile -File scripts/Check-DevEnvironment.ps1
pwsh -NoProfile -File scripts/Build-Android.ps1
# 完全重新生成：
pwsh -NoProfile -File scripts/Build-Android.ps1 -Rebuild
# 指定 SDK / JDK：
pwsh -NoProfile -File scripts/Build-Android.ps1 -AndroidSdkDirectory C:\Android\Sdk -JavaSdkDirectory C:\Java\jdk-21
# 安装到已授权的 USB 设备：
pwsh -NoProfile -File scripts/Install-Android.ps1
```

Android 打包工具不支持本机项目路径中的中文。构建脚本临时使用空闲的英文盘符映射，完成后清理自己建立的映射；映射检查使用 Unicode Win32 API。APK 完整复制后才替换交付文件。

当前 Android 工作负载的 Release JNI 注册优化曾导致更新安装后在 `MauiApplication.n_onCreate` 阶段启动失败。项目明确关闭 `AndroidEnableMarshalMethods`，对应上游记录：[dotnet/maui#35209](https://github.com/dotnet/maui/issues/35209)。该配置不影响原生控件、后台播放或 MD3。共享核心保留 .NET Standard 1.4 以兼容复制来的 UWP 工程，因此构建会出现 NETSDK1215 的兼容目标提示。

## 验证

```powershell
dotnet run --project tests/Petrichor.Core.Tests
dotnet run --project tests/Petrichor.Core.Tests -- --live-migration
```

业务测试包含98项检查：WEAPI/EAPI、会话、账号密码/短信协议、歌单创建/移除与权限、播客节目搜索与所属播客、歌词、队列、睡眠定时等。`--live-migration` 仅访问公开搜索/详情，不发送短信，不修改远程账号。

本轮实际安卓渲染对照、输入检查和原始性能记录见 `artifacts/polish/`（1.0.3）。历史记录保留于 `artifacts/ui-refinement/` 和 `artifacts/password-login/`。真实账号的密码登录、手机验证码接收、登录后歌单写入及设备厂商的后台保活策略，仍需在实际账号/手机上验收；当前未使用任何真实手机号或账号执行这些操作。

## 迁移与设计

源项目已复制到此工作目录，原位置 `H:\项目\UWP\Project-Petrichor\Petrichor-uiux` 未改动。`src/Petrichor.App` 与 `src/Petrichor.Uwp` 保留 UWP 参考实现；Android 宿主不引用 Windows 专用项目。

- 设计预览：`design/android/index.html`（35个页面/状态，样例数据）。
- 页面实现：`src/Petrichor.Android/Pages`。
- Android 媒体、动态配色、离线存储：`src/Petrichor.Android/Services`。
- 跨平台接口与协议：`src/Petrichor.Core`。
- 迁移过程：`docs/ANDROID-MIGRATION.md`。
