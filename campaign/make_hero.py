#!/usr/bin/env python3
"""
Builds the hero TikTok video (1080x1920, 30 fps) from REAL footage recorded by record_hero.sh:
title cards + zoomed crops of the actual app + the actual offline browser screenshot.
Nothing is staged or simulated; every app frame is a frame the app really rendered.

Usage: python3 make_hero.py <raw-footage-dir> <output.mp4>
"""
import os, sys, subprocess
from PIL import Image, ImageDraw, ImageFont, ImageFilter

RAW, OUT = sys.argv[1], sys.argv[2]
HERE = os.path.dirname(os.path.abspath(__file__))
W, H, FPS = 1080, 1920, 30
BG = (14, 16, 20)
PANEL = (22, 26, 33)
TEXT = (236, 238, 241)
MUTED = (156, 165, 178)
ACCENT = (94, 234, 212)
RED = (248, 113, 113)

def font(weight, size):
    return ImageFont.truetype(os.path.join(HERE, "fonts", f"Inter-{weight}.ttf"), size)

# ---------- footage ----------
frames_dir = os.path.join(RAW, "frames")
frame_files = sorted(f for f in os.listdir(frames_dir) if f.endswith(".png"))
frame_times = [int(f[2:10]) for f in frame_files]
events = {}
for line in open(os.path.join(RAW, "events.txt")):
    ms, name = line.split()
    events[name] = int(ms)
_cache = {}
def footage(ms):
    """The real recorded frame closest to `ms` (skips any frame cut off when recording stopped)."""
    i = min(range(len(frame_times)), key=lambda k: abs(frame_times[k] - ms))
    f = frame_files[i]
    if f not in _cache:
        if len(_cache) > 40: _cache.clear()
        try: _cache[f] = Image.open(os.path.join(frames_dir, f)).convert("RGB")
        except Exception: _cache[f] = footage(frame_times[max(0, i - 1)])
    return _cache[f]
browser = Image.open(os.path.join(RAW, "browser-offline.png")).convert("RGB")

# ---------- drawing helpers ----------
def wrap(draw, text, fnt, maxw):
    lines, line = [], ""
    for word in text.split():
        test = (line + " " + word).strip()
        if draw.textlength(test, font=fnt) <= maxw: line = test
        else: lines.append(line); line = word
    if line: lines.append(line)
    return lines

def text_block(img, y, text, fnt, color=TEXT, maxw=900, spacing=1.18, align="center"):
    d = ImageDraw.Draw(img)
    lh = int(fnt.size * spacing)
    for ln in wrap(d, text, fnt, maxw):
        w = d.textlength(ln, font=fnt)
        x = (W - w) / 2 if align == "center" else 90
        d.text((x, y), ln, font=fnt, fill=color)
        y += lh
    return y

def lock_line(img, y, text, size=40):
    """The app's green 'privacy line', recreated for video."""
    d = ImageDraw.Draw(img)
    f = font("Bold", size)
    lines = wrap(d, text, f, 820)
    h = len(lines) * int(size * 1.25) + 50
    d.rounded_rectangle((80, y, W - 80, y + h), 24, fill=(20, 52, 48))
    yy = y + 25
    for ln in lines:
        d.text((130, yy), ln, font=f, fill=ACCENT); yy += int(size * 1.25)
    # tiny padlock
    d.rounded_rectangle((97, y + 32, 117, y + 52), 4, fill=ACCENT)
    d.arc((100, y + 18, 114, y + 40), 180, 360, fill=ACCENT, width=4)
    return y + h

def place(img, src, box, width, y, border=ACCENT):
    """Crop `box` from `src`, scale to `width`, center horizontally at `y`, with rounded frame."""
    c = src.crop(box)
    h = int(c.height * width / c.width)
    c = c.resize((width, h), Image.LANCZOS)
    x = (W - width) // 2
    mask = Image.new("L", c.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, c.width, c.height), 22, fill=255)
    shadow = Image.new("RGBA", (c.width + 60, c.height + 60), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((30, 30, c.width + 30, c.height + 30), 26, fill=(0, 0, 0, 160))
    img.paste(shadow.filter(ImageFilter.GaussianBlur(14)), (x - 30, y - 24), shadow.filter(ImageFilter.GaussianBlur(14)))
    img.paste(c, (x, y), mask)
    if border: ImageDraw.Draw(img).rounded_rectangle((x - 3, y - 3, x + width + 3, y + h + 3), 24, outline=border, width=3)
    return y + h

def brand(img):
    d = ImageDraw.Draw(img)
    f = font("Black", 30)
    t = "U N P L U G G E D"
    d.text(((W - d.textlength(t, font=f)) / 2, 150), t, font=f, fill=ACCENT)

def ease(t): return t * t * (3 - 2 * t)

# ---------- scenes ----------
# each scene: (duration_seconds, render(img, t_seconds_into_scene, progress_0_1))
def card(lines, accent_last=False, sub=None):
    def render(img, t, p):
        brand(img)
        a = min(1.0, t / 0.7)
        y = 640
        for i, ln in enumerate(lines):
            col = ACCENT if (accent_last and i == len(lines) - 1) else TEXT
            col = tuple(int(BG[k] + (col[k] - BG[k]) * a) for k in range(3))
            y = text_block(img, y, ln, font("Black", 92), col, maxw=920) + 18
        if sub:
            col = tuple(int(BG[k] + (MUTED[k] - BG[k]) * a) for k in range(3))
            text_block(img, y + 30, sub, font("SemiBold", 46), col, maxw=880)
    return render

ev = events
online_t = max(1200, ev["offline"] - 3200)

def scene_online(img, t, p):
    brand(img)
    text_block(img, 330, "Right now, my laptop is online.", font("Black", 70), maxw=900)
    fr = footage(min(online_t + t * 1000, ev["offline"] - 400))   # never show the flip early
    place(img, fr, (1235, 10, 1845, 92), 980, 640)                     # the network badge, big
    place(img, fr, (0, 0, 1860, 1230), 980, 900, border=None)           # the whole app
    text_block(img, 1600, "This is Unplugged, a free app with an AI built in.", font("SemiBold", 42), MUTED, maxw=880)

def scene_flip(img, t, p):
    brand(img)
    text_block(img, 360, "Internet: off.", font("Black", 96), RED if t > 1.0 else TEXT, maxw=900)
    fr = footage(ev["offline"] - 900 + t * 1000)
    place(img, fr, (1235, 10, 1845, 92), 980, 700, border=RED if t > 1.0 else ACCENT)
    text_block(img, 1000, "I unplugged it for real. No tricks.", font("SemiBold", 48), MUTED, maxw=880)

def scene_browser(img, t, p):
    brand(img)
    text_block(img, 300, "Proof: my browser can't load anything.", font("Black", 66), maxw=900)
    z = 1.0 + 0.06 * ease(p)
    bw = int(860 * z)
    b = browser.crop((15, 15, 985, 520))     # tab bar, address bar, dinosaur + ERR_INTERNET_DISCONNECTED
    bh = int(b.height * bw / b.width)
    b = b.resize((bw, bh), Image.LANCZOS)
    img.paste(b, ((W - bw) // 2, 640))
    text_block(img, 640 + bh + 60, "ERR_INTERNET_DISCONNECTED", font("Bold", 44), RED, maxw=900)

answer_start = ev["typing"] - 300
# The AI's typing and writing play at TRUE speed (never slowed down); the finished answer then holds on screen.
writing_done = (ev["answer-end"] - answer_start) / 1000
answer_len = writing_done + 10.0

def scene_answer(img, t, p):
    brand(img)
    if t < writing_done + 1.0:
        head, col = "No internet. Still writing.", TEXT
    elif t < answer_len - 4.5:
        head, col = "Written by my laptop's own processor.", TEXT
    else:
        head, col = "Nothing I typed left this computer.", ACCENT
    text_block(img, 250, head, font("Black", 64), col, maxw=940)
    fr = footage(answer_start + t * 1000)
    place(img, fr, (1235, 10, 1845, 92), 640, 420, border=RED)          # OFFLINE badge
    y = place(img, fr, (70, 290, 1230, 925), 1000, 560)                 # the conversation
    place(img, fr, (60, 1052, 1230, 1118), 1000, y + 24, border=None)   # the typing box
    ImageDraw.Draw(img).text((90, y + 120), "MY PROCESSOR, DOING THE MATH:", font=font("Bold", 30), fill=MUTED)
    place(img, fr, (1305, 165, 1835, 335), 820, y + 168)                # live CPU graph

scenes = [
    (5.2, card(["This AI lives", "on my laptop."], sub="No account. No subscription. No cloud.")),
    (5.2, scene_online),
    (3.0, card(["Now I turn off", "the internet."], accent_last=True)),
    (5.2, scene_flip),
    (4.8, scene_browser),
    (2.8, card(["Still working?"], accent_last=True)),
    (answer_len, scene_answer),
    (5.2, card(["No internet.", "No company.", "No subscription."], sub="Just my computer.")),
    (7.2, card(["They'll say the only way to have AI is to pay them for it."], sub="That is simply not true.")),
    (6.8, None),   # end card
]

def end_card(img, t, p):
    brand(img)
    a = min(1.0, t / 0.8)
    def fade(c): return tuple(int(BG[k] + (c[k] - BG[k]) * a) for k in range(3))
    text_block(img, 560, "Try it yourself. Free.", font("Black", 84), fade(TEXT))
    text_block(img, 760, "keepailocal.org", font("Black", 96), fade(ACCENT))
    text_block(img, 930, "Free app: Unplugged · Windows · Mac · Linux", font("SemiBold", 40), fade(MUTED), maxw=900)
    text_block(img, 1180, "Keep AI local.", font("Black", 74), fade(TEXT))
    text_block(img, 1290, "#KeepAILocal", font("Bold", 50), fade(ACCENT))
scenes[-1] = (6.8, end_card)

# ---------- render ----------
os.makedirs(os.path.dirname(os.path.abspath(OUT)), exist_ok=True)
ff = subprocess.Popen([
    "ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
    "-c:v", "libx264", "-preset", "slow", "-crf", "17", "-pix_fmt", "yuv420p", "-movflags", "+faststart", OUT,
], stdin=subprocess.PIPE)

XF = 12  # crossfade frames between scenes
prev_last = None
total = 0
cover = None
for si, (dur, render) in enumerate(scenes):
    n = int(round(dur * FPS))
    for i in range(n):
        img = Image.new("RGB", (W, H), BG)
        render(img, i / FPS, i / max(1, n - 1))
        if prev_last is not None and i < XF:
            img = Image.blend(prev_last, img, (i + 1) / (XF + 1))
        ff.stdin.write(img.tobytes())
        total += 1
        if si == 6 and cover is None and i / FPS > answer_len - 4.0: cover = img.copy()
    prev_last = img
ff.stdin.close()
ff.wait()
if cover: cover.save(os.path.splitext(OUT)[0] + "-cover.png")
print(f"wrote {OUT}: {total} frames, {total / FPS:.1f} s")
