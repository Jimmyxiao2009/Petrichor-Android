"""Package the inspected native screenshots and measured results; no mock data."""
from pathlib import Path
import hashlib
import html
import json
from PIL import Image, ImageDraw

root=Path(__file__).resolve().parents[1]
out=root/'artifacts/polish'
def read(name):return json.loads((out/name).read_text(encoding='utf-8-sig'))
inputs=read('native-input-checks.json')
assert len(inputs['checks'])==13 and all(inputs['checks'].values())
assert all(read('navigation-checks.json').values())
assert all(read('playlist-checks.json').values())
package=(out/'package-final.txt').read_text(encoding='utf-8-sig')
assert 'versionName=1.0.3' in package and 'DEBUGGABLE' not in package
installed=read('apk-install-check.json')
assert installed['matches']
steps=[
    ('01 · 发现','首屏先呈现结构，推荐与播客分别加载；保留原稿的层级与双列推荐。',['before-01-discover.png','after-01-discover.png']),
    ('02 · 我的音乐','访客页返回时复用布局，账号变化仍刷新。',['before-02-library.png','after-02-library.png']),
    ('03 · 登录','修复输入贴顶、标签裁切、快速输入乱序和错误提示中的运行时参数名。三种登录入口保留。',['before-03-login.png','after-03-login.png','after-08-sms.png','after-09-qr.png']),
    ('04 · 搜索','30条结果批量更新，列表复用测量；接近底部才显示分页操作，为歌曲留出空间。',['before-04-search-host.png','after-04-search.png']),
    ('05 · 长按菜单','核对长按对应歌曲及其操作列表；长按后不会同时触发普通点击。',['before-05-menu.png','after-05-menu.png']),
    ('06 · 歌单广场','双列封面回收复用；实测分类切换、追加下一页及打开对应歌单。保留封面播放量。',['after-07-catalog.png','after-10-catalog-paged.png']),
    ('07 · 外观','动态配色、浅色与深色切换；播放栏贴边，播放按钮点击区域48dp。截图只能证明当前壁纸配色。',['after-06-settings.png','after-06-settings-light.png']),
    ('08 · 歌单与多选','核对公开歌单详情、全选计数、退出多选和恢复列表。真实账号移除操作仍需账号验收。',['after-11-playlist.png','after-12-multiselect.png']),
    ('09 · 节目搜索与所属播客','以Coldplay公开节目搜索，核对结果中的所属播客入口能进入对应播客详情。',['after-13-program-search.png','after-14-program-parent.png']),
]
for _,_,files in steps:
    for file in files:assert (out/file).is_file(),file
apk=root/'artifacts/android/Petrichor-Android.apk'
report={
    'version':'1.0.3','version_code':4,'configuration':'Release','apk_bytes':apk.stat().st_size,
    'apk_sha256':hashlib.sha256(apk.read_bytes()).hexdigest().upper(),
    'health':'本轮访客范围通过；尚未完成真实账号与实体手机验收',
    'core_checks':98,'native_input':inputs,'playlist_ui':read('playlist-checks.json'),'navigation':read('navigation-checks.json'),'installed_package':installed,
    'scroll':{'before':read('before-performance.json'),'after':read('after-performance.json')},
    'startup':read('startup-comparison.json'),
    'screenshots':[{'step':name,'note':note,'files':files} for name,note,files in steps],
    'limits':['无真实账号登录、短信接收或远程歌单写入','无实体ARM64手机、厂商后台策略或完整无障碍验收','软件渲染及休眠后异常记录不用于对比','冷启动仅计首帧，网络内容加载另计','滚动单场景预热结果相近，不能宣称所有卡顿解决'],
}
(out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
e=html.escape
sections=[]
for name,note,files in steps:
    figures=[]
    for file in files:
        label='修复前 · 1.0.2' if file.startswith('before') else '修复后 · 1.0.3'
        figures.append(f'<figure><figcaption>{label}<small>{e(file)}</small></figcaption><a href="{file}"><img loading="lazy" src="{file}" alt="{e(name+label)}"></a></figure>')
    sections.append(f'<section><h2>{e(name)}</h2><p>{e(note)}</p><div class="screens">'+''.join(figures)+'</div></section>')
scroll=report['scroll'];startup=report['startup']['median_ms']
rows=''.join(f'<tr><td>{e(label)}</td><td>{e(scroll["before"][key])}</td><td>{e(scroll["after"][key])}</td></tr>' for label,key in [('滚动帧数','Total frames rendered'),('Android标记卡顿帧','Janky frames'),('滚动p95','95th percentile'),('滚动p99','99th percentile')])
document='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Petrichor 1.0.3 · 原生界面复查</title><style>
:root{color-scheme:light dark;font:16px/1.65 system-ui,"Microsoft YaHei",sans-serif;background:#16191f;color:#e4e7ee}body{max-width:1140px;margin:auto;padding:40px 24px}h1{font-size:clamp(28px,4vw,44px);line-height:1.2}h2{font-size:24px}p,li{max-width:850px;color:#bfc7d8}a{color:#b5caff}nav{display:flex;flex-wrap:wrap;gap:20px;margin:24px 0}section{padding:28px 0;border-top:1px solid #414855}.screens{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:24px}figure{margin:0;max-width:480px}figcaption{margin:0 0 12px}small{display:block;color:#9faabc;font-size:12px}img{display:block;width:100%;height:auto;border-radius:16px;border:1px solid #414855}table{border-collapse:collapse;width:100%;max-width:720px}th,td{text-align:left;border-bottom:1px solid #414855;padding:12px}code{overflow-wrap:anywhere;font-size:12px}.status{background:#283448;border-radius:20px;padding:20px;margin:24px 0}.status p{margin:0}footer{margin-top:32px;color:#9faabc}
</style><h1>Petrichor 1.0.3<br>实际安卓渲染与修复对照</h1><p>这份记录使用安卓模拟器直接截屏。图片中的封面、歌单及结果来自公开接口，不是设计稿占位数据；公开推荐会随时间变化。点击图片查看原始1080×2400截图。</p><nav><a href="../android/Petrichor-Android.apk">安装 APK</a><a href="../../docs/UI-POLISH.md">修复与验证说明</a><a href="../../design/android/index.html">既有35屏设计预览</a><a href="report.json">完整检查记录</a></nav><div class="status"><p>本轮访客范围通过：98项业务检查、13项原生输入检查。真实账号与实体手机验收仍未完成。</p></div><section><h2>同环境测量</h2><p>Android 15 x86_64，1080×2400，4核/3GB，GTX1650主机GPU加速。旧版源码仅更换包名，与新版同机交替启动。列表均先预热，再滑动8次；不包含构建竞争或软件渲染异常记录。</p><table><thead><tr><th>指标</th><th>1.0.2</th><th>1.0.3</th></tr></thead><tbody>'''+rows+f'<tr><td>进程冷启动首帧中位数 · 3轮</td><td>{startup["before"]}ms</td><td>{startup["after"]}ms</td></tr></tbody></table><p>冷启动三轮样本波动较大，原始数据完整保留，仅作参考；预热后的搜索滚动差异较小。单个模拟器场景不足以证明所有卡顿都已解决；首帧时间也不等于网络内容加载完成。</p><p><a href="before-search-frames.txt">旧版原始帧记录</a> · <a href="after-search-frames.txt">新版原始帧记录</a> · <a href="startup-comparison.json">启动原始数据</a> · <a href="native-input-checks.json">原生输入检查</a></p></section>'+''.join(sections)+f'<section><h2>仍需验收</h2><p>真实账号的密码登录、短信接收、创建歌单和移除歌曲，实体手机上的性能与后台播放策略，以及完整无障碍检查。未使用真实账号执行自动写入；二维码截图仅记录当时的界面状态。</p><small>Release APK · {apk.stat().st_size/1048576:.1f} MiB · SHA256</small><code>{report["apk_sha256"]}</code></section><footer>原始日志与截图保留在 artifacts/polish；历史版本资料不计入本轮通过范围。</footer></html>'
(out/'index.html').write_text(document,encoding='utf-8')
# A single contact sheet makes the same-viewport before/after review explicit.
files=['before-03-login.png','after-03-login.png','before-04-search-host.png','after-04-search.png']
sheet=Image.new('RGB',(1440,850),'#16191f');draw=ImageDraw.Draw(sheet)
for i,file in enumerate(files):
    shot=Image.open(out/file).convert('RGB');shot.thumbnail((350,790));sheet.paste(shot,(i*360,40));draw.text((i*360+8,10),['Login before','Login after','Search before','Search after'][i],fill='white')
sheet.save(out/'comparison.png')
print(json.dumps({'apk_sha256':report['apk_sha256'],'screenshots':sum(len(s[2]) for s in steps),'gallery':str(out/'index.html')}))
