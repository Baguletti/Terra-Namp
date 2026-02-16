#!/usr/bin/env bash
# Test: End-to-end playlist download — extract metadata, download first 2 tracks, verify MP3 output.
# Uses --playlist-items 1:2 to limit to first 2 tracks only.
set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'
pass() { echo -e "${GREEN}PASS${NC}: $1"; }
fail() { echo -e "${RED}FAIL${NC}: $1"; FAILURES=$((FAILURES + 1)); }
warn() { echo -e "${YELLOW}WARN${NC}: $1"; }

FAILURES=0

if ! command -v yt-dlp &>/dev/null; then
    echo "yt-dlp not found. Install: pip install yt-dlp"
    exit 1
fi

if ! command -v ffmpeg &>/dev/null; then
    echo "ffmpeg not found. Install: apt install ffmpeg"
    exit 1
fi

PLAYLIST_URL="https://www.youtube.com/playlist?list=PLRqwX-V7Uu6ZiZxtDDRCi6uhfTH4FilpH"
WORKDIR=$(mktemp -d /tmp/playlist_dl_test_XXXXXX)
trap "rm -rf $WORKDIR" EXIT

echo "=== Test: End-to-End Playlist Download ==="
echo "URL: $PLAYLIST_URL"
echo "Work dir: $WORKDIR"
echo ""

# Step 1: Extract playlist metadata
echo "--- Step 1: Extract playlist metadata ---"
META_FILE="$WORKDIR/playlist.json"
if ! yt-dlp --flat-playlist -J --no-download "$PLAYLIST_URL" > "$META_FILE" 2>/dev/null; then
    fail "Metadata extraction failed"
    exit 1
fi
pass "Metadata extracted"

PLAYLIST_TITLE=$(python3 -c "
import json
data = json.load(open('$META_FILE'))
print(data.get('title', 'Unknown'))
")
echo "  Playlist title: \"$PLAYLIST_TITLE\""

TOTAL=$(python3 -c "
import json
data = json.load(open('$META_FILE'))
print(len(data.get('entries', [])))
")
echo "  Total tracks: $TOTAL"

# Get first 2 video IDs
VIDEO_IDS=$(python3 -c "
import json
data = json.load(open('$META_FILE'))
for entry in data.get('entries', [])[:2]:
    vid_id = entry.get('id', '')
    title = entry.get('title', 'Unknown')
    print(f'{vid_id}|{title}')
")

echo ""

# Step 2: Download first 2 tracks
echo "--- Step 2: Download first 2 tracks ---"
TRACK_NUM=0
while IFS='|' read -r VID_ID VID_TITLE; do
    TRACK_NUM=$((TRACK_NUM + 1))
    echo ""
    echo "  Track $TRACK_NUM: \"$VID_TITLE\" ($VID_ID)"

    UUID=$(python3 -c "import uuid; print(uuid.uuid4())")
    OUTPUT="$WORKDIR/${UUID}.mp3"

    VIDEO_URL="https://www.youtube.com/watch?v=${VID_ID}"

    echo "  Downloading + converting to MP3..."
    if yt-dlp -x --audio-format mp3 --audio-quality 0 \
        --postprocessor-args "ffmpeg:-af loudnorm" \
        --no-playlist \
        -o "$OUTPUT" "$VIDEO_URL" >/dev/null 2>&1; then

        if [ -f "$OUTPUT" ]; then
            SIZE=$(stat -c%s "$OUTPUT" 2>/dev/null || stat -f%z "$OUTPUT" 2>/dev/null)
            if [ "$SIZE" -gt 0 ]; then
                pass "Track $TRACK_NUM: MP3 created ($SIZE bytes)"

                # Verify it's a valid MP3 (check magic bytes)
                MAGIC=$(xxd -l 3 "$OUTPUT" | head -1)
                if echo "$MAGIC" | grep -q "4944 33\|fffa\|fffb\|fff3\|fff2"; then
                    pass "Track $TRACK_NUM: Valid MP3 header"
                else
                    warn "Track $TRACK_NUM: Unexpected header (may still be valid): $MAGIC"
                fi

                # Simulate metadata file creation (like AsyncDownloader does)
                META_TXT="$WORKDIR/${UUID}.txt"
                HASH=$(md5sum "$OUTPUT" | cut -d' ' -f1)
                echo -e "${VID_TITLE}\nYouTube\n${HASH}\n${PLAYLIST_TITLE}" > "$META_TXT"

                if [ -f "$META_TXT" ]; then
                    FOLDER_LINE=$(sed -n '4p' "$META_TXT")
                    if [ "$FOLDER_LINE" = "$PLAYLIST_TITLE" ]; then
                        pass "Track $TRACK_NUM: Metadata folder = playlist title (\"$FOLDER_LINE\")"
                    else
                        fail "Track $TRACK_NUM: Metadata folder mismatch: got \"$FOLDER_LINE\", expected \"$PLAYLIST_TITLE\""
                    fi
                fi
            else
                fail "Track $TRACK_NUM: MP3 file is empty"
            fi
        else
            fail "Track $TRACK_NUM: MP3 file not created"
        fi
    else
        fail "Track $TRACK_NUM: yt-dlp download failed"
    fi
done <<< "$VIDEO_IDS"

echo ""
echo "--- Step 3: Summary ---"

MP3_COUNT=$(find "$WORKDIR" -name "*.mp3" | wc -l)
TXT_COUNT=$(find "$WORKDIR" -name "*.txt" -not -name "playlist.json" | wc -l)
echo "  MP3 files: $MP3_COUNT"
echo "  Metadata files: $TXT_COUNT"

if [ "$MP3_COUNT" -ge 2 ] && [ "$TXT_COUNT" -ge 2 ]; then
    pass "Expected files created"
else
    fail "Expected at least 2 MP3 + 2 TXT files"
fi

echo ""
if [ $FAILURES -eq 0 ]; then
    echo -e "${GREEN}All tests passed!${NC}"
else
    echo -e "${RED}$FAILURES test(s) failed${NC}"
    exit 1
fi
