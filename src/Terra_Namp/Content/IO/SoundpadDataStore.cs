using Terra_Namp.Core.IO;
using System.Collections.Generic;
using System.IO;
using Terraria.ModLoader.IO;

namespace Terra_Namp.Content.IO;

public class SoundpadDataStore : PersistentDataStore
{
    private const string SoundCountTag = "Soundpad:Count";
    private const string SoundUuidPrefix = "Soundpad:Uuid:";
    private const string SoundNamePrefix = "Soundpad:Name:";
    private const string VolumeLevelTag = "Soundpad:VolumeLevel";
    // Boss/death sound UUIDs intentionally NOT persisted — session-only

    public List<SoundEntry> Sounds { get; set; } = new();
    public float VolumeLevel { get; set; } = 0.5f;

    public string BossSoundUuid { get; set; } = "";
    public string DeathSoundUuid { get; set; } = "";

    public override string FileName => "soundpad_data.dat";

    public static string SoundpadCachePath => Path.Combine(Terra_Namp.CachePath, "soundpad");

    public override void LoadGlobal(TagCompound tag)
    {
        Sounds.Clear();

        // Load volume
        if (tag.ContainsKey(VolumeLevelTag))
            VolumeLevel = tag.GetFloat(VolumeLevelTag);
        else
            VolumeLevel = 0.5f; // Default 50%

        // BossSoundUuid / DeathSoundUuid not loaded — session-only

        if (!tag.ContainsKey(SoundCountTag))
            return;

        int count = tag.GetInt(SoundCountTag);
        for (int i = 0; i < count; i++)
        {
            string uuidKey = SoundUuidPrefix + i;
            string nameKey = SoundNamePrefix + i;

            if (tag.ContainsKey(uuidKey) && tag.ContainsKey(nameKey))
            {
                Sounds.Add(new SoundEntry
                {
                    Uuid = tag.GetString(uuidKey),
                    DisplayName = tag.GetString(nameKey)
                });
            }
        }
    }

    public override void SaveGlobal(TagCompound tag)
    {
        tag[VolumeLevelTag] = VolumeLevel;
        // BossSoundUuid / DeathSoundUuid not saved — session-only
        tag[SoundCountTag] = Sounds.Count;

        for (int i = 0; i < Sounds.Count; i++)
        {
            tag[SoundUuidPrefix + i] = Sounds[i].Uuid;
            tag[SoundNamePrefix + i] = Sounds[i].DisplayName;
        }
    }

    public void AddSound(string uuid, string displayName)
    {
        Sounds.Add(new SoundEntry { Uuid = uuid, DisplayName = displayName });
        ForceSave();
    }

    public void RemoveSound(string uuid)
    {
        Sounds.RemoveAll(s => s.Uuid == uuid);
        ForceSave();

        string filePath = Path.Combine(SoundpadCachePath, $"{uuid}.mp3");
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch (IOException)
        {
            // File locked by playback — ignore, orphan will be overwritten or cleaned up
        }
    }

    public class SoundEntry
    {
        public string Uuid { get; set; }
        public string DisplayName { get; set; }
    }
}
