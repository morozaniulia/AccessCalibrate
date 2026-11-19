#!/usr/bin/env python3
# Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
"""
Development tool: builds the presentation video from the frames captured by
DemoVideoDirector (UnityProject/Assets/DevTools/Video), using ffmpeg.

    python tools/make_demo_video.py --ffmpeg <path to ffmpeg>

Input:  Recordings/raw/frames/NNNNN.png (one PNG per 60 fps frame) + Recordings/raw/markers.json
Output: Recordings/AccessCalibrate-demo.mp4 (1920x1080, H.264, 60 fps)
        Recordings/AccessCalibrate-demo-square.mp4 (1080x1080 for LinkedIn)

The calibration is recorded in full and real time; the video shows a ~3 s excerpt of each
check (cut points come from the markers), the whole Suggested settings screen up to Apply,
then an end card. Captions use the package's Manrope font. In the 16:9 version each caption
sits on a dark rounded pill (drawn with Pillow), so it stays readable over the light sky.

    python tools/make_demo_video.py --ffmpeg <ffmpeg> --only wide     (or --only square)
"""
import argparse
import json
import os
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REC = os.path.join(ROOT, "Recordings")
FONTS = os.path.join(ROOT, "UnityProject", "Packages", "com.accesscalibrate", "Runtime", "UI",
                     "Resources", "AccessCalibrate", "Fonts")
FPS = 60
CLIP = 3.2   # seconds per check excerpt


def font(weight):
    return os.path.join(FONTS, f"Manrope-{weight}.ttf")


def load_markers():
    with open(os.path.join(REC, "raw", "markers.json"), encoding="utf-8") as f:
        data = json.load(f)
    frames = sorted(int(n[:-4]) for n in os.listdir(os.path.join(REC, "raw", "frames")) if n.endswith(".png"))
    marks = {}
    for m in data["markers"]:
        marks.setdefault(m["name"], []).append(m["frame"])
    return frames[0], frames[-1], marks


def segments(first, marks):
    """(start, end, caption) in seconds of the raw capture, in video order."""
    t = lambda frame: (frame - first) / FPS
    one = lambda name: marks[name][0]
    signal = marks["signal"][1] if len(marks["signal"]) > 1 else marks["signal"][0]
    segs = [
        (t(one("welcome")), t(one("welcome")) + 3.4, None),                                   # Welcome
        (t(signal) - 1.5, t(signal) - 1.5 + CLIP, "Reaction"),
        (t(one("hold-press")) - 0.5, t(one("hold-press")) - 0.5 + CLIP, "Hold"),
        (t(one("test-TapTest")) + 0.2, t(one("test-TapTest")) + 0.2 + CLIP, "Repeated presses"),
        (t(one("steadiness-measuring")) - 0.8, t(one("steadiness-measuring")) - 0.8 + CLIP, "Steadiness"),
        (t(one("test-ButtonAccessTest")) + 0.4, t(one("test-ButtonAccessTest")) + 0.4 + CLIP, "Button reach"),
        # Suggested settings up to (and including) the frame where Apply is pressed; cut right after.
        (t(one("confirm")), t(one("applied")) + 1.0 / FPS, None),
    ]
    return [(max(0.0, a), b, c) for a, b, c in segs]


def text_file(tmp, name, text):
    path = os.path.join(tmp, name + ".txt")
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)
    return path


def esc(path):
    # ffmpeg filter arguments: escape backslashes, colons and quotes
    return path.replace("\\", "/").replace(":", "\\:").replace("'", "\\'")


def drawtext(tmp, key, text, weight, size, x, y, start, end, alpha=1.0, fade=0.25):
    tf = text_file(tmp, key, text)
    a = (f"if(lt(t,{start}),0,if(lt(t,{start + fade}),(t-{start})/{fade},"
         f"if(lt(t,{end - fade}),1,if(lt(t,{end}),({end}-t)/{fade},0))))*{alpha}")
    return (f"drawtext=fontfile='{esc(font(weight))}':textfile='{esc(tf)}':fontsize={size}:fontcolor=white:"
            f"alpha='{a}':x={x}:y={y}:shadowcolor=black@0.55:shadowx=0:shadowy=2:"
            f"enable='between(t,{start},{end})'")


PILL_FILL = (0, 0, 0, 153)   # black, 60 %


def pill_image(tmp, key, text, weight, size, pad_x, pad_y):
    """Renders text on a rounded dark pill (2x supersampled) and returns (path, width, height)."""
    from PIL import Image, ImageDraw, ImageFont
    k = 2
    f = ImageFont.truetype(font(weight), size * k)
    l, t, r, b = f.getbbox(text)
    asc, desc = f.getmetrics()
    w, h = (r - l) + 2 * pad_x * k, asc + desc + 2 * pad_y * k
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, w - 1, h - 1), radius=h // 2, fill=PILL_FILL)
    d.text((pad_x * k - l, pad_y * k), text, font=f, fill=(255, 255, 255, 255))
    img = img.resize((w // k, h // k), Image.LANCZOS)
    path = os.path.join(tmp, key + ".png")
    img.save(path)
    return path, img.width, img.height


def build(ffmpeg, square):
    first, last, marks = load_markers()
    segs = segments(first, marks)
    tmp = tempfile.mkdtemp(prefix="acvideo-")

    W, H = (1080, 1080) if square else (1920, 1080)
    parts, captions, cursor = [], [], 0.0
    for i, (a, b, cap) in enumerate(segs):
        parts.append(f"[0:v]trim=start={a:.4f}:end={b:.4f},setpts=PTS-STARTPTS[s{i}]")
        captions.append((cursor, cursor + (b - a), cap))
        cursor += b - a
    main_len = cursor
    end_len = 4.0

    concat = "".join(f"[s{i}]" for i in range(len(segs))) + f"concat=n={len(segs)}:v=1:a=0[main]"

    # Layout: 16:9 keeps the frame; square = frame scaled to 1080x608 on a black canvas.
    if square:
        frame_h = int(1080 * 1080 / 1920) // 2 * 2
        top = (1080 - frame_h) // 2
        layout = f"[main]scale=1080:{frame_h}:flags=lanczos,pad=1080:1080:0:{top}:black[lay]"
        cap_y, cap_size = top + frame_h + 52, 30
        tag_y, tag_size = top - 58, 26
    else:
        layout = "[main]null[lay]"
        cap_y = cap_size = tag_y = tag_size = None      # pills, see below

    timed = [(0.0, 4.0, "Simulated input for the demo", True)]
    for i, (a, b, cap) in enumerate(captions):
        if i == 0:
            cap = "Access Calibrate: a one-minute motor calibration for Unity games"
        elif i == len(captions) - 1:
            cap = "Each suggestion shows the measurement and the rule behind it"
        if cap:
            timed.append((a, b, cap, False))

    extra_inputs = []
    if square:
        texts = []
        for n, (a, b, cap, tag) in enumerate(timed):
            if tag:
                texts.append(drawtext(tmp, "tag", cap, "SemiBold", tag_size, "(w-text_w)/2", tag_y, a, b, alpha=0.75))
            else:
                texts.append(drawtext(tmp, f"cap{n}", cap, "SemiBold", cap_size, "(w-text_w)/2", cap_y, a, b, alpha=0.95))
        main_chain = "[lay]" + ",".join(texts) + "[mainc]"
    else:
        # Pills: captions 40 px centred under the panel (panel ends at y ~982), tag 26 px top-left.
        chain, prev = [], "[lay]"
        for n, (a, b, cap, tag) in enumerate(timed):
            size, px, py = (26, 18, 9) if tag else (40, 28, 10)
            path, pw, ph = pill_image(tmp, f"pill{n}", cap, "SemiBold", size, px, py)
            idx = n + 1                                  # input 0 = frames, then one per pill
            extra_inputs += ["-loop", "1", "-framerate", str(FPS), "-t", f"{main_len:.4f}", "-i", path]
            x, y = ("44", "24") if tag else (f"(W-{pw})/2", f"{1031 - ph // 2}")
            fade = 0.25
            chain.append(f"[{idx}:v]format=rgba,fade=t=in:st={a:.4f}:d={fade}:alpha=1,"
                         f"fade=t=out:st={b - fade:.4f}:d={fade}:alpha=1[p{n}]")
            out_label = f"[o{n}]"
            chain.append(f"{prev}[p{n}]overlay=x={x}:y={y}:enable='between(t,{a:.4f},{b:.4f})'{out_label}")
            prev = out_label
        main_chain = ";".join(chain) + f";{prev}null[mainc]"

    # End card: black, three lines in Manrope.
    title_size, sub_size, url_size = (64, 34, 30) if square else (84, 40, 34)
    end_texts = [
        drawtext(tmp, "e1", "Access Calibrate", "ExtraBold", title_size, "(w-text_w)/2", f"(h/2)-{int(title_size * 1.35)}", 0.0, end_len, fade=0.4),
        drawtext(tmp, "e2", "open-source Unity plugin", "SemiBold", sub_size, "(w-text_w)/2", "(h/2)+6", 0.15, end_len, alpha=0.85, fade=0.4),
        drawtext(tmp, "e3", "github.com/morozaniulia/AccessCalibrate", "Medium", url_size, "(w-text_w)/2", f"(h/2)+{6 + int(sub_size * 1.9)}", 0.3, end_len, alpha=0.8, fade=0.4),
    ]
    end_chain = f"color=c=black:s={W}x{H}:r={FPS}:d={end_len}," + ",".join(end_texts) + "[endc]"

    graph = ";".join(parts + [concat, layout, main_chain, end_chain, "[mainc][endc]concat=n=2:v=1:a=0,format=yuv420p[out]"])
    graph_file = os.path.join(tmp, "graph.txt")
    with open(graph_file, "w", encoding="utf-8") as f:
        f.write(graph)

    out = os.path.join(REC, "AccessCalibrate-demo-square.mp4" if square else "AccessCalibrate-demo.mp4")
    cmd = [ffmpeg, "-y", "-hide_banner", "-loglevel", "error",
           "-framerate", str(FPS), "-start_number", str(first), "-i", os.path.join(REC, "raw", "frames", "%05d.png"),
           *extra_inputs,
           "-/filter_complex", graph_file, "-map", "[out]",
           "-c:v", "libx264", "-preset", "slow", "-crf", "17", "-profile:v", "high", "-pix_fmt", "yuv420p",
           "-r", str(FPS), "-movflags", "+faststart", out]
    subprocess.run(cmd, check=True)
    print(f"{out}  ({main_len + end_len:.1f} s)")
    for a, b, cap in captions:
        print(f"  {a:6.2f}–{b:6.2f}  {cap or ''}")
    return out


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--ffmpeg", default="ffmpeg")
    p.add_argument("--only", choices=["wide", "square"], help="build just one version")
    args = p.parse_args()
    if args.only != "square":
        build(args.ffmpeg, square=False)
    if args.only != "wide":
        build(args.ffmpeg, square=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
