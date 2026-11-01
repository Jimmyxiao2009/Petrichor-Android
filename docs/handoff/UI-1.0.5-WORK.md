# 1.0.5 handoff · 2026-10-23

用户本轮：上下滑动不好用；参考 Android 官方进度条；每个页面和启动等待网络，要求预加载、先缓存后展示或占位。

已完成实现与正式包交付；详细说明见 `docs/UI-1.0.5.md`。

- `ResponseCache.cs` + `PageCacheStore.cs`：允许列表、详情、搜索、播客和评论读取缓存；完整参数和账号分区，5 分钟／评论 1 分钟，7 天最大过期展示；请求合并、独立等待取消、磁盘损坏／满保护、版本化失效。
- Core 的 Cache 默认不启用以保持 UWP 等调用方兼容，Android 的 AppServices 提供持久化 store。Core 仍为 netstandard1.4；没有移动到不兼容目标。
- SecureStorage 保存的 Cookie + profile 本地恢复与账号联网验证分离；验证和主页面预取后台执行；Cookie 变化才写安全存储，退出重置写入去重状态。
- BasePage 观察缓存依赖；已有页面没有相关失效时不重建。发现页可实时应用后台更新；其他页面记录脏状态，在下次出现时刷新，避免在阅读／分页过程中重置内容。列表数据填充等待入场转场完成。首次列表与发现页使用静态骨架。
- PlayerDeck 改为 ContentView + 原生 ContentViewGroup Handler，Android 原生触摸分发和 VelocityTracker 处理纵向分页、子滚动边界与横向拖动互斥。保留点击封面显示歌词、按钮入口、独立功能 ScrollView 和关闭系统动画支持。
- WaveTrackDrawable 原生 SeekBar 实际 thumb bounds；4×44 dp 把手，6 dp 可见间隔，48 dp 区域、停止点，播放波浪相位和暂停振幅回收；不在每帧触发 MAUI 布局。

最终 APK：`artifacts/android/Petrichor-Android.apk`，1.0.5 / 6，33,529,760 字节，SHA256 `90A10AECD7F2759EA79B3CFE7FD434AC8035B01219F35ABB6E3733394A63364D`。已安装并从模拟器拉回验证一致，无 DEBUGGABLE，保留 Release SDKOnly 裁剪和 Profiled AOT。

验证：119 Core、8 DEBUG 手势／断网检查、9 Release 真实导航／断网缓存、3 轮快速返回、1 个暂停拖动检查通过。`artifacts/cache-review/index.html` 原生图集与 10 秒录屏。录屏采样 295 帧、9 帧错过截止时间、P95 42ms；不能与前一版不同场景直接比较。冷启动 Activity TotalTime 实测 1936、2768、3347ms，未承诺启动无开销。

没有真实登录账号，无损在线权限、真实短信／账号修改本轮仍未验证；仅本地 FLAC 实际播放。保留此前动态颜色、紧凑比例、贴底 mini、NativeEntry 登录／评论编辑器、歌单移除／创建、播客搜索和下载目录功能。

QA 脚本：`Verify-AndroidCache.py` 为 DEBUG 路由；`Verify-AndroidCacheRelease.py` 不依赖 DEBUG，使用公开排行榜歌曲入队而不联网播放，然后评论、断网和重启；`Record-AndroidCacheMotion.py` 需要已打开、已加载音频且暂停的本地 FLAC 播放页。若在本地文件刚加载时立即点暂停，会取消该次加载，Player 为 null，不能据此测试 seek；先播放确认准备好后再暂停。录屏调用不使用 UIAutomator，避免播放中 500ms 时间更新阻碍 idle。

`Build-Android.ps1` 仍用 Q/P SUBST 路径，绝不能同时运行两个 Android 构建。Debug 也会覆盖交付 APK；当前最后一次构建为 Release。不要覆盖之前 `compact-review` 的 1.0.4 证据。
