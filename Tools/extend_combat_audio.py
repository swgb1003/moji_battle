"""Append generated effects, retaining the existing catalog and preview order."""
import concurrent.futures
import json
import pathlib
import sys
import wave

from prepare_combat_audio import prepare, ROOT, OUT

assets = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    added = list(pool.map(prepare, assets))
metrics_path = ROOT / 'Reports/combat_audio_assets.json'
metrics = {a['key']: a for a in json.loads(metrics_path.read_text(encoding='utf-8'))}
metrics.update({a['key']: a for a in added})
metrics_path.write_text(json.dumps(list(metrics.values()), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
sources_path = ROOT / 'Tools/combat_audio_sources.json'
sources = json.loads(sources_path.read_text(encoding='utf-8'))
provenance = {a['key']: a for a in sources['assets']}
provenance.update({a['key']: {k: a[k] for k in ('key', 'taskId', 'prompt', 'created')} for a in assets})
sources['assets'] = list(provenance.values())
sources_path.write_text(json.dumps(sources, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
with wave.open(str(ROOT / 'Reports/combat_audio_preview.wav'), 'wb') as reel:
    reel.setnchannels(1)
    reel.setsampwidth(2)
    reel.setframerate(44100)
    for key in metrics:
        with wave.open(str(OUT / (key + '.wav')), 'rb') as clip:
            reel.writeframes(clip.readframes(clip.getnframes()))
        reel.writeframes(bytes(44100))
print(f'Added {len(added)} sounds; {len(metrics)} total.')
