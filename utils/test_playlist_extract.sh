#!/usr/bin/env bash
# Test: yt-dlp --flat-playlist -J extracts correct playlist metadata structure.
# Validates JSON has: title, entries[] with id/title/duration fields.
set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'
pass() { echo -e "${GREEN}PASS${NC}: $1"; }
fail() { echo -e "${RED}FAIL${NC}: $1"; FAILURES=$((FAILURES + 1)); }
warn() { echo -e "${YELLOW}WARN${NC}: $1"; }

FAILURES=0

# Check yt-dlp
if ! command -v yt-dlp &>/dev/null; then
    echo "yt-dlp not found. Install: pip install yt-dlp"
    exit 1
fi

# Small public YouTube playlist (YouTube's own "Popular on YouTube" is too large)
# Using a small, stable public playlist — "Learn Colors" (short educational clips)
PLAYLIST_URL="https://www.youtube.com/playlist?list=PLRqwX-V7Uu6ZiZxtDDRCi6uhfTH4FilpH"

echo "=== Test: Playlist Metadata Extraction ==="
echo "URL: $PLAYLIST_URL"
echo ""

# Extract playlist info
TMPFILE=$(mktemp /tmp/playlist_test_XXXXXX.json)
trap "rm -f $TMPFILE" EXIT

echo "Running: yt-dlp --flat-playlist -J --no-download ..."
if ! yt-dlp --flat-playlist -J --no-download "$PLAYLIST_URL" > "$TMPFILE" 2>/dev/null; then
    fail "yt-dlp exited with non-zero code"
    exit 1
fi
pass "yt-dlp executed successfully"

# Validate JSON structure
if ! python3 -c "import json; json.load(open('$TMPFILE'))" 2>/dev/null; then
    fail "Output is not valid JSON"
    exit 1
fi
pass "Output is valid JSON"

# Check required fields
TITLE=$(python3 -c "
import json, sys
data = json.load(open('$TMPFILE'))
print(data.get('title', ''))
")

if [ -z "$TITLE" ]; then
    fail "Missing 'title' field"
else
    pass "Playlist title: \"$TITLE\""
fi

# Check _type field
TYPE=$(python3 -c "
import json
data = json.load(open('$TMPFILE'))
print(data.get('_type', ''))
")

if [ "$TYPE" = "playlist" ]; then
    pass "_type is 'playlist'"
else
    warn "_type is '$TYPE' (expected 'playlist')"
fi

# Check entries array
ENTRY_COUNT=$(python3 -c "
import json
data = json.load(open('$TMPFILE'))
entries = data.get('entries', [])
print(len(entries))
")

if [ "$ENTRY_COUNT" -gt 0 ]; then
    pass "Found $ENTRY_COUNT entries"
else
    fail "No entries found"
fi

# Validate first entry has required fields
python3 -c "
import json, sys
data = json.load(open('$TMPFILE'))
entries = data.get('entries', [])
if not entries:
    sys.exit(1)

entry = entries[0]
fields = {'id': str, 'title': str}
for field, expected_type in fields.items():
    val = entry.get(field)
    if val is None:
        print(f'MISSING: entries[0].{field}')
        sys.exit(1)
    print(f'  entries[0].{field} = \"{val}\"')

# Duration may be null in flat-playlist mode
dur = entry.get('duration')
print(f'  entries[0].duration = {dur}')
" && pass "First entry has id, title fields" || fail "First entry missing required fields"

echo ""
if [ $FAILURES -eq 0 ]; then
    echo -e "${GREEN}All tests passed!${NC}"
else
    echo -e "${RED}$FAILURES test(s) failed${NC}"
    exit 1
fi
