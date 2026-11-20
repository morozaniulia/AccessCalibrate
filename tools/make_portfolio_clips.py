"""Portfolio clips: seamless MP4 loops from the frames PortfolioClipRecorder saves.

    python tools/make_portfolio_clips.py --ffmpeg <path to ffmpeg>

Reads Recordings/raw-clips/<clip>/00000.jpg ... (60 fps) and writes
Recordings/portfolio-clips/<clip>.mp4: 1920x1080, 60 fps, H.264, no audio.

A loop is made seamless by blending its last frames into its first ones: the output
has N - F frames, and frames 0..F-1 are the clip's end dissolving into its start, so
the last frame leads straight into the first. The summary clip is three holds (Dark,
Light, High Contrast) with dissolves between them, and from High Contrast back to Dark.
"""
import argparse
import glob
import os
import subprocess

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW = os.path.join(ROOT, 'Recordings', 'raw-clips')
OUT = os.path.join(ROOT, 'Recordings', 'portfolio-clips')

# clip name -> (output file, frames of the loop dissolve)
CLIPS = {
    'a-welcome': ('a-welcome-device-detection.mp4', 30),
    'b-reaction': ('b-reaction-with-intro.mp4', 30),
    'c-steadiness': ('c-steadiness-active.mp4', 30),
    'd-suggestions': ('d-suggestions-with-reason.mp4', 12),
}


def frames(name):
    files = sorted(glob.glob(os.path.join(RAW, name, '*.jpg')))
    if not files:
        raise SystemExit('no frames for ' + name)
    return files


def load(path):
    return np.asarray(Image.open(path).convert('RGB'), dtype=np.float32)


def blend(a, b, t):
    # smoothstep, so a dissolve starts and ends gently
    t = t * t * (3 - 2 * t)
    return (a * (1 - t) + b * t + 0.5).astype(np.uint8)


def encode(ffmpeg, path, frame_iter):
    cmd = [ffmpeg, '-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', '1920x1080',
           '-r', '60', '-i', '-', '-an', '-c:v', 'libx264', '-preset', 'slow', '-crf', '16',
           '-pix_fmt', 'yuv420p', '-profile:v', 'high', '-movflags', '+faststart', path]
    p = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    n = 0
    for f in frame_iter:
        p.stdin.write(np.ascontiguousarray(f, dtype=np.uint8).tobytes())
        n += 1
    p.stdin.close()
    if p.wait() != 0:
        raise SystemExit('ffmpeg failed for ' + path)
    return n


def loop(files, fade):
    n = len(files)
    for i in range(fade):   # the end dissolving into the start
        yield blend(load(files[n - fade + i]), load(files[i]), i / fade)
    for i in range(fade, n - fade):
        yield load(files[i])


def holds(groups, fade):
    """Holds A, B, C...: each one dissolves into the next, the last into the first (the loop)."""
    count = len(groups)
    for g in range(count):
        files, nxt = groups[g], groups[(g + 1) % count]
        start = fade if g == 0 else 0
        body = files[start:len(files) - fade]
        if g == 0:
            last = groups[-1]
            for i in range(fade):   # the last hold dissolving into the first
                yield blend(load(last[len(last) - fade + i]), load(files[i]), i / fade)
        for f in body:
            yield load(f)
        if g < count - 1:
            for i in range(fade):
                yield blend(load(files[len(files) - fade + i]), load(nxt[i]), i / fade)
            groups[g + 1] = nxt[fade:]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--ffmpeg', required=True)
    args = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)
    for name, (out, fade) in CLIPS.items():
        n = encode(args.ffmpeg, os.path.join(OUT, out), loop(frames(name), fade))
        print(f'{out}: {n} frames, {n / 60:.2f} s')
    groups = [frames('e-summary-1-dark'), frames('e-summary-2-light'), frames('e-summary-3-high-contrast')]
    out = 'e-summary-themes.mp4'
    n = encode(args.ffmpeg, os.path.join(OUT, out), holds(groups, 18))
    print(f'{out}: {n} frames, {n / 60:.2f} s')


if __name__ == '__main__':
    main()
