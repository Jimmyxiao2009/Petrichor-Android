"""Bundle native verification screenshots and video as a local review page."""
import html,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/cache-review'
cards=[('release-wave-playing','播放中的波浪进度条'),('release-wave-paused','暂停后回到平直轨道'),('release-local-functions','上滑后的独立功能区'),('release-player','在线歌曲播放页'),('release-offline-discover','断网重启后的发现页'),('release-charts','预加载的排行榜'),('release-offline-playlist','断网重启后的歌单和歌曲'),('release-offline-comments','断网重新打开的评论')]
for file,_ in cards:assert (OUT/(file+'.png')).is_file(),file
package=json.loads((OUT/'release-verification.json').read_text(encoding='utf-8'))
figures=''.join(f'<figure><a href="{file}.png"><img src="{file}.png" loading="lazy" alt="{html.escape(title)}"></a><figcaption>{html.escape(title)}</figcaption></figure>' for file,title in cards)
document=f'''<!doctype html><html lang="zh-CN"><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Petrichor 1.0.5 原生检查</title><style>
body{{margin:0;background:#f6f6fa;color:#24252c;font:16px/1.7 system-ui,sans-serif}}main{{max-width:1300px;margin:auto;padding:32px}}h1{{font-size:28px;margin-bottom:8px}}p{{max-width:900px}}.grid{{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:24px}}figure{{margin:0;background:white;border-radius:18px;padding:14px}}img{{width:100%;border-radius:10px;display:block}}figcaption{{padding-top:10px}}video{{width:min(100%,340px);max-height:720px;border-radius:20px;background:#222}}.video{{display:flex;gap:28px;flex-wrap:wrap;margin:28px 0}}a{{color:#475d9b}}code{{word-break:break-all}}small{{color:#62646e}}
</style></head><body><main><h1>Petrichor Android 1.0.5</h1><p>本页是 Android 模拟器中实际运行正式包的截图与录屏。119 项 Core 检查、9 项正式包流程、3 轮快速返回及暂停状态拖动进度条检查通过。</p><div class="video"><video controls preload="metadata" src="release-motion.mp4" poster="release-wave-playing.png"></video><div><h2>播放与上下翻页</h2><p>本地 FLAC 播放、波浪流动、上滑功能、下滑返回、暂停后轨道恢复平直。</p><p>正式包：1.0.5 / 6，{package['bytes']:,} 字节，已核对安装文件哈希一致。</p><p><a href="../../docs/UI-1.0.5.md">改动说明</a> · <a href="release-checks.json">正式包检查</a> · <a href="release-verification.json">安装验证</a> · <a href="release-seek-check.json">拖动检查</a></p><small>录屏同时采样 295 帧，其中 9 帧错过系统截止时间，P95 42 ms。此数据包含模拟器与录屏开销，场景不同，不与旧版录屏直接比较。冷启动系统记录约 1.9–3.4 秒；仍有运行时启动开销。</small></div></div><div class="grid">{figures}</div><p><small>没有使用真实账号发送验证码、评论或修改歌单。账号隔离与缓存失效由可控服务端测试覆盖。</small></p></main></body></html>'''
(OUT/'index.html').write_text(document,encoding='utf-8')
print(OUT/'index.html')
