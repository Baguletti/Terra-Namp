#!/usr/bin/env bash
# Test: Verify --flat-playlist -J response differs between single video and playlist URLs.
# Single video: no "entries" array (or entries with 1 item, no _type=playlist)
# Playlist URL: has "entries" array and _type=playlist
set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; NC='\033[0m'
pass() { echo -e "${GREEN}PASS${NC}: $1"; }
fail() { echo -e "${RED}FAIL${NC}: $1"; FAILURES=$((FAILURES + 1)); }

FAILURES=0

if ! command -v yt-dlp &>/dev/null; then
    echo "yt-dlp not found. Install: pip install yt-dlp"
    exit 1
fi

# --- Test 1: Single video URL ---
echo "=== Test 1: Single Video URL ==="
SINGLE_URL="https://www.youtube.com/watch?v=jNQXAC9IVRw"  # "Me at the zoo" — first YouTube video
TMPFILE=$(mktemp /tmp/single_test_XXXXXX.json)
trap "rm -f $TMPFILE" EXIT

echo "URL: $SINGLE_URL"
if yt-dlp --flat-playlist -J --no-download "$SINGLE_URL" > "$TMPFILE" 2>/dev/null; then
    TYPE=$(python3 -c "
import json
data = json.load(open('$TMPFILE'))
print(data.get('_type', 'video'))
")
    ENTRY_COUNT=$(python3 -c "
import json
data = json.load(open('$TMPFILE'))
entries = data.get('entries', [])
print(len(entries))
")
    TITLE=$(python3 -c "
import json
data = json.load(open('$TMPFILE'))
print(data.get('title', ''))
")

    echo "  _type=$TYPE, entries=$ENTRY_COUNT, title=\"$TITLE\""

    if [ "$TYPE" != "playlist" ]; then
        pass "Single video: _type is NOT 'playlist' (got '$TYPE')"
    elif [ "$ENTRY_COUNT" -le 1 ]; then
        pass "Single video: has $ENTRY_COUNT entries (treated as single)"
    else
        fail "Single video unexpectedly returned playlist with $ENTRY_COUNT entries"
    fi
else
    fail "yt-dlp failed for single video URL"
fi

echo ""

# --- Test 2: Playlist URL ---
echo "=== Test 2: Playlist URL ==="
PLAYLIST_URL="https://www.youtube.com/playlist?list=PLRqwX-V7Uu6ZiZxtDDRCi6uhfTH4FilpH"
TMPFILE2=$(mktemp /tmp/playlist_test_XXXXXX.json)
trap "rm -f $TMPFILE $TMPFILE2" EXIT

echo "URL: $PLAYLIST_URL"
if yt-dlp --flat-playlist -J --no-download "$PLAYLIST_URL" > "$TMPFILE2" 2>/dev/null; then
    TYPE=$(python3 -c "
import json
data = json.load(open('$TMPFILE2'))
print(data.get('_type', 'video'))
")
    ENTRY_COUNT=$(python3 -c "
import json
data = json.load(open('$TMPFILE2'))
entries = data.get('entries', [])
print(len(entries))
")
    TITLE=$(python3 -c "
import json
data = json.load(open('$TMPFILE2'))
print(data.get('title', ''))
")

    echo "  _type=$TYPE, entries=$ENTRY_COUNT, title=\"$TITLE\""

    if [ "$TYPE" = "playlist" ]; then
        pass "Playlist URL: _type is 'playlist'"
    else
        fail "Playlist URL: expected _type='playlist', got '$TYPE'"
    fi

    if [ "$ENTRY_COUNT" -gt 1 ]; then
        pass "Playlist URL: has $ENTRY_COUNT entries (>1)"
    else
        fail "Playlist URL: expected >1 entries, got $ENTRY_COUNT"
    fi
else
    fail "yt-dlp failed for playlist URL"
fi

echo ""

# --- Test 3: Video URL with list= parameter (should NOT be treated as playlist by our heuristic) ---
echo "=== Test 3: URL Heuristic (C# IsLikelyPlaylist logic) ==="

check_heuristic() {
    local url="$1"
    local expected="$2"
    local label="$3"

    # Simulate the C# IsLikelyPlaylist logic:
    # Returns true only if URL contains "/playlist?" or "music.youtube.com/playlist"
    local result="false"
    if echo "$url" | grep -qi "/playlist?"; then
        result="true"
    elif echo "$url" | grep -qi "music.youtube.com/playlist"; then
        result="true"
    fi

    if [ "$result" = "$expected" ]; then
        pass "$label: IsLikelyPlaylist=$result (expected $expected)"
    else
        fail "$label: IsLikelyPlaylist=$result (expected $expected)"
    fi
}

check_heuristic "https://www.youtube.com/watch?v=dQw4w9WgXcQ" "false" "Plain video"
check_heuristic "https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLxxx" "false" "Video with list= param"
check_heuristic "https://www.youtube.com/playlist?list=PLxxx" "true" "Explicit playlist page"
check_heuristic "https://music.youtube.com/playlist?list=PLxxx" "true" "YouTube Music playlist"

echo ""
if [ $FAILURES -eq 0 ]; then
    echo -e "${GREEN}All tests passed!${NC}"
else
    echo -e "${RED}$FAILURES test(s) failed${NC}"
    exit 1
fi
