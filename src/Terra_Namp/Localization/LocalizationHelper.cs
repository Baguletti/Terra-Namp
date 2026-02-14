// Credit to Scalie for LocalizationHelper - https://github.com/ScalarVector1/DragonLens/blob/407a54e45d7a4828f660b46988feaf86092249b3/Helpers/LocalizationHelper.cs

using Terraria.Localization;

namespace Terra_Namp.Localization;

public static class LocalizationHelper
{
    /// <summary>
    /// Gets a localized text value of the mod.
    /// If no localization is found, the key itself is returned.
    /// </summary>
    /// <param name="key">the localization key</param>
    /// <param name="args">optional args that should be passed</param>
    /// <returns>the text should be displayed</returns>
    public static string GetText(string key, params object[] args)
    {
        return Language.Exists($"Mods.Terra_Namp.{key}") ? Language.GetTextValue($"Mods.Terra_Namp.{key}", args) : key;
    }

    public static string GetGUIText(string key, params object[] args)
    {
        return GetText($"UI.{key}", args);
    }
}
