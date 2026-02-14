# Configuration

## build.txt

```
displayName = Terra Namp
author = Baguletti
version = 1.0
side = Both
```

`side = Both` loads on client + server. Server code guarded by `Main.dedServ`.

## Terra_NampConfig (ModConfig)

`ConfigScope.ClientSide`, each client independent.

| Setting | Type | Default |
|---------|------|---------|
| `SendNowPlayingMessages` | bool | true |
| `EnablePrefetch` | bool | true |

## Keybind

```csharp
KeybindLoader.RegisterKeybind(this, "OpenMusicPlayer", "K");
```

Toggles `TerraState.Visible` + plays menu open/close sound.

