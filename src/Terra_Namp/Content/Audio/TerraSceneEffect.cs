using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Core.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terra_Namp.Content.Audio
{
    public class TerraSceneEffect : ModSceneEffect
    {
        public override int Music => Main.dedServ ? 0 : MusicLoader.GetMusicSlot(Mod, Terra_Namp.Silence);

        public override bool IsSceneEffectActive(Player player) => TerraUILoader.GetUIState<TerraState>()?.MainPanel?.ActiveSong != null;

        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        public override float GetWeight(Player player) => 1;
    }
}
