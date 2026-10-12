"""Install generated button cues. Signed download URLs are kept in a temporary manifest."""
import json
import pathlib
import subprocess
import sys
import wave

import imageio_ffmpeg
import numpy as np
import requests

ROOT = pathlib.Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Audio/UI'
OUT.mkdir(parents=True, exist_ok=True)
assets = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
metrics = []
for asset in assets:
    response = requests.get(asset['url'], timeout=90)
    response.raise_for_status()
    decoded = subprocess.run([
        imageio_ffmpeg.get_ffmpeg_exe(), '-hide_banner', '-loglevel', 'error',
        '-i', 'pipe:0', '-f', 'f32le', '-ac', '1', '-ar', '44100', 'pipe:1'
    ], input=response.content, capture_output=True, check=True)
    samples = np.frombuffer(decoded.stdout, dtype='<f4').copy()
    samples -= samples.mean()
    peak = float(np.max(np.abs(samples)))
    if peak < .001:
        raise ValueError(f"Silent effect: {asset['key']}")
    audible = np.flatnonzero(np.abs(samples) > peak * .025)
    cut = max(0, int(audible[0]) - 353)
    samples = samples[cut:]
    if asset['key'] == 'battle_start':
        # Place two explicit attacks: the generated drum texture alone can sound
        # like one strike with a resonant tail, even when two were requested.
        gap = int(44100 * .24)
        double = np.zeros(len(samples) + gap, dtype=np.float32)
        double[:len(samples)] += samples * .85
        double[gap:] += samples
        samples = double
    samples *= .85 / float(np.max(np.abs(samples)))
    fade = min(176, len(samples) // 2)
    samples[:fade] *= np.linspace(0, 1, fade)
    samples[-fade:] *= np.linspace(1, 0, fade)
    pcm = (samples * 32767).astype('<i2')
    with wave.open(str(OUT / (asset['key'] + '.wav')), 'wb') as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(44100)
        wav.writeframes(pcm.tobytes())
    metrics.append(dict(key=asset['key'], seconds=round(len(samples) / 44100, 4),
                        removedLeadSeconds=round(cut / 44100, 4), peak=.85,
                        rms=round(float(np.sqrt(np.mean(samples ** 2))), 4)))
(ROOT / 'Reports/ui_audio_assets.json').write_text(
    json.dumps(metrics, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
(ROOT / 'Tools/ui_audio_sources.json').write_text(json.dumps(dict(
    generator='Runway Sound Effects', created='2026-10-07',
    assets=[{k: a[k] for k in ('key', 'taskId', 'prompt', 'created')} for a in assets]
), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'Prepared {len(metrics)} button cues; peak .85; mono PCM16/44100Hz.')
