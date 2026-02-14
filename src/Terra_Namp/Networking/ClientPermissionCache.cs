using System;
using System.Collections.Generic;
using Terraria;

namespace Terra_Namp.Networking;

public static class ClientPermissionCache
{
    public static Dictionary<int, PlayerPermissions> Permissions { get; } = new();
    public static HashSet<int> SuperUsers { get; } = new();

    public static event Action OnPermissionsChanged;

    public static void Update(Dictionary<int, PlayerPermissions> perms, HashSet<int> superUsers)
    {
        Permissions.Clear();
        foreach (var (key, value) in perms)
            Permissions[key] = value;

        SuperUsers.Clear();
        foreach (int idx in superUsers)
            SuperUsers.Add(idx);

        OnPermissionsChanged?.Invoke();
    }

    public static bool IsLocalPlayerAdmin()
    {
        return Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient
            && Permissions.TryGetValue(Main.myPlayer, out var p)
            && p.CanManage;
    }

    public static bool IsSuperUser(int playerIndex) => SuperUsers.Contains(playerIndex);

    public static PlayerPermissions GetLocalPermissions()
    {
        return Permissions.TryGetValue(Main.myPlayer, out var p) ? p : PlayerPermissions.Default;
    }

    public static void Clear()
    {
        Permissions.Clear();
        SuperUsers.Clear();
        OnPermissionsChanged?.Invoke();
    }
}
