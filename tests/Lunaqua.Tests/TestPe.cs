namespace Lunaqua.Tests;

/// <summary>造一个只有 PE 头的最小可执行文件字节。</summary>
internal static class TestPe
{
    public const ushort MachineAmd64 = 0x8664;

    public const ushort MachineI386 = 0x014C;

    public static byte[] Create(ushort machine)
    {
        var bytes = new byte[0x100];
        bytes[0] = 0x4D;   // M
        bytes[1] = 0x5A;   // Z
        BitConverter.GetBytes(0x80).CopyTo(bytes, 0x3C);
        BitConverter.GetBytes(0x00004550u).CopyTo(bytes, 0x80);   // PE\0\0
        BitConverter.GetBytes(machine).CopyTo(bytes, 0x84);
        return bytes;
    }
}
