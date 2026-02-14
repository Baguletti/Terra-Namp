#!/usr/bin/env python3
"""
Repack emoji atlas with padding to fix:
1. Atlas bleeding (pixel border artifacts when scaling)
2. Edge cropping (emoji content going to cell edges)

Takes existing 36px-cell atlas, repacks into 40px cells with 2px transparent padding.
"""

import json
from PIL import Image

ATLAS_PATH = "../src/Terra_Namp/Assets/UI/Emoji/emoji_atlas.png"
MAP_PATH = "../src/Terra_Namp/Assets/UI/Emoji/emoji_map.json"

OLD_GLYPH = 36
PAD = 2
NEW_GLYPH = OLD_GLYPH + PAD * 2  # 40

def main():
    # Load existing atlas and map
    atlas = Image.open(ATLAS_PATH).convert("RGBA")
    with open(MAP_PATH, "r") as f:
        data = json.load(f)

    old_size = data["GlyphSize"]
    assert old_size == OLD_GLYPH, f"Expected GlyphSize={OLD_GLYPH}, got {old_size}"
    glyphs = data["Glyphs"]
    count = len(glyphs)
    print(f"Repacking {count} emojis: {OLD_GLYPH}px -> {NEW_GLYPH}px cells ({PAD}px padding)")

    # Determine new atlas layout (same column count as before, based on atlas width)
    old_cols = atlas.width // OLD_GLYPH
    new_cols = old_cols  # keep same number of columns
    new_rows = (count + new_cols - 1) // new_cols

    new_width = new_cols * NEW_GLYPH
    new_height = new_rows * NEW_GLYPH
    print(f"New atlas: {new_width}x{new_height} ({new_cols} cols x {new_rows} rows)")

    # Create new atlas with transparent background
    new_atlas = Image.new("RGBA", (new_width, new_height), (0, 0, 0, 0))

    # Repack each glyph
    new_glyphs = {}
    sorted_keys = sorted(glyphs.keys(), key=lambda k: (glyphs[k]["Y"], glyphs[k]["X"]))

    for i, key in enumerate(sorted_keys):
        old_x = glyphs[key]["X"]
        old_y = glyphs[key]["Y"]

        # Extract old sprite
        sprite = atlas.crop((old_x, old_y, old_x + OLD_GLYPH, old_y + OLD_GLYPH))

        # Place in new cell with padding
        new_col = i % new_cols
        new_row = i // new_cols
        new_x = new_col * NEW_GLYPH
        new_y = new_row * NEW_GLYPH

        new_atlas.paste(sprite, (new_x + PAD, new_y + PAD))
        new_glyphs[key] = {"X": new_x, "Y": new_y}

    # Save new atlas
    new_atlas.save(ATLAS_PATH, optimize=True)
    print(f"Saved atlas: {ATLAS_PATH}")

    # Save new map
    new_data = {"GlyphSize": NEW_GLYPH, "Glyphs": new_glyphs}
    with open(MAP_PATH, "w") as f:
        json.dump(new_data, f, separators=(",", ":"))
    print(f"Saved map: {MAP_PATH}")

    # Stats
    import os
    size_kb = os.path.getsize(ATLAS_PATH) / 1024
    print(f"Atlas file size: {size_kb:.0f} KB")

if __name__ == "__main__":
    main()
