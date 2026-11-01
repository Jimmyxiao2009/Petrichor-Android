# 1.0.2：账号密码登录

登录页增加「账号密码」页签，支持手机号和网易邮箱，保留「手机验证码」及「扫码登录」。沿用现有下划线页签、浮动标签输入框和动态颜色；不改动已保存的35屏设计稿。

- 手机号可修改国家区号，默认 +86；输入邮箱时隐藏区号。密码保留大小写、空格及 UTF-8 字符。
- 密码默认隐藏，可切换显示/隐藏。登录提交和键盘提交共用互斥操作，避免重复请求；请求期间禁用方式切换。
- 按协议计算密码摘要，并放入现有加密 HTTPS 请求；只在设备安全存储中保存会话 Cookie，不存储密码或摘要。退出页面时清空密码。
- 登录响应后继续验证会话和用户资料，验证成功才返回。错误和额外验证提示原样显示；验证码、扫码入口继续可用。

协议参照上游实现：[手机号登录](https://github.com/neteasecloudmusicapienhanced/api-enhanced/blob/master/module/login_cellphone.js)、[邮箱登录](https://github.com/neteasecloudmusicapienhanced/api-enhanced/blob/master/module/login.js)。本应用直接请求网易云，不依赖该项目的代理服务。

本轮新增17项业务检查，总计98项通过：涵盖两种请求参数、UTF-8摘要、空输入、国际区号、真实会话校验、错误/风控提示、会话失效清理及取消请求。检查使用内存 HTTP 替身，没有向网易云提交账号密码；日志为 `artifacts/password-login/core-tests.log`。

构建日志：`artifacts/password-login/build.log`。安装包：`artifacts/android/Petrichor-Android.apk`，版本1.0.2 / 版本号3，可覆盖同证书测试版。

Release 构建0错误，3条 NETSDK1215 提示来自保留的共享核心旧目标。在 Android 15 x86_64 模拟器覆盖安装并冷启动成功，耗时3774ms，运行日志未发现致命异常。原生交互复核覆盖三种页签、空账号提示、邮箱区号隐藏、密码默认遮挡与显隐、退出清空密码、验证码入口和二维码生成，以及动态配色的浅色/深色外观；未提交真实账号凭据或发送短信。[原生截图](../artifacts/password-login/index.html)，指纹和验证范围见同目录 `report.json`。

真实账号的密码登录成功、验证码接收及服务端风控仍需账号验收；本轮不使用真实凭据自动登录。
