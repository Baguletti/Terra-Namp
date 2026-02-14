using System;
using System.IO;
using System.Security.Cryptography;

namespace Terra_Namp.Networking;

public static class ContentHash
{
    public static byte[] ComputeHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var md5 = MD5.Create();
        return md5.ComputeHash(stream);
    }

    public static string HashToHex(byte[] hash)
        => BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

    public static byte[] HexToHash(string hex)
    {
        byte[] hash = new byte[16];
        for (int i = 0; i < 16; i++)
            hash[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return hash;
    }
}
