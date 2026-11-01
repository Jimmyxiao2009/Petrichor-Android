# Android 验证记录

这是初版1.0.0的验证记录；1.0.1的界面对照资料见 [UI-REFINEMENT.md](UI-REFINEMENT.md)，当前1.0.2的账号密码登录资料见 [PASSWORD-LOGIN.md](PASSWORD-LOGIN.md)。

日期：2026-10-20。交付文件：`artifacts/android/Petrichor-Android.apk`。

## 已通过

| 范围 | 证据 |
| --- | --- |
| Release 构建、签名 APK | `artifacts/android-final-build.log`，0错误；3条共享核心旧目标提示 |
| 业务检查 | `artifacts/core-tests.log`，全部81项通过 |
| 公开接口 | `artifacts/core-live-tests.log`，按「晴天」查节目、读取节目详情、定位所属播客成功 |
| 原生启动与覆盖安装 | Android 15 / API35 / x86_64 模拟器，`final-launch.txt`，冷启动 Status: ok |
| 原生搜索与播客 | Coldplay 单曲/节目搜索，分类切换后标题、输入框仍保留；进入「欧美｜分享」播客，523期详情可读取 |
| 长按菜单 | 长按 Yellow，菜单曲目匹配；未触发播放；返回列表后可继续操作 |
| 原生播放 | Viva La Vida——Coldplay 播放；Android MediaSession 为 PLAYING，暂停为 PAUSED；`final-playback.txt` |
| 外观 | 动态系统蓝色、深色模式、关闭动态取色后的默认配色；播放栏固定贴底 |
| 登录页面 | 手机验证码输入、空号码本地校验、真实登录二维码生成；没有请求短信或扫码确认 |

截图位于 `artifacts/android-smoke/index.html`。最后一次构建仅补齐开关滑块的动态颜色；在该构建上复核安装、启动和主题，其他流程在本轮前一构建上验证。APK 指纹见同目录 `report.json`。

## 验证边界

没有登录真实账号。手机短信实际接收、网易云账号风控、新建/添加/移除歌单的远程写入，以及评论发送等账号操作，仍需实际账号验收。协议检查覆盖短信参数、会话验证、歌单所有权、批量移除、失败传播及取消喜欢语义；这些检查不能替代真实账号操作结果。

仅使用模拟器进行运行验证，未连接实体手机；ARM64 包已随同打包，厂商后台保活、电池策略、壁纸变更和实体设备音频路由仍需真机验证。安装包使用本机开发证书签名，可侧载安装，尚未作为商店版本发布。

## 已修复的运行问题

- Release JNI 注册优化造成的启动异常：关闭 `AndroidEnableMarshalMethods` 后重新构建与安装。
- Android 列表清空/重填后搜索头部错位：将分页按钮移出列表回收区域。
- 深色模式下输入框文字与状态栏图标对比不足：统一动态文字资源和系统栏外观。
- 暂停时无意义的定时界面更新：仅在播放或睡眠计时需要时刷新。

构建和复现命令见 [ANDROID.md](ANDROID.md)。
