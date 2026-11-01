# 1.0.8 · 播放进度条与手势协调

在现有 MAUI 客户端中调整音乐进度条，继续使用原生 Android SeekBar 和 Petrichor 播放后台。

## 视觉与操作

- 4 dp 轨道，竖形把手静止为 4×32 dp，按下后过渡为 2×36 dp；触摸区域保留 48 dp。
- 把手与轨道圆头之间两侧各留 6 dp 空隙，右端保留停止点，接近终点时隐藏避免重叠。
- 播放时已播放部分显示连续运动的波浪；暂停和拖动时平滑收直。关闭系统动画时停止波浪运动，仍呈现可辨认的进度和状态。
- 拖动时在原有进度条上方留白显示时间预览，下面的时间同步预览；松手后提交一次跳转。等待原生 seek 完成期间保留目标位置，避免短暂跳回旧位置。
- 播放期间由原生控件更新视觉位置，不逐帧测量 MAUI 页面。未把模拟器录屏作为帧率或性能提升比例的依据。

## 原有下滑交互

封面下滑仍打开功能菜单，上滑／返回收起，歌词保持独立滚动。手指从进度条开始后由进度条持有该次触摸；首次明确纵向运动取消进度拖动并吞掉剩余触摸，避免触发功能菜单。确定横向拖动后允许手指有纵向偏移。多指与系统取消也取消拖动。顶栏下箭头继续返回上一页。

原生焦点和键盘操作保留，左右方向键按约一秒调整。本轮未做完整 TalkBack 验收。

## Google 参考

参考 [Google Material Slider 文档](https://github.com/material-components/material-components-android/blob/master/docs/components/Slider.md) 的把手／轨道间隙、停止点、触摸目标和交互状态；参考 [Android SystemUI SquigglyProgress](https://android.googlesource.com/platform/frameworks/base/+/refs/heads/android13-release/packages/SystemUI/src/com/android/systemui/media/SquigglyProgress.kt) 的媒体波浪状态切换。播放器中的把手尺寸为紧凑音乐布局适配值。

核对了当前 [MAUI Android SliderHandler](https://github.com/dotnet/maui/blob/10.0.110/src/Core/src/Handlers/Slider/SliderHandler.Android.cs) 的触摸／数值事件顺序，使用自有 MusicSliderHandler 协调原生触摸和现有播放逻辑。

## 验证

最终项目版本 1.0.8 / code 9，Release APK 的实际交互结果、原生截图、录屏、像素测量和安装包哈希见 `artifacts/slider-review`。

在 Android 15 模拟器播放现有 90 秒本地 FLAC 测试音频，检查拖动预览、松手跳转、取消拖动、起终点、原生焦点／键盘、封面下滑菜单、菜单选项与收起入口。可见间隙和波浪变化由实际截图测量，数值包含像素取整与抗锯齿边缘。

18 项最终 Release 交互检查和 119 项后台检查通过。实际截图中的可见留白两侧各约 6.48 dp；暂停轨道纵向跨度 10 px，播放波浪跨度 34 px，固定区域的相邻帧波形发生变化。实际录屏编码时长约 10.02 秒，已检查截图和转场抽帧。

未新增真实账号、会员在线无损或云端歌单写入测试。现有 Core 源码保持不变；共享后台检查日志为 `artifacts/slider-core-tests.log`。原生轨道／把手的视觉和手势实现位于 `UI/MusicSlider.cs`、`MusicThumbDrawable.cs`、`WaveTrackDrawable.cs`，页面预览和提交逻辑位于 `Pages/PlayerPages.cs`。

安装包：`artifacts/android/Petrichor-1.0.8.apk`。对应源码含 Mooic 原有出处与 GPL-3.0 许可：`artifacts/android/Petrichor-1.0.8-source.zip`。
