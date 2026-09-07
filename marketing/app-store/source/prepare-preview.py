"""Conform real capture timestamps to 30 FPS without speeding up gameplay."""
from pathlib import Path
import bisect,csv,json,shutil,subprocess,tempfile,html
base=Path(__file__).resolve().parents[1]
repo=base.parents[1]
assets=base/'video/preview/assets';assets.mkdir(exist_ok=True)
rows=list(csv.DictReader((base/'captures/timeline.csv').open()))
times=[float(r['time']) for r in rows]
with tempfile.TemporaryDirectory(prefix='ring-rush-conform-') as td:
 for i in range(450):
  t=i/30;j=bisect.bisect_left(times,t)
  j=min(range(max(0,j-1),min(len(times),j+1)),key=lambda k:abs(times[k]-t))
  (Path(td)/f'{i:05}.png').symlink_to(base/'captures/frames'/f"{int(rows[j]['frame']):05}.png")
 subprocess.run(['ffmpeg','-y','-v','error','-framerate','30','-i',td+'/%05d.png','-frames:v','450','-c:v','libx264','-preset','fast','-crf','16','-pix_fmt','yuv420p','-g','30','-keyint_min','30','-movflags','+faststart','-an',str(assets/'gameplay.mp4')],check=True)
for name in ['playing.wav','match.wav']:shutil.copy2(repo/'Assets/Audio'/name,assets/name)
shutil.copy2(repo/'Assets/Resources/Brand/Rounded-Bold.ttf',assets/'Rounded-Bold.ttf')
shutil.copy2(base/'video/preview/node_modules/gsap/dist/gsap.min.js',assets/'gsap.min.js')
match_times=[t for t in json.loads((base/'captures/match-times.json').read_text()) if t<15]
effects='\n'.join(f'<audio id="match-{i}" class="clip" src="assets/match.wav" data-start="{t:.6f}" data-duration="0.046667" data-track-index="{i+2}" data-volume="0.8"></audio>' for i,t in enumerate(match_times))
automation=html.escape(json.dumps({'version':1,'lanes':[{'target':'volume','points':[{'t':0,'v':0},{'t':.15,'v':.35},{'t':14.7,'v':.35},{'t':15,'v':0}]}]}),quote=True)
(base/'video/preview/index.html').write_text('''<!doctype html>
<html lang="en"><head><meta charset="UTF-8"><meta name="viewport" content="width=886,height=1920">
<script src="assets/gsap.min.js"></script>
<style>
@font-face{font-family:Rounded;src:url('assets/Rounded-Bold.ttf')}
*{box-sizing:border-box}html,body{margin:0;width:886px;height:1920px;overflow:hidden;background:#F0F6FC}
#root{position:relative;width:886px;height:1920px;overflow:hidden}
#gameplay{position:absolute;inset:0;width:886px;height:1920px;object-fit:contain}
#closing{position:absolute;left:0;right:0;bottom:0;height:185px;opacity:0;display:flex;align-items:center;justify-content:center;background:#F0F6FC;color:#222347;border-radius:0;font:700 48px Rounded,sans-serif;letter-spacing:-1px;}
</style></head><body>
<div id="root" data-composition-id="ring-rush" data-width="886" data-height="1920" data-duration="15">
<video id="gameplay" class="clip" src="assets/gameplay.mp4" data-start="0" data-duration="15" data-track-index="0" muted playsinline></video>
<audio id="music" class="clip" src="assets/playing.wav" data-start="0" data-duration="15" data-track-index="1" data-volume="0.35" data-automation="'''+automation+'''"></audio>
'''+effects+'''
<div id="closing" class="clip" data-start="13" data-duration="2" data-track-index="20" data-layout-allow-caption-zone>Find your flow.</div>
</div><script>
window.__timelines=window.__timelines||{};
const tl=gsap.timeline({paused:true});
tl.set('#closing',{opacity:1},13);
window.__timelines['ring-rush']=tl;
</script></body></html>''')
print(f'Conformed 450 frames from {len(rows)} timestamped real captures; {len(match_times)} match effects.')
