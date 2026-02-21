using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using ReLogic.Localization.IME;
using ReLogic.OS;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;

namespace Terra_Namp.Core.UI;

/// <summary>
/// Reusable text input handler with cursor positioning, arrow key navigation,
/// key repeat, surrogate pair awareness, and IME composition support.
/// Call <see cref="HandleInput"/> from the Draw phase, then use
/// <see cref="DrawCursor"/> to render the blinking caret.
/// </summary>
public class TextInputHandler
{
    private const int RepeatDelay = 18; // ~0.3s at 60fps
    private const int RepeatInterval = 2; // ~30 chars/s

    private string _text = "";
    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? "";
            if (CursorPos > _text.Length)
                CursorPos = _text.Length;
        }
    }

    public int CursorPos { get; set; }

    private KeyboardState _prevKeys;
    private Keys _heldKey;
    private int _heldTicks;
    private bool _oldHasCompositionString;

    /// <summary>
    /// Process keyboard input. Call once per frame from Draw() while the field is focused.
    /// Returns true if the text changed this frame.
    /// </summary>
    public bool HandleInput()
    {
        PlayerInput.WritingText = true;
        Main.instance.HandleIME();

        // Split at cursor so GetInputText types/backspaces at the right position
        CursorPos = Math.Clamp(CursorPos, 0, Text.Length);
        string left = Text[..CursorPos];
        string right = Text[CursorPos..];
        string newLeft = Main.GetInputText(left);

        // IME composition workaround: GetInputText deletes a character in
        // both the composition string and the text on Backspace. Revert
        // the text change when a composition string was active.
        if (_oldHasCompositionString && Main.inputText.IsKeyDown(Keys.Back))
            newLeft = left;

        _oldHasCompositionString = Platform.Get<IImeService>().CompositionString is { Length: > 0 };

        int delta = newLeft.Length - left.Length;
        bool changed = delta != 0;
        Text = newLeft + right;
        CursorPos = Math.Clamp(CursorPos + delta, 0, Text.Length);

        // Navigation & Delete with key repeat
        var keys = Main.keyState;
        HandleRepeatableKey(keys, Keys.Left, () =>
            CursorPos = MoveCursorLeft(Text, CursorPos));
        HandleRepeatableKey(keys, Keys.Right, () =>
            CursorPos = MoveCursorRight(Text, CursorPos));
        HandleRepeatableKey(keys, Keys.Delete, () =>
        {
            if (CursorPos < Text.Length)
            {
                int rem = (CursorPos < Text.Length - 1
                    && char.IsHighSurrogate(Text[CursorPos])
                    && char.IsLowSurrogate(Text[CursorPos + 1])) ? 2 : 1;
                Text = Text[..CursorPos] + Text[(CursorPos + rem)..];
                changed = true;
            }
        });

        if (JustPressed(keys, Keys.Home))
            CursorPos = 0;
        if (JustPressed(keys, Keys.End))
            CursorPos = Text.Length;

        _prevKeys = keys;
        return changed;
    }

    /// <summary>
    /// Draw the blinking cursor line at the current position.
    /// Uses EmojiRenderer.MeasureWidth for emoji-aware measurement.
    /// </summary>
    public void DrawCursor(SpriteBatch spriteBatch, DynamicSpriteFont font, float textScale,
        float textStartX, float fieldY, float fieldHeight, Color color)
    {
        if (Main.GameUpdateCount % 40 >= 20) return;

        string beforeCursor = Text[..Math.Min(CursorPos, Text.Length)];
        float cursorX = textStartX + EmojiRenderer.MeasureWidth(font, beforeCursor, textScale) + 1;
        float cursorY = fieldY + 4;
        float cursorHeight = fieldHeight - 8;

        spriteBatch.Draw(TextureAssets.MagicPixel.Value,
            new Rectangle((int)cursorX, (int)cursorY, 2, (int)cursorHeight), color);
    }

    /// <summary>
    /// Draw the blinking cursor for standard SpriteFont (no emoji).
    /// </summary>
    public void DrawCursorPlain(SpriteBatch spriteBatch, DynamicSpriteFont font, float textScale,
        float textStartX, float fieldY, float fieldHeight, Color color)
    {
        if (Main.GameUpdateCount % 40 >= 20) return;

        string beforeCursor = Text[..Math.Min(CursorPos, Text.Length)];
        float cursorX = textStartX + font.MeasureString(beforeCursor).X * textScale + 1;
        float cursorY = fieldY + 4;
        float cursorHeight = fieldHeight - 8;

        spriteBatch.Draw(TextureAssets.MagicPixel.Value,
            new Rectangle((int)cursorX, (int)cursorY, 2, (int)cursorHeight), color);
    }

    /// <summary>
    /// Set cursor position from a mouse click X coordinate.
    /// Uses EmojiRenderer for emoji-aware hit testing.
    /// </summary>
    public void SetCursorFromClick(DynamicSpriteFont font, float textScale, float textStartX, float clickX)
    {
        CursorPos = GetCursorPosFromClick(font, Text, textScale, textStartX, clickX);
    }

    /// <summary>
    /// Set cursor position from click using standard SpriteFont (no emoji).
    /// </summary>
    public void SetCursorFromClickPlain(DynamicSpriteFont font, float textScale, float textStartX, float clickX)
    {
        CursorPos = GetCursorPosFromClickPlain(font, Text, textScale, textStartX, clickX);
    }

    /// <summary>
    /// Insert text at the current cursor position (e.g. emoji from picker).
    /// </summary>
    public void InsertAtCursor(string value)
    {
        Text = Text[..CursorPos] + value + Text[CursorPos..];
        CursorPos += value.Length;
    }

    /// <summary>
    /// Clear text and reset cursor.
    /// </summary>
    public void Clear()
    {
        Text = "";
        CursorPos = 0;
    }

    // --- Private helpers ---

    private bool JustPressed(KeyboardState current, Keys key)
        => current.IsKeyDown(key) && !_prevKeys.IsKeyDown(key);

    private void HandleRepeatableKey(KeyboardState current, Keys key, Action action)
    {
        if (!current.IsKeyDown(key))
        {
            if (_heldKey == key) { _heldKey = 0; _heldTicks = 0; }
            return;
        }

        if (_heldKey != key)
        {
            _heldKey = key;
            _heldTicks = 0;
            action();
            return;
        }

        _heldTicks++;
        if (_heldTicks >= RepeatDelay && (_heldTicks - RepeatDelay) % RepeatInterval == 0)
            action();
    }

    private static int MoveCursorLeft(string text, int pos)
    {
        if (pos <= 0) return 0;
        if (pos >= 2 && char.IsLowSurrogate(text[pos - 1]) && char.IsHighSurrogate(text[pos - 2]))
            return pos - 2;
        return pos - 1;
    }

    private static int MoveCursorRight(string text, int pos)
    {
        if (pos >= text.Length) return text.Length;
        if (pos < text.Length - 1 && char.IsHighSurrogate(text[pos]) && char.IsLowSurrogate(text[pos + 1]))
            return pos + 2;
        return pos + 1;
    }

    private static int GetCursorPosFromClick(DynamicSpriteFont font, string text, float textScale, float textStartX, float clickX)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        float relX = clickX - textStartX;
        if (relX <= 0) return 0;

        for (int i = 0; i < text.Length;)
        {
            int charLen = (i < text.Length - 1 && char.IsHighSurrogate(text[i]) && char.IsLowSurrogate(text[i + 1])) ? 2 : 1;
            float widthBefore = EmojiRenderer.MeasureWidth(font, text[..i], textScale);
            float widthAfter = EmojiRenderer.MeasureWidth(font, text[..(i + charLen)], textScale);
            if (relX < (widthBefore + widthAfter) / 2f)
                return i;
            i += charLen;
        }

        return text.Length;
    }

    private static int GetCursorPosFromClickPlain(DynamicSpriteFont font, string text, float textScale, float textStartX, float clickX)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        float relX = clickX - textStartX;
        if (relX <= 0) return 0;

        for (int i = 0; i < text.Length;)
        {
            int charLen = (i < text.Length - 1 && char.IsHighSurrogate(text[i]) && char.IsLowSurrogate(text[i + 1])) ? 2 : 1;
            float widthBefore = font.MeasureString(text[..i]).X * textScale;
            float widthAfter = font.MeasureString(text[..(i + charLen)]).X * textScale;
            if (relX < (widthBefore + widthAfter) / 2f)
                return i;
            i += charLen;
        }

        return text.Length;
    }
}
