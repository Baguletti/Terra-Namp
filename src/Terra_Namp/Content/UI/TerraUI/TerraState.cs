using Microsoft.Xna.Framework;
using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.IO;
using Terra_Namp.Content.UI.TerraUI.Enums;
using Terra_Namp.Core.IO;
using System.Collections.Generic;
using Terraria;
using Terraria.UI;

namespace Terra_Namp.Content.UI.TerraUI
{
    public class TerraState : SmartUIState
    {
        public TerraMainPanel MainPanel { get; private set; }
        public MiniPlayerPanel MiniPanel { get; private set; }
        public PlayerViewMode ViewMode { get; private set; } = PlayerViewMode.Hidden;

        // Top-left position tracked across mode switches (avoids center-based mismatch between panel sizes)
        private Vector2? lastVisibleTopLeft;

        public override bool Visible
        {
            get => ViewMode != PlayerViewMode.Hidden;
            set { }
        }

        public override int InsertionIndex(List<GameInterfaceLayer> layers) => layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));

        public override void OnInitialize()
        {
            MainPanel = new();
            MainPanel.Width.Set(TerraMainPanel.PanelWidth, 0);
            MainPanel.Height.Set(TerraMainPanel.PanelHeight, 0);
            Append(MainPanel);
            MainPanel.Activate();

            MiniPanel = new();
            MiniPanel.FullPanel = MainPanel;
            MiniPanel.Width.Set(MiniPlayerPanel.MiniWidth, 0);
            MiniPanel.Height.Set(MiniPlayerPanel.MiniHeight, 0);
            MiniPanel.Left.Set(-9999, 0);
            Append(MiniPanel);
            MiniPanel.Activate();

            Recalculate();
        }

        public void CycleViewMode()
        {
            switch (ViewMode)
            {
                case PlayerViewMode.Hidden:
                    ViewMode = PlayerViewMode.Full;
                    ShowFullPanel();
                    break;
                case PlayerViewMode.Full:
                    if (PersistentDataStoreSystem.GetDataStore<TerraDataStore>().MiniPlayerEnabled)
                    {
                        ViewMode = PlayerViewMode.Mini;
                        SwitchToMini();
                    }
                    else
                    {
                        ViewMode = PlayerViewMode.Hidden;
                        HideAll();
                    }
                    break;
                case PlayerViewMode.Mini:
                    ViewMode = PlayerViewMode.Hidden;
                    HideAll();
                    break;
            }
        }

        private void ShowFullPanel()
        {
            float x, y;

            if (lastVisibleTopLeft.HasValue)
            {
                x = lastVisibleTopLeft.Value.X;
                y = lastVisibleTopLeft.Value.Y;
            }
            else
            {
                // First open — use stored center-based position
                var pos = MainPanel.DefaultPosition;
                x = pos.X * Main.screenWidth - TerraMainPanel.PanelWidth / 2f;
                y = pos.Y * Main.screenHeight - TerraMainPanel.PanelHeight / 2f;
            }

            x = MathHelper.Clamp(x, 0, Main.screenWidth - TerraMainPanel.PanelWidth);
            y = MathHelper.Clamp(y, 0, Main.screenHeight - TerraMainPanel.PanelHeight);

            MainPanel.SetPositionDirect(x, y);
            MiniPanel.SetPositionDirect(-9999, 0);
            Recalculate();
        }

        private void SwitchToMini()
        {
            var fullDims = MainPanel.GetDimensions();
            float x = fullDims.X;
            float y = fullDims.Y;

            lastVisibleTopLeft = new Vector2(x, y);

            x = MathHelper.Clamp(x, 0, Main.screenWidth - MiniPlayerPanel.MiniWidth);
            y = MathHelper.Clamp(y, 0, Main.screenHeight - MiniPlayerPanel.MiniHeight);

            MainPanel.SetPositionDirect(-9999, 0);
            MiniPanel.SetPositionDirect(x, y);
            Recalculate();
        }

        private void HideAll()
        {
            // Save whichever panel is visible before hiding
            var miniDims = MiniPanel.GetDimensions();
            var fullDims = MainPanel.GetDimensions();

            if (miniDims.X > -1000)
                lastVisibleTopLeft = new Vector2(miniDims.X, miniDims.Y);
            else if (fullDims.X > -1000)
                lastVisibleTopLeft = new Vector2(fullDims.X, fullDims.Y);

            MainPanel.SetPositionDirect(-9999, 0);
            MiniPanel.SetPositionDirect(-9999, 0);
            Recalculate();
        }
    }
}
