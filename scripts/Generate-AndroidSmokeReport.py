"""Build a local gallery from actual emulator captures, with APK provenance."""
from pathlib import Path
import hashlib
import html
import json
import struct

root = Path(__file__).resolve().parents[1]
out = root / "artifacts/android-smoke"
apk = root / "artifacts/android/Petrichor-Android.apk"
shots = [
    ("final-discover", "发现与贴底播放栏"),
    ("final-search", "单曲搜索"),
    ("final-long-menu", "Yellow 长按菜单"),
    ("final-program", "节目搜索与所属播客入口"),
    ("final-podcast", "所属播客详情"),
    ("final-playing", "真实音频播放"),
    ("final-login", "手机验证码登录"),
    ("final-qr", "扫码登录（截图二维码会过期）"),
    ("final-settings", "动态取色设置"),
    ("final-dark", "动态深色模式"),
    ("final-dark-default", "关闭动态取色后的默认深色"),
]
cards = []
for name, title in shots:
    data = (out / (name + ".png")).read_bytes()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", name
    width, height = struct.unpack(">II", data[16:24])
    assert width > 0 and height > 0, name
    cards.append(f'<figure><a href="{name}.png"><img src="{name}.png" loading="lazy" alt="{html.escape(title)}"></a><figcaption>{html.escape(title)}</figcaption></figure>')
report = {
    "date": "2026-10-20",
    "apk": "../android/Petrichor-Android.apk",
    "sha256": hashlib.sha256(apk.read_bytes()).hexdigest().upper(),
    "bytes": apk.stat().st_size,
    "emulator": "Android 15 / API 35 / x86_64",
    "core_checks_passed": 81,
    "account_writes_tested": False,
    "screenshots": [name + ".png" for name, _ in shots],
}
(out / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
(out / "index.html").write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Petrichor 安卓模拟器验证</title><style>body{font:16px/1.6 system-ui,sans-serif;background:#fffbfc;color:#23191c;max-width:1400px;margin:auto;padding:32px}h1{font-size:32px}p{max-width:850px}code{overflow-wrap:anywhere}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(230px,1fr));gap:24px}figure{margin:0;background:#f0e3e7;padding:12px;border-radius:20px}img{width:100%;height:auto;border-radius:12px}figcaption{padding:8px}a{color:#8e113f}</style><h1>Petrichor 安卓模拟器验证</h1><p>以下为原生应用的实际截图。81 项业务检查通过，模拟器验证了启动、搜索、节目所属播客导航、长按菜单、播放、登录页面与主题切换。短信实际收取及真实账号歌单写入仍需账号验收。</p><p><a href="../android/Petrichor-Android.apk">安装 APK</a> · <a href="../../docs/ANDROID-VALIDATION.md">验证范围</a> · <a href="report.json">构建指纹</a></p><p>APK SHA256：<code>''' + report["sha256"] + '</code></p><div class="grid">' + "".join(cards) + '</div></html>', encoding="utf-8")
print(json.dumps(report, ensure_ascii=True))
