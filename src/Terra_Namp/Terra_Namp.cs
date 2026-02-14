using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terra_Namp.Core.Audio;
using Terra_Namp.Core.UI;
using Terra_Namp.Networking;
using ReLogic.Content;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terra_Namp;

public class Terra_Namp : Mod
{
    public const string Silence = "Assets/Sounds/Silence";

    public static readonly string CachePath = Path.Combine(Main.SavePath, "MP3PlayerCache");

    public static ModKeybind MusicPlayerBind { get; private set; }

    public static ModKeybind SoundpadBind { get; private set; }

    public static ModKeybind VolumeUpBind { get; private set; }

    public static ModKeybind VolumeDownBind { get; private set; }

    public static SpriteFont Font { get; private set; }

    public static Terra_Namp Instance { get; private set; }

    public static List<string> Bosses { get; private set; }

    public Terra_Namp()
    {
        Instance = this;
        Bosses = [];
    }

    public override void Load()
    {
        Directory.CreateDirectory(CachePath);
        Directory.CreateDirectory(Path.Combine(CachePath, "soundpad"));

        // Migrate old tracks to new folder structure (backward compatibility)
        MigrateLegacyTracks();

        if (!Main.dedServ)
        {
            FFmpeg.Initialise(this);

            MusicPlayerBind = KeybindLoader.RegisterKeybind(this, "OpenMusicPlayer", "K");
            SoundpadBind = KeybindLoader.RegisterKeybind(this, "OpenSoundpad", "L");
            VolumeUpBind = KeybindLoader.RegisterKeybind(this, "VolumeUp", "None");
            VolumeDownBind = KeybindLoader.RegisterKeybind(this, "VolumeDown", "None");

            Font = Assets.Request<SpriteFont>("Assets/Fonts/TerraNamp", AssetRequestMode.ImmediateLoad).Value;

            EmojiRenderer.Load(this);

            MusicLoader.AddMusic(this, Silence);
        }
    }

    private void MigrateLegacyTracks()
    {
        if (!Directory.Exists(CachePath)) return;

        int migratedCount = 0;

        foreach (string file in Directory.GetFiles(CachePath, "*.txt"))
        {
            try
            {
                string[] lines = File.ReadAllLines(file);

                // If file has less than 4 lines, it's a legacy track without folder info
                if (lines.Length < 4)
                {
                    // Determine folder based on author field:
                    // If author contains "Added by user" -> Singles (local import)
                    // Otherwise -> Downloads (YouTube)
                    string author = lines.Length > 1 ? lines[1] : "";
                    string folder = author.Contains("Added by user") ? "Singles" : "Downloads";

                    File.AppendAllText(file, System.Environment.NewLine + folder);
                    migratedCount++;
                }
            }
            catch (System.Exception ex)
            {
                Logger.Warn($"Failed to migrate track {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        if (migratedCount > 0)
            Logger.Info($"Migrated {migratedCount} legacy tracks to new folder structure");
    }

    public override void Unload()
    {
        AsyncDownloader.CancelAll();

        if (!Main.dedServ)
        {
            Content.UI.TerraUI.BlurHelper.Unload();
        }
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        PacketRouter.HandlePacket(reader, whoAmI);
    }

    public override void PostSetupContent()
    {
        PopulateBossList();
    }

    private void PopulateBossList()
    {
        for (int i = 0; i < NPCLoader.NPCCount; i++)
        {
            NPC npc = new();
            npc.SetDefaults(i);

            if (npc.boss)
            {
                // For modded bosses the type name needs to be stored, but for vanilla bosses they don't have an associated class so the numerical ID must be stored.
                if (npc.ModNPC is not null)
                {
                    Bosses.Add(npc.ModNPC.GetType().FullName);
                }
                else
                {
                    Bosses.Add(i.ToString());
                }
            }
        }

        Bosses.Remove($"{NPCID.MoonLordHead}");
        Bosses.Remove($"{NPCID.MoonLordHand}");

        Bosses.Insert(1, NPCID.EaterofWorldsHead.ToString());

        // Attempts to remove duplicate boss names caused by worm segments.
        List<string> removeBuffer = new();

        for (int i = 0; i < Bosses.Count; i++)
        {
            string boss = Bosses[i];

            if (boss.EndsWith("Head"))
            {
                // Remove last 4 characters.
                string prefixName = boss[..^4];
                    
                foreach (string checkBoss in Bosses)
                {
                    if (checkBoss.Contains(prefixName) && !checkBoss.Contains("Head"))
                    {
                        removeBuffer.Add(checkBoss);
                    }
                }
            }
        }

        foreach (string typeName in removeBuffer)
        {
            Bosses.Remove(typeName);
        }
    }
}