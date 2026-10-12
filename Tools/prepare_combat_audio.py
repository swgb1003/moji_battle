"""Download generated effects and prepare Unity PCM assets (signed URLs stay in temp)."""
import concurrent.futures
import json
import pathlib
import subprocess
import sys
import wave

import imageio_ffmpeg
import numpy as np
import requests

ROOT = pathlib.Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Resources/Audio/Combat'
OUT.mkdir(parents=True, exist_ok=True)
FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()


def prepare(asset):
    response = requests.get(asset['url'], timeout=90)
    response.raise_for_status()
    decoded = subprocess.run([FFMPEG, '-hide_banner', '-loglevel', 'error', '-i',
                              'pipe:0', '-f', 'f32le', '-ac', '1', '-ar', '44100', 'pipe:1'],
                             input=response.content, capture_output=True, check=True)
    samples = np.frombuffer(decoded.stdout, dtype='<f4').copy()
    samples -= samples.mean()
    peak = float(np.max(np.abs(samples)))
    if peak < .001:
        raise ValueError(f"Silent effect: {asset['key']}")
    # Remove leading silence, retaining 8ms before the first audible transient.
    audible = np.flatnonzero(np.abs(samples) > peak * .025)
    cut = max(0, int(audible[0]) - 353)
    samples = samples[cut:]
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
    return dict(key=asset['key'], seconds=round(len(samples) / 44100, 4),
                removedLeadSeconds=round(cut / 44100, 4), peak=round(float(np.abs(samples).max()), 4),
                rms=round(float(np.sqrt(np.mean(samples ** 2))), 4))


if __name__ == '__main__':
    assets = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        metrics = list(pool.map(prepare, assets))
    (ROOT / 'Reports/combat_audio_assets.json').write_text(
        json.dumps(metrics, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    # A listening reel with a short gap between clips, in manifest order.
    with wave.open(str(ROOT / 'Reports/combat_audio_preview.wav'), 'wb') as reel:
        reel.setnchannels(1)
        reel.setsampwidth(2)
        reel.setframerate(44100)
        for asset in assets:
            with wave.open(str(OUT / (asset['key'] + '.wav')), 'rb') as clip:
                reel.writeframes(clip.readframes(clip.getnframes()))
            reel.writeframes(bytes(44100))
    print(f'Prepared {len(metrics)} effects; peak .85; mono PCM16/44100Hz.')
