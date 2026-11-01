# 1.0.7 · 下滑播放功能与 Material 3 调整

在现有 Mooic UI 移植版上恢复 Petrichor 的播放功能分页，继续使用 MAUI、AppServices 和 Petrichor.Core。没有另建播放器或登录后台。

## 交互

- 封面状态向下拖动打开功能区；反向向上拖动收起。页面跟手，松手后按距离和速度吸附，边界有阻尼。短距离拖动取消。
- 点击歌曲信息旁的更多按钮也能打开；面板返回按钮和系统返回先收起功能区。
- 面板中的收藏到歌单、下载、评论、队列、音质、睡眠定时、歌词与歌曲操作连接原有功能。音质显示实际播放格式。
- 点击封面仍进入独立歌词状态；歌词滚动不触发功能分页。进度条触摸优先交给原生 SeekBar。
- 内容超过可用高度时先交给内部滚动容器；滚动到边界后才接受切换手势。短高度播放器也支持滚动。

## Google 文档依据

查阅 [Google Material Android Bottom Sheet 文档](https://github.com/material-components/material-components-android/blob/master/docs/components/BottomSheet.md)、[Android 触摸目标规范](https://developer.android.com/develop/ui/compose/accessibility/api-defaults)、[官方 Sheet 设计令牌](https://github.com/androidx/androidx/blob/androidx-main/compose/material3/material3/src/commonMain/kotlin/androidx/compose/material3/tokens/SheetBottomTokens.kt) 和 [Material 动效令牌](https://github.com/material-components/material-components-android/blob/master/lib/java/com/google/android/material/motion/res/values/tokens.xml)。

采用低层级表面容器、28 dp 圆角、32×4 dp 把手、至少 48 dp 操作目标和 standard easing `cubic-bezier(.2,0,0,1)`。播放主按钮使用 Primary / OnPrimary；循环和随机选中状态使用 SecondaryContainer，其他控制保持中性色。菜单与设置行继续使用 Android 12+ 系统动态调色板，旧系统使用现有备用主题。

这里的“向下打开功能区”是用户要求的应用内分页手势。Google 标准底部面板通常向下关闭；本实现借用其表面层级和组件规范，没有把自定义手势声称为标准 BottomSheet 行为。

## 验证与交付

Release 1.0.7 / code 8。Android 15 模拟器实际截图、交互检查与录屏见 `artifacts/md3-functions`，最终检查项目见 `checks.json`。后台可执行检查程序 119 项通过。未使用真实账号发送短信、发表评论或写入歌单；会员资源权限和账号写入未新增实测。

同一最终 Release APK 上完成 18 项交互检查，包括短拖取消、进度条优先级、菜单反复打开后的点击、歌词独立滚动、系统返回、禁用动画、现有功能路由和横屏滚动。录屏实际编码时长约 13.53 秒；已查看原生截图及转场抽帧。旋转后一次 UIAutomator 空闲检测超时，使用脚本的 `--resume` 完成剩余断言；过程日志保留。

安装包 `artifacts/android/Petrichor-1.0.7.apk`；对应源代码包 `artifacts/android/Petrichor-1.0.7-source.zip`。Mooic 的 GPL-3.0 许可与出处随应用和源码保留。
