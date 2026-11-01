# Campaign videos

All app footage is REAL: the app records its own window (demo mode) while this computer is genuinely offline.

1. `./record_hero.sh` → films raw footage into `raw/<timestamp>/`. **Takes this computer offline for under a minute**
   (nmcli), loads a website in Chrome to capture the real "no internet" error, and always turns networking back on
   (plus a 3-minute safety net).
2. `python3 make_hero.py raw/<timestamp> videos/01-unplugged-proof.mp4` → builds the 1080×1920 vertical video
   (title cards + zoomed crops of the real footage), plus a cover image.

Fonts: Inter (SIL Open Font License, `fonts/Inter-LICENSE-OFL.txt`). Videos are silent; add a sound inside TikTok.
