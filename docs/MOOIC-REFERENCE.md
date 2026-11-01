# Mooic 3.0.1 实机参考与 Petrichor 1.0.5 对照

用户提供：[Mooic Releases](https://github.com/kid-depress/Mooic/releases)。本轮已下载 3.0.1 安装包，在独立测试模拟器安装、操作并截图；源码检出发布标签 3.0.1，commit `e54896498c43f9fcad26352d59f8cda9a393e6b7`。没有改动 Petrichor 的产品代码或替换已交付 APK。

同一 Android 15 模拟器、1080×2400、density 420。首页及播放页另拍浅色模式对照；歌曲与主题种子不同，不据此比较专辑美术或配色优劣。所有用于结论的图片均在本次运行中捕获并打开检查；转场中重影的截图已重拍。`10a-petrichor-home-loading.png` 仅保留为加载过程证据，不作为完成态对照。

## 实际查看的流程

1. **首页 → 选择歌曲 → 迷你播放器：内容优先。** `08-mooic-home-light.png`、`02-mooic-mini.png`。第一屏直接呈现可操作的歌曲列表与歌单；标题、正文、封面有固定层次，底栏只有图标。Petrichor 对照 `10-petrichor-home-current.png`：大宣传标题、副标题、完整搜索栏与三个大入口挤占首屏，实际音乐内容开始得明显更靠下。改进重点是入口组织及纵向占用，而不是继续统一缩小全部字号。
2. **迷你播放器 → 完整播放：视觉焦点明确。** `09-mooic-player-light.png` 对照 `11-petrichor-player-current.png`。Mooic 的封面与歌曲信息形成连续分组，顶栏只保留收起入口。Petrichor 的封面区域按剩余高度居中，封面上下出现大空白；信息及控制区断开，顶部通用页面栏重复表达“正在播放”。需要重新分配封面、信息和控制区空间。
3. **点击封面 → 歌词 → 返回：歌词有独立阅读空间。** `04-mooic-lyrics.png`。顶部小封面保留歌曲上下文，主体交给歌词列表。Petrichor 当前把歌词塞在封面槽位里，同时保留其余控制，阅读高度有限。应重新处理歌词状态的版式与返回关系，保留用户要求的点击封面入口及手动滚动能力。
4. **播放页更多 → 底部菜单：操作集中。** `05-mooic-player-menu.png`。短菜单从底部展开，背景降暗，分组圆角一致。Petrichor 还需要统一功能面板、曲目菜单、图标反馈和展开／关闭动效。Mooic 此菜单只有少量功能，不能据此移除用户要求的下载、评论、音质、歌单等功能。
5. **探索／我的：主导航与内容层级统一。** `06-mooic-explore.png`、`07-mooic-library.png`。探索用紧凑的榜单入口，“我的”未登录状态非常简单。已看过空账号状态；没有登录，不评价同步歌单、账号权限或评论等未访问流程。

## 源码中确认的做法

- [Player.kt](https://github.com/kid-depress/Mooic/blob/3.0.1/app/src/main/java/com/rcmiku/music/ui/components/Player.kt)：直接使用 Compose Material 3 的 Slider，按媒体场景设置 4 dp 轨道、4×20 dp 把手、2 dp thumbTrackGap；播放器根据高度切换 8／24 dp 纵向间距。Petrichor 上一轮直接使用 44 dp 高把手，虽来自通用参数，但与本项目细媒体轨道的整体比例不合适。需要保持用户要求的可见间隔和波浪，同时重新协调把手的视觉高度与交互区域。
- [PlayerTransform.kt](https://github.com/kid-depress/Mooic/blob/3.0.1/app/src/main/java/com/rcmiku/music/ui/screen/PlayerTransform.kt)：迷你播放器、完整播放、队列和歌词在同一状态结构中，封面与容器使用 sharedElement／sharedBounds。Petrichor 的 PageMotion 目前主要是通用整页淡入和平移；添加“有动画”不足以表达几个播放状态之间的关系。
- [Dimensions.kt](https://github.com/kid-depress/Mooic/blob/3.0.1/app/src/main/java/com/rcmiku/music/constants/Dimensions.kt)：统一列表 64 dp、缩略图 48 dp、迷你播放器 64 dp、底导航 68 dp，以及强调曲线和进入／退出时长。应建立组件级尺寸和动效规则，避免只靠全局 .78／.85 缩放。
- [HomeScreenViewModel.kt](https://github.com/kid-depress/Mooic/blob/3.0.1/app/src/main/java/com/rcmiku/music/viewModel/HomeScreenViewModel.kt)：先从 DataStore 载入推荐缓存，再启动请求；成功后写入 StateFlow 并保存，失败时已有内容保留。我们已有缓存层，但多数页面后台更新仅标记下次出现时刷新，当前展示状态、列表差量更新及导航保留仍可改进。
- HomeScreen／PlaylistScreen 使用 LazyColumn 等列表；这些结构可作为状态和按需创建的参考。没有做可比的帧率基准，不能仅据框架或 APK 大小断言性能差距比例。

## 下一轮实现基准

先按这些已见差距重排首页和播放状态的结构：减少入口区占用、连续分组封面与信息、重设媒体滑块比例、让歌词获得阅读空间，并将迷你播放展开、歌词返回、队列和功能面板的动效串起来。再逐页检查列表、歌单、搜索、播客、评论、下载和设置的同一套尺寸与分组规则。

继续保留原用户要求：MD3、动态取色、贴底播放栏、紧凑界面、波浪进度、登录方式、歌单创建／移除、播客歌曲搜索、下载目录和无损实际格式。Mooic 用于建立完成度和结构参照，不意味着删除已有需求或照搬第三方代码。

## 证据和限制

图集与原生参考录屏：`artifacts/mooic-reference/index.html`。参考录屏实际编码时长约 5.37 秒，提取帧仅用于确认播放状态间封面尺寸变化，没有用它推导响应速度或帧率。

未测试真实登录、长期使用、真实账号收藏或歌单修改。没有进行 TalkBack 全流程或无障碍合规审计。参考界面也有图标缺少可访问名称、部分低强调文字较淡等可见风险，不能将其所有细节视为无需判断的标准。

本次没有生成新的设计稿或修改 APK，当前交付仍为 Petrichor 1.0.5。
