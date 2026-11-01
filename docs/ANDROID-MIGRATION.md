# Petrichor Android 迁移记录

源项目 `H:\项目\UWP\Project-Petrichor\Petrichor-uiux` 已复制到当前工作目录，原目录未改动。Android 项目为原生 .NET MAUI 10，复用跨平台协议层，不依赖 UWP 页面或 WebView。

## 已实现

- 35 个页面/状态的设计预览位于 `design/android/index.html`；用户已确认设计方向。实际原生页面位于 `src/Petrichor.Android/Pages`，使用真实接口数据。
- Material Design 3、Android 12+ 壁纸动态取色、系统/浅色/深色主题、固定贴底播放栏。
- 手机验证码与扫码登录、安全保存会话；新建公开/私密歌单；歌曲添加、单首与批量移除；移除前检查歌单创建者，「我喜欢」使用取消喜欢。
- 六类搜索，节目可直接播放或进入所属播客；处理较大的节目序号、稀疏详情和缺失元数据。
- 原生长按菜单消费触摸，菜单绑定具体曲目；多选模式点击只改变选择。
- 原生音频、前台媒体服务、通知/锁屏、音频焦点、队列、歌词、睡眠定时、下载、本地导入、历史与评论等。

## 交付与验证

安装包：`artifacts/android/Petrichor-Android.apk`。构建、安装、运行时配置和功能说明见 [ANDROID.md](ANDROID.md)。验证记录见 [ANDROID-VALIDATION.md](ANDROID-VALIDATION.md)。

业务检查 81 项通过；公开接口「晴天节目搜索 → 节目详情 → 所属播客」实测通过。使用 Android 15 x86_64 模拟器进行原生启动、搜索、播客导航、长按、播放和登录页面验证。未使用真实手机号发送短信或执行真实账号的歌单写操作。

设计稿中的样例账号、曲目和二维码仅用于说明；原生应用二维码由真实登录接口生成。设计画布：[Petrichor Android](https://superdesign.dev/teams/2a786f92-dde2-44dd-b17c-8f6c94277721/projects/f68bfd1a-53a5-4dca-b67c-6ea662228783)。
