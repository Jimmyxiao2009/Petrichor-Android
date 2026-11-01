# 1.0.9 · 播放功能、手势与页面布局

继续使用 MAUI 原生界面与 Petrichor.Core，保留 Mooic 的出处和 GPL-3.0 许可。

## 本轮调整

- 播放功能页去掉外层黑色圆角容器和歌曲信息，功能直接排列在页面背景上。将原 Mooic 菜单的收藏到歌单、下载、评论、分享、下一首播放、喜欢、歌手、专辑入口与现有音质、睡眠定时、歌词入口合并。
- 功能页打开评论、歌手、专辑时直接导航；返回仍停留在功能页。功能页进入当前队列时直接交叉淡入，不经过封面页。
- 播放封面下滑打开当前队列，上滑进入功能页。队列上滑、功能页下滑返回播放；有可滚动内容时优先滚动，进度条保持独立触摸所有权。
- 共享封面转场复用原生已解码图片，由窗口上的 Drawable 图层绘制；等待终点布局稳定后同步启动封面与页面淡入，隐藏源图避免双影。播放器背景保持原位，功能和队列控件的挂载延后到进入动画结束。歌词改为点击后加载，分批建立行视图。
- 歌单滚动时，封面与名称渐变收进顶部一行。原有歌曲多选、移除和收藏逻辑保留。
- 排行榜改为原生两列卡片，官方封面仅作小图标；探索页四个“更多音乐”入口改为两行两列。
- 每日推荐以第一首推荐歌曲的封面作为背景，向页面底色渐变，保留日期与播放全部操作。未登录时显示占位封面和原有登录提示。
- 根页面明确由 Shell 底栏处理底部安全区，迷你播放条不再重复保留底部安全区；返回时重新请求系统 inset。

## 依据

安全区处理核对了 [MAUI SafeAreaEdges 文档](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/safe-area?view=net-maui-10.0) 和 [MAUI 10.0.110 ShellItemRenderer](https://github.com/dotnet/maui/blob/10.0.110/src/Controls/src/Core/Compatibility/Handlers/Shell/Android/ShellItemRenderer.cs) 的底栏 inset 重新分发逻辑。

封面图层按 [Android ViewOverlay 文档](https://developer.android.com/reference/android/view/ViewOverlay) 使用宿主窗口坐标绘制，转场结束后移除。通过 ValueAnimator 更新绘制边界并主动重绘宿主，避免临时 ImageView 属性动画与窗口覆盖层的刷新时机相互影响。

原有原生波浪进度条、把手两侧留白、拖动预览和无损播放路径保留，设计依据见 [1.0.8 记录](UI-1.0.8.md)。

## 验证与产物

Release 构建日志：`artifacts/ui109-build-release.log`。共享后台 119 项检查日志：`artifacts/ui109-core-tests.log`。

实际 Release 界面截图、交互检查、录屏和安装信息放在 `artifacts/ui-1.0.9`。交互检查脚本为 `scripts/Verify-Player109.py`。模拟器使用现有本地 FLAC 测试音频，公开榜单及评论为只读检查。未进行短信发送、登录凭证输入、评论发布、歌单云端写入或系统分享发送。

封面转场通过运行画面检查，不将录屏作为帧率或性能提升比例的证明。每日推荐真实内容依赖登录，本轮不以游客占位图证明账号推荐封面加载成功。

安装包：`artifacts/android/Petrichor-1.0.9.apk`。对应源码与许可：`artifacts/android/Petrichor-1.0.9-source.zip`。

最终 Release 的 31 项界面／交互与画面检查通过，录屏实际编码时长约 16.89 秒。Android 15 模拟器三次根页面测量的播放条与导航栏间隙均为 0 px。Pixel 3 / Android 12 已完成保留数据的覆盖安装并核对哈希，未强行切换用户手机的前台页面。
