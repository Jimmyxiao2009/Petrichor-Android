# Petrichor 1.0.7 · 下滑播放功能检查

final result: passed — reviewed the native player, functional panel and transitions.

在现有 MAUI Mooic UI 移植版上恢复 Petrichor 功能分页。继续使用原有 AppServices、Core、账号、队列、播放、评论、下载与缓存。

实际检查 Android 15 模拟器的竖屏和横屏截图：播放器、独立歌词、功能菜单、音质与睡眠面板、评论。检查了约 13.53 秒的真实录屏及抽帧，包含下滑跟手展开、上滑返回、封面／歌词切换和再次展开。

修正：滑动优先级、短拖取消、页面边界阻尼、关闭系统动画、反复打开后的触摸恢复；横屏封面按可见区域计算，短高度播放器可滚动，功能面板最大宽度 640 dp，内容滚动到边界才切换页面。

最终 APK 上完成 18 项交互断言，结果见 `artifacts/md3-functions/checks.json`。UIAutomator 曾在旋转后报告无法获得空闲；保留日志与现场截图后，通过 `--resume` 在相同 APK 上完成余下检查。119 项 Core 检查通过。

文档与证据：`docs/UI-1.0.7.md`、`artifacts/md3-functions/index.html`、`manifest.json`、`motion-contact-sheet.png`。安装包身份、已安装包哈希、进程崩溃匹配和 Core 源码对照由 `scripts/Package-PlayerFunctions.py` 检查。

Google BottomSheet、触摸目标、Sheet 与动效令牌的官方链接见实现文档。应用使用自定义“下滑打开”分页手势。

未新增真实账号短信、密码成功、会员无损或歌单写入验证；未作完整 TalkBack 审核，也未据模拟器录屏宣称帧率或性能提升比例。Mooic 共享封面过渡中的位图放大限制与 1.0.6 相同。
