# 1.0.10 · 播放稳定性、系统封面与页面动效

版本：1.0.10 / versionCode 11。产品仍为 src/Petrichor.Android，native/Petrichor 仅作参考。

## 播放修复

- “上一首”直接选择队列中的上一首，取消播放超过 3 秒时先重播当前歌曲的分支。单曲循环下，手动前后切歌仍可跨越队列边界。
- 每个加载请求分别持有取消源和候选 MediaPlayer。旧请求返回、旧播放器的完成或错误回调不能覆盖新歌曲、清除新加载状态或重复推进队列。
- 完成回调以原生音频时长判断是否正常结束，允许 1.5 秒的编码时间戳误差。提前结束时保留当前队列项；网络流最多重连两次，重建播放器后先恢复位置再开始播放。试听使用原生片段时长。
- 对网络 I/O、超时、媒体进程失效和取流网络错误加入有限重试与短暂退避。资源不可用、解码不支持或重试耗尽时显示当前歌曲的错误，不偷偷跳过。
- 播放准备期间使用有超时的 CPU 唤醒锁；网络准备和播放期间持有 Wi-Fi 锁，在暂停、焦点丢失、失败和释放播放器时释放。加载中失去临时音频焦点也会取消准备，获得焦点后恢复；手动暂停会取消待恢复任务。
- 最近播放记录写入失败只记日志，不再把正在正常播放的音频停止。

## 系统媒体封面

播放服务异步读取本地封面或下载远程封面，后台解码并限制尺寸，将位图写入媒体会话的 AlbumArt、Art、DisplayIcon 和通知 LargeIcon。没有封面的歌曲使用应用默认图。切歌会取消旧封面任务并校验当前曲目，避免旧图覆盖新图。加载状态在系统媒体会话中显示为缓冲中。

## 播放诊断日志

设置 → 播放诊断日志提供开关、最近 100 条记录、刷新、导出和清空。日志默认保存在本机，使用有界异步队列写入两个轮换文件，总量约 1 MB；文件 I/O 不在音频或界面回调中执行。

记录包括加载和启动、手动切歌、正常或提前结束、网络重试、原生错误码、音频焦点、服务创建/销毁，以及每 30 秒一次的播放心跳。心跳包含当前进度、时长、前后台状态和 Android 系统省电状态。网络变化也会记录；账号凭证与播放 URL 不写入日志，并对意外出现的敏感字段进行脱敏。

导出生成带设备、Android 和版本信息的文本文件，再打开 Android 分享面板供用户选择去向。没有自动上传或发送。

## 动效与性能

- 详情页进入使用短距离滑入与淡入，返回使用滑出与淡出；主标签页按切换方向滑入，已缓存页面同样播放动画。播放器保留共享封面转场。
- 页面动画改由 Android ValueAnimator 更新原生渲染属性，避免逐帧把动画属性传播到 MAUI 布局树。系统关闭动画时直接呈现最终状态。
- 共享封面等待原生 pre-draw 布局完成回调，替代连续轮询多个稳定帧，并让重绘跟随 Android 显示帧。
- Activity 暂停后停止播放界面的周期更新与波浪重绘，音频服务继续播放。返回前台后立即同步一次真实状态。
- 进度显示按 500 毫秒采样并用单调时钟插值，波浪每帧绘制不再重复读取播放器进度、时长或通过 ContentResolver 读取系统动画设置。
- 周期刷新最多排队一次，避免界面忙时积压回调；播放与系统媒体更新仍由实际状态变化驱动。

## 验证

- 共享业务检查：128 项通过，日志见 artifacts/playback-fixes/core-tests.log。
- 播放生命周期与日志检查：26 项通过。测试直接编译生产 PlaybackController，使用 Android 替身注入延迟回调、取消、断流及焦点事件；它不代替真机媒体栈测试。
- Release 构建成功，日志见 artifacts/playback-fixes/build-release.log。
- Android 15 模拟器实际交互检查、封面截图、页面与封面转场录像、后台采样记录见 artifacts/playback-fixes。完整交互脚本会在结束后恢复模拟器原测试曲库。
- Android 系统省电模式实际长测：电池模拟为未接电、30% 电量；电源服务和应用日志均确认省电已开启。连续后台熄屏播放 360.14 秒，18 次采样均保持播放，期间跨过一次 242.416 秒的完整音频结束并正常重播。
- 长测应用绘制帧数从 204 到 204，后台未新增界面绘制。导出文件包含 12 次后台省电心跳，无播放链接、原生错误或最终播放失败；日志页面与 Android 分享导出路径已实际验证。证据见 artifacts/playback-fixes/power-save/result.json 和 exported-diagnostics.txt。
- 首轮长测在 225 秒附近因 ADB 状态读取失败退出，应用日志当时仍正常推进；增加有限的工具读取重试后重新完成完整 6 分钟，未把两次测试时长相加。
- 本轮不使用账号进行登录、歌单或评论写入。厂商后台限制、长时间网络切换和实际手机的耗电表现仍需在对应真机上复测，不以模拟器录像宣称帧率或功耗提升比例。

运行检查：

```powershell
dotnet run --project tests/Petrichor.Core.Tests
dotnet run --project tests/Petrichor.Playback.Tests
pwsh -NoProfile -File scripts/Build-Android.ps1
python scripts/Verify-PlaybackFixes.py
python scripts/Verify-BackgroundPlayback.py
python scripts/Verify-PowerSavePlayback.py
```

界面脚本只针对本机的可丢弃 Android 15 测试模拟器，需要已有 qa.flac 测试音频与公开播客测试记录；不会对连接的个人手机执行这些步骤。系统省电测试使用 Android 自身的省电开关，并在结束时恢复模拟器电池和曲库设置。

## 平台依据

- [Android 后台 MediaPlayer 与唤醒锁](https://developer.android.com/media/platform/mediaplayer/background)。
- [WifiManager 锁模式及版本限制](https://developer.android.com/reference/android/net/wifi/WifiManager#WIFI_MODE_FULL_HIGH_PERF)：新 Android 的 Wi-Fi 锁策略由平台决定，锁不能绕过关闭网络或厂商进程回收。
- [MediaMetadata.Builder.putBitmap](https://developer.android.com/reference/android/media/MediaMetadata.Builder#putBitmap(java.lang.String,%20android.graphics.Bitmap))。
- [ViewTreeObserver.OnPreDrawListener](https://developer.android.com/reference/android/view/ViewTreeObserver.OnPreDrawListener)。

安装包：artifacts/android/Petrichor-1.0.10.apk；对应源码与许可：artifacts/android/Petrichor-1.0.10-source.zip。