# Petrichor 1.0.9 · 播放功能与页面检查

final result: passed — reviewed actual native light/dark functions, direct navigation, queue gestures and artwork motion.

播放功能页去掉外层黑底和当前歌曲信息，功能直接排列在页面背景上；补入原 Mooic 菜单的分享、下一首、歌手、专辑等入口。评论直接打开，返回仍在功能页；当前队列直接切换，不再经过封面。

最终 Release 完成 31 项交互与画面检查，119 项共享后台检查通过。验证本地 FLAC、原生进度操作、封面下滑队列／上滑功能、菜单触摸目标、音质和定时入口、歌词返回、在线歌曲评论返回、四宫格入口、原生排行榜、歌单顶栏收纳、游客每日推荐占位、深浅色与关闭系统动画的状态。

封面由原生窗口 Drawable 绘制，复用已解码图片；稳定终点布局后同步启动页面与封面动画，补齐 Shell 根页面的缩回目标，功能／队列控件延后挂载。实际录屏编码时长约 16.89 秒，检查了连续缩放和缩回的抽帧。未将模拟器录屏作为帧率基准。

三次根页面截图的原生层级测量均显示播放条底边与导航栏顶边相接，额外间隙为 0 px。深色功能页四处外边缘的背景像素相同，没有外层黑色容器。

证据：artifacts/ui-1.0.9/index.html、checks.json、inset-measurements.json、motion-contact-sheet.png。Google／MAUI 依据和范围见 docs/UI-1.0.9.md。保留原进度条实现与此前版本证据。

1.0.9 / code 10 已通过无线 ADB 覆盖安装到 Pixel 3 / Android 12，保留数据并核对安装包哈希。界面自动检查在 Android 15 模拟器执行；未在用户正在使用的手机上强行切换页面。没有账号云端写入、短信发送或分享发送；真实每日推荐封面仍需登录账号验收。Core 源码与本轮前备份逐字节一致，Mooic 的出处和 GPL-3.0 许可随包保留。
