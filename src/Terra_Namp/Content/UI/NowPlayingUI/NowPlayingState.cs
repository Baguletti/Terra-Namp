using Microsoft.Xna.Framework;
using Terra_Namp.Common.UI.Abstract;
using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria.UI;

namespace Terra_Namp.Content.UI.NowPlayingUI
{
    public class NowPlayingState : SmartUIState
    {
        private NowPlayingPopup currentPopup;

        // Above everything else layer-wise.
        public override int InsertionIndex(List<GameInterfaceLayer> layers) => layers.Count - 1;

        public override void OnInitialize() => Visible = true;

        public override void SafeUpdate(GameTime gameTime)
        {
            if (currentPopup != null && currentPopup.IsCompleted)
            {
                RemoveChild(currentPopup);
                currentPopup = null;
            }

            base.SafeUpdate(gameTime);
        }

        public void NotifyActiveSong(string name, string author)
        {
            if (!ModContent.GetInstance<Terra_NampConfig>().SendNowPlayingMessages)
            {
                return;
            }

            // If there's already an active popup then don't bother with the initial animation.
            if (currentPopup != null)
            {
                RemoveChild(currentPopup);
                currentPopup = new(name, author, true);
            }
            else
            {
                currentPopup = new(name, author);
            }

            Append(currentPopup);
        }
    }
}
