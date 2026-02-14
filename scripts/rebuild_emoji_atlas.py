#!/usr/bin/env python3
"""
Rebuild emoji atlas from Google Noto Color Emoji 128x128 PNGs.
Noto has built-in padding (~7px in 128px), so emojis look complete when scaled.
High-quality LANCZOS downscale + 2px atlas padding to prevent bleeding.
"""

import json
import os
from PIL import Image, ImageFilter

NOTO_DIR = "/tmp/noto_repo/png/128"
TWEMOJI_DIR = "/tmp/twemoji_repo/assets/72x72"  # fallback
ATLAS_OUT = "../src/Terra_Namp/Assets/UI/Emoji/emoji_atlas.png"
MAP_OUT = "../src/Terra_Namp/Assets/UI/Emoji/emoji_map.json"

EMOJI_SIZE = 72       # 2x resolution for smooth GPU downsampling (72 → 24px = 3x)
PAD = 2               # atlas padding on each side
CELL_SIZE = EMOJI_SIZE + PAD * 2  # 76
COLS = 38


def find_noto_png(cp_hex):
    """Find Noto PNG for a codepoint hex string."""
    # Noto uses emoji_u{hex}.png format
    path = os.path.join(NOTO_DIR, f"emoji_u{cp_hex}.png")
    if os.path.exists(path):
        return path
    # Try with leading zeros removed
    stripped = cp_hex.lstrip("0")
    path = os.path.join(NOTO_DIR, f"emoji_u{stripped}.png")
    if os.path.exists(path):
        return path
    return None


def find_twemoji_png(cp_hex):
    """Fallback to Twemoji."""
    path = os.path.join(TWEMOJI_DIR, f"{cp_hex}.png")
    return path if os.path.exists(path) else None


def main():
    # Load existing map to know which codepoints to include
    with open(MAP_OUT, "r") as f:
        old_data = json.load(f)

    codepoints = sorted(old_data["Glyphs"].keys())
    print(f"Building atlas for {len(codepoints)} codepoints")
    print(f"Primary: Noto Color Emoji 128px, Fallback: Twemoji 72px")

    rows = (len(codepoints) + COLS - 1) // COLS
    width = COLS * CELL_SIZE
    height = rows * CELL_SIZE
    print(f"Atlas: {width}x{height}, cell={CELL_SIZE}px")

    atlas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    glyphs = {}
    noto_count = 0
    twemoji_count = 0
    missing = 0

    for i, cp_hex in enumerate(codepoints):
        # Try Noto first, then Twemoji as fallback
        png_path = find_noto_png(cp_hex)
        source = "noto"
        if not png_path:
            png_path = find_twemoji_png(cp_hex)
            source = "twemoji"
        if not png_path:
            missing += 1
            continue

        img = Image.open(png_path).convert("RGBA")

        # High-quality downscale
        if img.size != (EMOJI_SIZE, EMOJI_SIZE):
            img = img.resize((EMOJI_SIZE, EMOJI_SIZE), Image.LANCZOS)

        # Clip semi-transparent edge pixels that cause colored halo in XNA
        pixels = img.load()
        ALPHA_THRESHOLD = 32
        for py in range(EMOJI_SIZE):
            for px in range(EMOJI_SIZE):
                r, g, b, a = pixels[px, py]
                if 0 < a < ALPHA_THRESHOLD:
                    pixels[px, py] = (0, 0, 0, 0)

        col = i % COLS
        row = i // COLS
        x = col * CELL_SIZE
        y = row * CELL_SIZE

        atlas.paste(img, (x + PAD, y + PAD))
        glyphs[cp_hex] = {"X": x, "Y": y}

        if source == "noto":
            noto_count += 1
        else:
            twemoji_count += 1

    print(f"Packed: {noto_count} Noto + {twemoji_count} Twemoji fallback, {missing} missing")

    atlas.save(ATLAS_OUT, optimize=True)
    size_kb = os.path.getsize(ATLAS_OUT) / 1024
    print(f"Saved: {ATLAS_OUT} ({size_kb:.0f} KB)")

    map_data = {"GlyphSize": CELL_SIZE, "Glyphs": glyphs}
    with open(MAP_OUT, "w") as f:
        json.dump(map_data, f, separators=(",", ":"))
    print(f"Saved: {MAP_OUT}")


if __name__ == "__main__":
    main()
