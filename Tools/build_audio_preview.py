"""Build an offline listening page from the imported combat effects."""
import base64
import html
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
labels = {
    'gong_start': '戦闘開始のゴング', 'gong_end': '戦闘終了のゴング',
    'jump': 'ジャンプ', 'land_light': '軽い着地', 'land_heavy': '重い着地',
    'swing_light_01': '軽い武器の振り 1', 'swing_light_02': '軽い武器の振り 2',
    'swing_heavy_01': '大型武器の振り 1', 'swing_heavy_02': '大型武器の振り 2',
    'hit_01': '通常打撃・ドカッ', 'hit_02': '通常打撃・バキッ', 'hit_03': '通常打撃・ゴツッ',
    'hit_heavy_01': '重い打撃 1', 'hit_heavy_02': '重い打撃 2', 'critical': 'クリティカル',
    'guard': 'ガード', 'clash': '武器同士の衝突', 'guard_break': 'ガードブレイク',
    'wall': '壁への衝突', 'fall': '転倒・床への衝突', 'launch': '打ち上げ', 'slam': '叩きつけ',
    'evade': 'バックステップ', 'throw': '武器を投げる', 'pickup': '武器を拾う',
    'charge': '攻撃前の溜め', 'step_01': '足音 1', 'step_02': '足音 2',
    'recover': '起き上がり', 'ko': 'KOの決着音',
    'dash_step': '踏み込み斬り・踏み出し', 'dash_swing': '踏み込み斬り・振り',
    'dash_hit': '踏み込み斬り・命中', 'parry': '受け流し成功',
    'counter_swing': '反撃・斬り返し', 'counter_hit': '反撃・命中',
    'whirl_swing': '全身ぶん回し・回転', 'whirl_hit': '全身ぶん回し・命中',
}
metrics = json.loads((root / 'Reports/combat_audio_assets.json').read_text(encoding='utf-8'))
cards = []
for index, entry in enumerate(metrics, 1):
    key = entry['key']
    wav = (root / 'Assets/Resources/Audio/Combat' / (key + '.wav')).read_bytes()
    encoded = base64.b64encode(wav).decode()
    cards.append(f'<section><h2>{index:02d} {html.escape(labels[key])}</h2>'
                 f'<p>{entry["seconds"]:.2f} 秒 · {key}</p>'
                 f'<audio controls preload="none" aria-label="{html.escape(labels[key])}" '
                 f'src="data:audio/wav;base64,{encoded}"></audio></section>')
page = '''<!doctype html><html lang="ja"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>文字武器バトル — 効果音試聴</title><style>
body{font:16px/1.6 system-ui,sans-serif;background:#f5f1e8;color:#272420;margin:0;padding:32px}
main{max-width:1100px;margin:auto}h1{font-size:26px}h2{font-size:16px;margin:0}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:16px}
section{background:#fffdf8;border:1px solid #d8d1c3;padding:18px;border-radius:10px}
p{color:#6a645b;font-size:13px}audio{width:100%}
button{font:inherit;background:#272420;color:white;padding:10px 18px;border:0;border-radius:6px;margin:0 8px 24px 0;cursor:pointer}
</style><main><h1>文字武器バトル · 効果音試聴</h1>
<p>生成した素材音です。ゲーム内では場面に応じて音量・高さ・左右の位置を調整しています。</p>
<button id="all">順番に試聴</button><button id="stop">停止</button><div class="grid">'''
page += '\n'.join(cards)
page += '''</div></main><script>
const clips=[...document.querySelectorAll('audio')];let sequence=false,index=0;
function stop(){sequence=false;clips.forEach(a=>{a.pause();a.currentTime=0})}
document.querySelector('#stop').onclick=stop;
document.querySelector('#all').onclick=()=>{stop();sequence=true;index=0;clips[0].play()};
clips.forEach((a,i)=>{a.onplay=()=>clips.forEach(b=>{if(b!==a)b.pause()});
a.onended=()=>{if(sequence&&i===index&&++index<clips.length)clips[index].play();else sequence=false}});
</script></html>'''
(root / 'Reports/combat_audio_preview.html').write_text(page, encoding='utf-8')
print(f'Built offline preview with {len(metrics)} embedded WAV files.')
