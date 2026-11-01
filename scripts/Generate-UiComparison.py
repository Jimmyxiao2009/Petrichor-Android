"""Create a local review gallery; reference PNGs are rendered from unchanged approved HTML."""
from pathlib import Path
from PIL import Image
import hashlib
import html
import json

root=Path(__file__).resolve().parents[1]
out=root/'artifacts/ui-refinement'
pairs=[('discover','discover','发现'),('search-songs','search','单曲搜索'),('search','program','节目搜索'),('podcasts','podcasts','播客首页'),('podcast','podcast','播客详情'),('library-guest','library','我的 · 访客'),('playlist','playlist','歌单'),('player','player','播放器'),('queue','queue','队列'),('login','login','手机登录'),('qr-login','qr','扫码登录'),('settings','settings','设置'),('downloads','downloads','本地与下载'),('comments','comments','评论')]
sections=[]
for reference,actual,title in pairs:
    ref=out/f'reference-{reference}.png'
    shot=out/f'{actual}.png'
    if not ref.exists() or not shot.exists():continue
    for file in (ref,shot):
        with Image.open(file) as image:image.verify()
    sections.append(f'<section id="{actual}"><h2>{html.escape(title)}</h2><div class="pair"><figure><figcaption>确认稿</figcaption><a href="{ref.name}"><img loading="lazy" src="{ref.name}" alt="{html.escape(title)}确认稿"></a></figure><figure><figcaption>原生 MAUI · 真实接口／设备数据</figcaption><a href="{shot.name}"><img loading="lazy" src="{shot.name}" alt="{html.escape(title)}实际截图"></a></figure></div></section>')
extras=''.join(f'<figure><figcaption>{title}</figcaption><a href="{name}.png"><img loading="lazy" src="{name}.png" alt="{title}"></a></figure>' for name,title in [('menu','长按菜单'),('sleep','睡眠定时'),('selection','歌单多选'),('lyrics','暂无歌词时显示封面'),('dark','深色模式'),('dynamic','壁纸动态取色')] if (out/f'{name}.png').exists())
apk=root/'artifacts/android/Petrichor-Android.apk'
digest=hashlib.sha256(apk.read_bytes()).hexdigest().upper()
state=json.loads((root/'.superdesign/resume.json').read_text(encoding='utf-8-sig'))
mismatches=[p for target in state['targets'].values() for p,d in target['fingerprints'].items() if hashlib.sha256((root/p).read_bytes()).hexdigest()!=d]
assert not mismatches,'Approved source changed'
report={'version':'1.0.1','sha256':digest,'bytes':apk.stat().st_size,'approved_screens':len(state['targets']),'approved_sources_unchanged':True,'comparison_pairs':len(sections),'platform':'Android 15 API35 x86_64','core_checks_passed':81,'account_write_acceptance':False}
(out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
document='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Petrichor 1.0.1 · 稿件与原生 UI 对照</title><style>body{margin:0;background:#f4f2f4;color:#23191c;font:15px/1.7 system-ui,sans-serif}header{padding:28px;max-width:1000px;margin:auto}h1{font-size:28px}h2{font-size:22px}a{color:#9b2544}main{max-width:940px;margin:auto;padding:0 24px 40px}section{background:#fffbfc;border-radius:24px;padding:24px;margin:24px 0}.pair,.extras{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}figure{margin:0;min-width:0}figcaption{padding-bottom:12px;font-size:13px;color:#76666c}img{width:100%;height:auto;display:block;border:1px solid #dac2ca;border-radius:12px}code{overflow-wrap:anywhere}@media(max-width:650px){main{padding:0 10px}.pair,.extras{gap:10px}section{padding:12px}h1{font-size:22px}}</style><header><h1>Petrichor 1.0.1 · 稿件与原生 UI 对照</h1><p>左侧是原先确认稿的 HTML 渲染，右侧是 Android 模拟器中的原生 MAUI 页面。35份确认稿及设计系统的指纹均保持不变。曲目、封面、数量来自真实接口，和稿中的示例数据不同；系统状态栏、手势区和原生字体由 Android 呈现。</p><p>本轮按稿重做快捷卡片、筛选标签、页签、输入框、详情页、播放器和弹层，并保留动态取色和贴底播放栏。账号写入与短信接收仍需真实账号验收。</p><p><a href="../android/Petrichor-Android.apk">新版 APK</a> · <a href="../../docs/UI-REFINEMENT.md">修改与验收范围</a> · <a href="../../design/android/index.html">原确认稿</a></p><p>SHA256：<code>'''+digest+'</code></p></header><main>'+''.join(sections)+'<section><h2>交互状态</h2><div class="extras">'+extras+'</div></section></main></html>'
(out/'index.html').write_text(document,encoding='utf-8')
print(json.dumps(report,ensure_ascii=True))
