# Petrichor for Android

原生 **.NET MAUI 10 + Material Design 3** 网易云音乐客户端，支持 Android 8.0 以上的64位设备。

- [1.1.0 按 Material Design 3 调整全部界面、导航与动效](docs/UI-1.1.0.md)
- [1.0.10 播放稳定性、系统封面、页面动效与性能修复](docs/PLAYBACK-1.0.10.md)
- [APK 安装包](artifacts/android/Petrichor-Android.apk)
- [1.0.9 播放功能、当前队列与实际截图](artifacts/ui-1.0.9/index.html)
- [1.0.9 页面与手势修改说明](docs/UI-1.0.9.md)
- [1.0.8 进度条、手势协调与实际截图](artifacts/slider-review/index.html)
- [1.0.8 实现与验证说明](docs/UI-1.0.8.md)
- [1.0.7 下滑功能菜单、MD3 调整与实际截图](artifacts/md3-functions/index.html)
- [1.0.7 手势、Google 文档依据与验证范围](docs/UI-1.0.7.md)
- [1.0.6 Mooic UI 移植与原生截图](artifacts/maui-mooic-port/index.html)
- [1.0.6 移植范围、现有后台接线与验证](docs/UI-1.0.6.md)
- [Android 功能、构建与验证说明](docs/ANDROID.md)
- [1.0.4 安卓截图、交互录像与检查](artifacts/compact-review/index.html)
- [1.0.3 原生截图、修复前后对照与性能记录](artifacts/polish/index.html)
- [1.0.1 确认稿与原生 UI 对照](artifacts/ui-refinement/index.html)
- [35屏 MD3 交互设计预览](design/android/index.html)

已接入账号密码（手机号/网易邮箱）、手机验证码与扫码登录、公开/私密歌单创建、单首/多选移除自己歌单内的歌曲、播客节目搜索与所属播客跳转，以及动态取色、原生后台播放、歌词、评论和离线曲库。播放栏贴边固定。

1.0.1 按原确认稿修正了页面与组件，35份设计文件保持不变。[本轮修改与验证范围](docs/UI-REFINEMENT.md)。

打开 `Petrichor.Android.sln` 开始开发。构建脚本会处理中文路径下 Android 打包工具的兼容问题：

```powershell
pwsh -NoProfile -File scripts/Build-Android.ps1
```

工具链版本、本机安装位置、环境变量与已知陷阱见 [开发与编译环境](docs/DEV-ENVIRONMENT.md)。换机后可先运行 `pwsh -NoProfile -File scripts/Check-DevEnvironment.ps1` 检查依赖。

1.0.2 新增账号密码登录，复用登录页的页签和表单组件，支持密码显示/隐藏，仅保存会话，不保存密码。[本轮说明](docs/PASSWORD-LOGIN.md)。

1.0.3 针对实际安卓渲染修复输入框对齐、浮动标签裁切和快速输入乱序；优化曲目列表、歌单封面网格、分页与返回页面的刷新，Release 启用预编译。保留动态取色和贴边播放栏。[本轮检查与限制](docs/UI-POLISH.md)。

1.0.4 收紧布局、重做封面／歌词与功能分页、评论输入和页面转场，加入循环状态反馈、波浪进度条、无损音质请求与下载文件夹选择。[本轮说明与验证边界](docs/UI-1.0.4.md)。

共享业务代码的119项检查通过，公开接口的节目搜索/详情链路也已实测。真实账号登录和歌单写入仍需实际账号验收，未使用真实账号做自动写入测试。

原 UWP 项目保留作迁移参考：[UWP 原始说明](docs/README-UWP.md)。原源码目录未修改。

1.0.6 在现有 MAUI 工程移植 Mooic 3.0.1 的页面结构和交互：主页、探索、我的、贴底播放栏、大封面播放器、独立歌词和共享封面动效。继续调用现有 AppServices、Petrichor.Core、播放、登录、歌单、评论、下载与缓存逻辑。引用的上游 UI 使用 GPL-3.0，出处与许可随包提供，见 Resources/Raw/Mooic-NOTICE.txt 和 Mooic-LICENSE.txt。
