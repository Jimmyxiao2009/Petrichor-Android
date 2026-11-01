# 1.0.6 · Mooic UI 移植到现有 Petrichor

用户澄清：移植 Mooic 的 UI 和交互，接我们的 codebehind，适配现有功能。活动工程仍是 `src/Petrichor.Android`，包名 `app.petrichor.music`；共享后台仍是 `Petrichor.Core`。误建的独立 native 工程已停止作为产品推进，仅留作参考。

## 参考与实现映射

参考 [Mooic 3.0.1](https://github.com/kid-depress/Mooic/tree/3.0.1)，提交 `e54896498c43f9fcad26352d59f8cda9a393e6b7`。按已在同一模拟器捕获的真实界面，结合 HomeScreen、NavigationTitle、Player、Lyrics、MiniPlayer、MainScreen 的源码结构移植到 MAUI。GPL-3.0 许可和出处已放入应用 Raw 资源，设置页可查看出处。

| 界面 | 移植内容 | 保留的 Petrichor 接线 |
|---|---|---|
| 主页 | 直接呈现四行横向歌曲列表、横向歌单；22 sp 标题、64 dp 曲目行、48 dp 缩略图 | Core 日推、公开榜单与推荐歌单读取；访客使用公开榜单歌曲，登录后日推 |
| 探索 | 四行横向榜单卡片；新增功能使用同一视觉层次 | 排行榜、每日推荐、私人 FM、播客与节目、歌单广场 |
| 我的 | 中央紧凑登录卡；本地和最近播放入口 | 原登录页与 SecureStorage 会话；现有歌单、新建／移除、收藏、本地与下载 |
| 迷你播放 | 64 dp 贴底栏；真实队列为空时隐藏；播放和队列入口 | 同一个 PlaybackController 和 Queue |
| 播放页 | 大方形封面、相邻的歌曲信息和操作、圆形控制区；删除重复标题 | 播放、随机、三态循环、实际音质、下载、评论、收藏、睡眠、队列 |
| 歌词 | 点击封面切换；小封面上下文栏和独立可滚动阅读区域 | 原歌词缓存、同步高亮、点击歌词 seek；移除旧功能上下滑动分页 |
| 搜索 | 由顶栏进入，六项下划线分类，返回上页 | 原歌曲／歌单／歌手／专辑／播客／节目搜索和所属播客跳转 |
| 底部菜单 | 统一圆角分组和容器颜色，保留滚动 | 原 Actions 菜单、账号校验、歌单创建／添加／移除 |

`UI/MooicLayout.cs` 定义移植组件；`ArtworkTransition.cs` 用原生覆盖图层做共享封面的平移／缩放，避免逐帧测量 MAUI 页面。进入播放器、返回迷你播放器、封面／歌词状态切换已接入。`PageMotion` 保留评论等页面转场与关闭系统动画支持。

进度条保留原生 SeekBar 的触摸和可访问操作；视觉把手改为参考中的 4×20 dp，交互区域保持 48 dp；保留用户要求的 6 dp 间隙、停止点和播放时波浪。实际音质继续取原有播放解析结果，未把请求音质当成实际无损标记。

## 后台与数据

对照改 UI 前备份 `artifacts/backups/maUi-before-1.0.6.zip`，Core 源文件没有变化。AppServices、PlaybackController、OfflineLibrary、DownloadFolder、会话与响应缓存继续使用现有实现。没有把 Kotlin 的登录、API、下载或播放器作为另一套后台接进来。

公开读取先使用现有 ResponseCache；界面中的加载骨架保留。首页列表使用虚拟化横向 CollectionView。没有用本轮不同场景的结果承诺帧率或启动速度比例。

## 验证

Release 1.0.6 / code 7 已构建；`scripts/Build-Android.ps1` 为现有构建入口。Core 可执行检查程序运行 119 项通过。

模拟器：Android 15，1080×2400，density 420。实际截图、交互检查和录屏位于 `artifacts/maui-mooic-port`；最终检查结果以 `checks.json` 与 QA 日志为准。包括真实包的导航、菜单、独立歌词滚动、三态循环、评论、六类搜索、登录入口、系统下载文件夹选择器、断网重启缓存。

本地 FLAC 已实际解码播放。没有真实账号，短信发送、密码／验证码成功登录、在线会员无损和账号歌单写入仍未作真实账号验证。本轮未发送短信、未发表评论或修改线上歌单。

交付包：`artifacts/android/Petrichor-1.0.6.apk`，33,264,785 字节。SHA256 `65DD2138D8AC455714055E6467D2915DC24A98B669792AD96F0F80F431165D96`，已安装 APK 校验一致。对应可编辑源码（含许可）为 [Petrichor-1.0.6-source.zip](../artifacts/android/Petrichor-1.0.6-source.zip)。

实际通过 17 项 Release UI 检查，另补本地 FLAC 播放和 49.8 / 90 秒的原生 seek 与波浪验证，共 19 项。原生转场录屏编码时长约 7.06 秒，已检查展开中间帧和歌词状态。中间封面来自 mini 位图，放大阶段比最终封面柔和；未据此宣称完整性能验收。
