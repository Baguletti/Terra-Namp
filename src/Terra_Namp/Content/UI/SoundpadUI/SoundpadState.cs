using Terra_Namp.Common.UI.Abstract;
using Terra_Namp.Content.UI.TerraUI;
using System.Collections.Generic;
using Terraria.UI;

namespace Terra_Namp.Content.UI.SoundpadUI;

public class SoundpadState : SmartUIState
{
    public SoundpadPopup Popup { get; private set; }

    public override int InsertionIndex(List<GameInterfaceLayer> layers) =>
        layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));

    public override void OnInitialize()
    {
        Popup = new SoundpadPopup();
        Popup.Width.Set(SoundpadPopup.PopupWidth, 0);
        Popup.Height.Set(SoundpadPopup.PopupHeight, 0);
        Append(Popup);
        Popup.Activate();
        Popup.Recalculate();
    }

    public void SetPlaybackController(SoundpadPlaybackController controller)
    {
        Popup?.SetPlaybackController(controller);
    }
}
