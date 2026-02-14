using Microsoft.Xna.Framework;
using Terraria;

namespace Terra_Namp.Networking;

public static class NetLogger
{
    private static readonly Color ColorPacket = new(100, 200, 255);    // Light blue
    private static readonly Color ColorTransfer = new(255, 200, 100);  // Orange
    private static readonly Color ColorState = new(100, 255, 150);     // Green
    private static readonly Color ColorError = new(255, 80, 80);       // Red
    private static readonly Color ColorInfo = new(200, 200, 200);      // Gray

    public static void Packet(string msg)   => Log($"[NET:PKT] {msg}", ColorPacket);
    public static void Transfer(string msg) => Log($"[NET:TRF] {msg}", ColorTransfer);
    public static void State(string msg)    => Log($"[NET:STA] {msg}", ColorState);
    public static void Error(string msg)    => Log($"[NET:ERR] {msg}", ColorError);
    public static void Info(string msg)     => Log($"[NET:INF] {msg}", ColorInfo);

    private static void Log(string msg, Color color)
    {
        if (Main.dedServ)
            System.Console.WriteLine(msg);

        Terra_Namp.Instance?.Logger.Info(msg);
    }
}
