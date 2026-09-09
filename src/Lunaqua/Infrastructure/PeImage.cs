namespace Lunaqua.Infrastructure;

/// <summary>可执行文件位数。</summary>
public enum PeArchitecture
{
    Unknown,

    /// <summary>32 位。</summary>
    X86,

    /// <summary>64 位。</summary>
    X64,
}

/// <summary>只读 PE 头，判断游戏是不是 64 位（规格书 §8 第 1 步）。</summary>
public static class PeImage
{
    public static PeArchitecture ReadArchitecture(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            if (stream.Length < 0x40 || reader.ReadUInt16() != 0x5A4D)   // MZ
            {
                return PeArchitecture.Unknown;
            }

            stream.Position = 0x3C;
            var peOffset = reader.ReadInt32();
            if (peOffset <= 0 || peOffset + 0x18 > stream.Length)
            {
                return PeArchitecture.Unknown;
            }

            stream.Position = peOffset;
            if (reader.ReadUInt32() != 0x00004550)   // "PE\0\0"
            {
                return PeArchitecture.Unknown;
            }

            var machine = reader.ReadUInt16();
            return machine switch
            {
                0x8664 => PeArchitecture.X64,   // AMD64
                0x014C => PeArchitecture.X86,   // I386
                _ => PeArchitecture.Unknown,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PeArchitecture.Unknown;
        }
    }

    public static bool IsX64(string path) => ReadArchitecture(path) == PeArchitecture.X64;
}
