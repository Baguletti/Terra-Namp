#!/usr/bin/env bash
# Convert MP4 to optimized GIF for Steam Workshop (< 10 MB for Imgur animation)
# Usage: ./mp4_to_gif.sh input.mp4 [output.gif] [width] [fps]
set -euo pipefail

INPUT="${1:?Usage: $0 input.mp4 [output.gif] [width] [fps]}"
OUTPUT="${2:-${INPUT%.mp4}.gif}"
WIDTH="${3:-720}"
FPS="${4:-15}"

if ! command -v ffmpeg &>/dev/null; then
    echo "ffmpeg not found. Install: apt install ffmpeg"
    exit 1
fi

echo "Input:  $INPUT"
echo "Output: $OUTPUT"
echo "Width:  ${WIDTH}px, FPS: $FPS"
echo ""

# Two-pass: generate palette first, then use it for better quality + smaller size
PALETTE=$(mktemp /tmp/palette_XXXXXX.png)
trap "rm -f $PALETTE" EXIT

echo "Pass 1: Generating palette..."
ffmpeg -y -i "$INPUT" \
    -vf "fps=$FPS,scale=$WIDTH:-1:flags=lanczos,palettegen=stats_mode=diff" \
    "$PALETTE" 2>/dev/null

echo "Pass 2: Encoding GIF..."
ffmpeg -y -i "$INPUT" -i "$PALETTE" \
    -lavfi "fps=$FPS,scale=$WIDTH:-1:flags=lanczos [x]; [x][1:v] paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle" \
    "$OUTPUT" 2>/dev/null

SIZE=$(stat -c%s "$OUTPUT" 2>/dev/null || stat -f%z "$OUTPUT")
SIZE_MB=$(echo "scale=1; $SIZE / 1048576" | bc)

echo ""
echo "Done: $OUTPUT ($SIZE_MB MB)"

if (( $(echo "$SIZE_MB > 10" | bc -l) )); then
    echo "WARNING: File > 10 MB — Imgur will convert to MP4 (no animation in [img] tag)"
    echo "Try: lower width (480), lower fps (10), or trim the video first"
fi
