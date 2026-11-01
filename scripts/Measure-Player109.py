"""Check native screenshot and hierarchy evidence; does not synthesize UI."""
from pathlib import Path
from PIL import Image
import json,re
OUT=Path(__file__).resolve().parents[1]/"artifacts/ui-1.0.9"
results=[]
for file in ["09-explore.json","14-root-return.json","16-final-root.json"]:
    ns=json.loads((OUT/file).read_text(encoding="utf-8"));bounds=lambda n:tuple(map(int,re.findall(r"\d+",n.get("bounds",""))))
    nav=next(n for n in ns if n.get("content-desc")=="主页");y=bounds(nav)[1]
    assert any(n.get("class","").endswith("ViewGroup") and bounds(n)==(0,y-168,1080,y) for n in ns),(file,y)
    results.append({"screen":file,"miniBottom":y,"navigationTop":y,"gapPixels":0})
image=Image.open(OUT/"15-functions-dark.png").convert("RGB")
background=[image.getpixel(p) for p in [(12,260),(12,800),(12,1400),(1068,1400)]]
assert len(set(background))==1,background
report={"insets":results,"darkOuterBackgroundPixels":background}
(OUT/"inset-measurements.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
checks=json.loads((OUT/"checks.json").read_text(encoding="utf-8"))
for name in ["Mini player touches bottom navigation before and after detail roundtrips","Dark function outer margins use one page background"]:
    if name not in checks:checks.append(name)
(OUT/"checks.json").write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps(report,indent=2));print("UI checks:",len(checks))
