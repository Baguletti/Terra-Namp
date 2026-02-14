using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Terraria.ModLoader;

namespace Terra_Namp.Core.UI;

/// <summary>
/// Renders text with inline color emoji from a Twemoji spritesheet atlas.
/// Emoji codepoints are drawn as colored sprites; regular characters use the provided SpriteFont.
/// </summary>
public static class EmojiRenderer
{
    private static Texture2D atlas;
    private static Dictionary<int, Rectangle> glyphMap;
    private static int glyphSize;
    private static bool loaded;

    public static bool IsLoaded => loaded;

    public static Texture2D Atlas => atlas;

    public static IReadOnlyDictionary<int, Rectangle> GlyphMap => glyphMap;

    private static int[] sortedCodepoints;

    public static int[] GetSortedCodepoints()
    {
        if (sortedCodepoints == null && glyphMap != null)
        {
            var keys = new List<int>(glyphMap.Keys);
            keys.Sort((a, b) =>
            {
                int catA = GetEmojiCategory(a);
                int catB = GetEmojiCategory(b);
                if (catA != catB) return catA.CompareTo(catB);
                return a.CompareTo(b);
            });
            sortedCodepoints = keys.ToArray();
        }
        return sortedCodepoints ?? System.Array.Empty<int>();
    }

    /// <summary>
    /// Returns category index for emoji sorting (messenger-style grouping).
    /// Lower number = shown first in the picker.
    /// </summary>
    private static int GetEmojiCategory(int cp)
    {
        // 0: Smileys & Emotion
        if (cp >= 0x1F600 && cp <= 0x1F64F) return 0;  // 😀-🙏
        if (cp >= 0x1F910 && cp <= 0x1F92F) return 0;  // 🤐-🤯
        if (cp >= 0x1F970 && cp <= 0x1F97A) return 0;  // 🥰-🥺
        if (cp == 0x263A || cp == 0x2639) return 0;     // ☺☹
        if (cp >= 0x1FAE0 && cp <= 0x1FAE8) return 0;  // 🫠-🫨 newer faces
        if (cp == 0x1F9D0) return 0;                    // 🧐 face with monocle

        // 1: Hearts & Gestures
        if (cp >= 0x1F440 && cp <= 0x1F450) return 1;  // 👀-👐 body parts & pointing
        if (cp >= 0x1F490 && cp <= 0x1F49F) return 1;  // 💐-💟
        if (cp >= 0x270A && cp <= 0x270D) return 1;     // ✊-✍
        if (cp == 0x1F4AA) return 1;                    // 💪
        if (cp == 0x2764) return 1;                     // ❤
        if (cp >= 0x1F90C && cp <= 0x1F90F) return 1;  // 🤌-🤏 pinch/hearts
        if (cp >= 0x1F91A && cp <= 0x1F91F) return 1;  // 🤚-🤟 hand gestures
        if (cp >= 0x1FAF0 && cp <= 0x1FAF8) return 1;  // 🫰-🫸 newer hand gestures

        // 2: People
        if (cp >= 0x1F466 && cp <= 0x1F487) return 2;  // 👦-💇
        if (cp >= 0x1F930 && cp <= 0x1F93A) return 2;  // 🤰-🤺
        if (cp >= 0x1F9B0 && cp <= 0x1F9DD) return 2;  // 🦰-🧝

        // 3: Animals
        if (cp >= 0x1F400 && cp <= 0x1F43F) return 3;  // 🐀-🐿
        if (cp >= 0x1F980 && cp <= 0x1F9AE) return 3;  // 🦀-🦮

        // 4: Nature & Weather
        if (cp >= 0x1F300 && cp <= 0x1F321) return 4;  // 🌀-🌡
        if (cp >= 0x1F330 && cp <= 0x1F344) return 4;  // 🌰-🍄
        if (cp >= 0x2600 && cp <= 0x2614) return 4;     // ☀-☔

        // 5: Food & Drink
        if (cp >= 0x1F345 && cp <= 0x1F37F) return 5;  // 🍅-🍿
        if (cp >= 0x1F950 && cp <= 0x1F96F) return 5;  // 🥐-🥯
        if (cp == 0x2615) return 5;                     // ☕

        // 6: Activities & Sports
        if (cp >= 0x1F3A0 && cp <= 0x1F3CA) return 6;  // 🎠-🏊
        if (cp >= 0x1F3CB && cp <= 0x1F3CE) return 6;  // 🏋-🏎
        if (cp >= 0x1F93C && cp <= 0x1F945) return 6;  // 🤼-🥅
        if (cp == 0x26BD || cp == 0x26BE) return 6;     // ⚽⚾

        // 7: Travel & Places
        if (cp >= 0x1F680 && cp <= 0x1F6FF) return 7;  // 🚀-🛿
        if (cp >= 0x1F3CF && cp <= 0x1F3F0) return 7;  // 🏏-🏰

        // 8: Objects
        if (cp >= 0x1F380 && cp <= 0x1F39F) return 8;  // 🎀-🎟
        if (cp >= 0x1F4A0 && cp <= 0x1F53D) return 8;  // 💠-🔽

        // 9: Symbols & everything else
        return 9;
    }

    public static void Load(Mod mod)
    {
        // Load the emoji atlas texture
        atlas = ModContent.Request<Texture2D>("Terra_Namp/Assets/UI/Emoji/emoji_atlas", AssetRequestMode.ImmediateLoad).Value;

        // Load the glyph mapping from JSON
        byte[] mapBytes = mod.GetFileBytes("Assets/UI/Emoji/emoji_map.json");
        string mapJson = System.Text.Encoding.UTF8.GetString(mapBytes);

        var mapData = JsonSerializer.Deserialize<EmojiMapData>(mapJson);
        glyphSize = mapData.GlyphSize;
        glyphMap = new Dictionary<int, Rectangle>();

        foreach (var entry in mapData.Glyphs)
        {
            // Parse hex codepoint to int
            if (int.TryParse(entry.Key, System.Globalization.NumberStyles.HexNumber, null, out int codepoint))
            {
                glyphMap[codepoint] = new Rectangle(entry.Value.X, entry.Value.Y, glyphSize, glyphSize);
            }
        }

        loaded = true;
    }

    /// <summary>
    /// Draws a string with inline emoji. Emoji codepoints are rendered from the atlas;
    /// other characters are rendered with the provided SpriteFont.
    /// </summary>
    public static void DrawString(SpriteBatch sb, DynamicSpriteFont font, string text,
        Vector2 position, Color textColor, float scale)
    {
        if (string.IsNullOrEmpty(text))
            return;

        float charHeight = font.MeasureString("A").Y * scale;
        float x = position.X;
        int i = 0;

        while (i < text.Length)
        {
            int codepoint = GetCodepoint(text, ref i);

            if (loaded && glyphMap != null && glyphMap.TryGetValue(codepoint, out Rectangle srcRect))
            {
                // Draw colored emoji from atlas (preserve emoji colors, apply alpha from textColor)
                float emojiSize = charHeight;
                sb.Draw(atlas, new Rectangle((int)x, (int)position.Y, (int)emojiSize, (int)emojiSize),
                    srcRect, Color.White * (textColor.A / 255f));
                x += emojiSize;
            }
            else
            {
                // Skip orphan surrogates
                if (codepoint >= 0xD800 && codepoint <= 0xDFFF)
                    continue;

                // Draw regular character with font
                string ch = char.ConvertFromUtf32(codepoint);
                sb.DrawString(font, ch, new Vector2(x, position.Y), textColor,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                x += font.MeasureString(ch).X * scale;
            }
        }
    }

    /// <summary>
    /// Measures the width of a string with emoji, matching DrawString layout.
    /// </summary>
    public static float MeasureWidth(DynamicSpriteFont font, string text, float scale)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        float charHeight = font.MeasureString("A").Y * scale;
        float width = 0f;
        int i = 0;

        while (i < text.Length)
        {
            int codepoint = GetCodepoint(text, ref i);

            if (loaded && glyphMap != null && glyphMap.TryGetValue(codepoint, out _))
            {
                width += charHeight; // emoji rendered at text height
            }
            else
            {
                if (codepoint >= 0xD800 && codepoint <= 0xDFFF)
                    continue;

                string ch = char.ConvertFromUtf32(codepoint);
                width += font.MeasureString(ch).X * scale;
            }
        }

        return width;
    }

    /// <summary>
    /// Extracts a Unicode codepoint from a string, handling UTF-16 surrogate pairs.
    /// Advances the index past the consumed characters.
    /// </summary>
    private static int GetCodepoint(string text, ref int index)
    {
        if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            int cp = char.ConvertToUtf32(text[index], text[index + 1]);
            index += 2;
            return cp;
        }

        int result = text[index];
        index++;
        return result;
    }

    private class EmojiMapData
    {
        public int GlyphSize { get; set; }
        public Dictionary<string, GlyphPosition> Glyphs { get; set; }
    }

    private class GlyphPosition
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
