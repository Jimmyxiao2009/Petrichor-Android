"""Inspect actual native slider pixels; do not synthesize application screenshots."""
from pathlib import Path
import json,re
from PIL import Image,ImageChops
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/"artifacts/slider-review"
ns=json.loads((OUT/"05-paused-half-release.json").read_text(encoding="utf-8"));node=next(n for n in ns if n.get("class")=="android.widget.SeekBar")
l,t,r,b=map(int,re.findall(r"\d+",node["bounds"]));d=(b-t)/48;y=(t+b)//2
paused=Image.open(OUT/"05-paused-half-release.png").convert("RGB");color=paused.getpixel(((l+r)//2,y))
def runs(image,ink):
    result=[];start=None
    for x in range(l,r):
        match=image.getpixel((x,y))==ink
        if match and start is None:start=x
        if start is not None and (not match or x==r-1):result.append((start,x-1 if not match else x));start=None
    return result
active=runs(paused,color);thumb=min(active,key=lambda ab:abs((ab[0]+ab[1])/2-(l+r)/2))
left=next(ab for ab in active if ab[1]<thumb[0] and ab[1]-ab[0]>100)
inactive=runs(paused,paused.getpixel((round(l+(r-l)*.8),y)));right=next(ab for ab in inactive if ab[0]>thumb[1] and ab[1]-ab[0]>100)
gaps=((thumb[0]-left[1]-1)/d,(right[0]-thumb[1]-1)/d)
playing=Image.open(OUT/"06-playing-wave-release.png").convert("RGB");next_frame=Image.open(OUT/"07-playing-wave-next-release.png").convert("RGB")
sample=(round(l+32*d),round(y-20*d),round(l+(r-l)*.35),round(y+20*d))
def span(image):
    ys=[yy for yy in range(sample[1],sample[3]) for xx in range(sample[0],sample[2]) if max(abs(a-b) for a,b in zip(image.getpixel((xx,yy)),color))<8]
    return max(ys)-min(ys)+1
result={"density":d,"visibleGapDp":gaps,"pausedRailSpanPixels":span(paused),"playingWaveSpanPixels":span(playing),"wavePhaseChanges":ImageChops.difference(playing.crop(sample),next_frame.crop(sample)).getbbox() is not None}
assert all(5.5<=gap<=7 for gap in gaps) and result["playingWaveSpanPixels"]>result["pausedRailSpanPixels"]*2 and result["wavePhaseChanges"]
for name in ["02-paused-midpoint-release","03-drag-preview-release","05-paused-half-release","06-playing-wave-release"]:
    Image.open(OUT/(name+".png")).crop((l-12,y-130,r+12,y+130)).save(OUT/(name+"-detail.png"))
(OUT/"visual-measurements.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
print(json.dumps(result))
