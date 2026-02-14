using Microsoft.Xna.Framework;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace Terra_Namp;

public class Terra_NampConfig : ModConfig
{
	public override ConfigScope Mode => ConfigScope.ClientSide;

	[DefaultValue(true)]
	public bool SendNowPlayingMessages { get; set; }

	[DefaultValue(true)]
	public bool EnablePrefetch { get; set; }
}
