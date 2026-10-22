using System.IO.Compression;
using System.Text;

namespace FleetCommand.Import;

public sealed record BigEntry(string Path, long Offset, int StoredSize, int Size, byte Flags);

/// <summary>Read-only parser for the documented _ARCHIVE version 2 container. No game tools or keys are bundled.</summary>
public sealed class BigArchive : IDisposable
{
    private readonly FileStream _stream;
    public IReadOnlyList<BigEntry> Entries { get; }
    public BigArchive(string path)
    {
        _stream = File.OpenRead(path);
        try
        {
            using var r = new BinaryReader(_stream, Encoding.UTF8, true);
            if (Encoding.ASCII.GetString(r.ReadBytes(8)) != "_ARCHIVE" || r.ReadUInt32() != 2)
                throw new InvalidDataException("Only unencrypted _ARCHIVE version 2 archives are supported.");
            _stream.Position = 172;
            int tableSize = checked((int)r.ReadUInt32());
            long dataOffset = r.ReadUInt32();
            if (tableSize is < 24 or > 32_000_000 || dataOffset > _stream.Length || 180L + tableSize > _stream.Length)
                throw new InvalidDataException("Invalid archive table.");
            var table = r.ReadBytes(tableSize);
            using var tr = new BinaryReader(new MemoryStream(table));
            var sections = new (int Offset, int Count)[4];
            for (int i = 0; i < 4; i++) sections[i] = (checked((int)tr.ReadUInt32()), tr.ReadUInt16());
            string Name(int offset)
            {
                int p = checked(sections[3].Offset + offset);
                if (p < 0 || p >= table.Length) throw new InvalidDataException("Invalid filename offset.");
                int end = Array.IndexOf(table, (byte)0, p);
                if (end < 0 || end - p > 4096) throw new InvalidDataException("Invalid filename.");
                return Encoding.UTF8.GetString(table, p, end - p).Replace('\\', '/');
            }
            var files = new (string Name, byte Flag, uint Offset, int Stored, int Size)[sections[2].Count];
            for (int i = 0; i < files.Length; i++)
            {
                tr.BaseStream.Position = checked(sections[2].Offset + i * 17);
                files[i] = (Name(checked((int)tr.ReadUInt32())), tr.ReadByte(), tr.ReadUInt32(), checked((int)tr.ReadUInt32()), checked((int)tr.ReadUInt32()));
            }
            var entries = new List<BigEntry>();
            for (int i = 0; i < sections[1].Count; i++)
            {
                tr.BaseStream.Position = checked(sections[1].Offset + i * 12);
                var folder = Name(checked((int)tr.ReadUInt32()));
                tr.ReadUInt16(); tr.ReadUInt16();
                int first = tr.ReadUInt16(), last = tr.ReadUInt16();
                if (first > last || last > files.Length) throw new InvalidDataException("Invalid directory file range.");
                for (int j = first; j < last; j++)
                {
                    var f = files[j];
                    var entryPath = string.IsNullOrEmpty(folder) ? f.Name : folder + "/" + f.Name;
                    if (entryPath.Split('/').Any(p => p is ".." or ".") || entryPath.Contains(':') || entryPath.StartsWith('/'))
                        throw new InvalidDataException("Unsafe archive path.");
                    if (f.Stored < 0 || f.Size < 0 || dataOffset + f.Offset + f.Stored > _stream.Length || f.Size > 256_000_000)
                        throw new InvalidDataException("Invalid archive entry size.");
                    entries.Add(new(entryPath, dataOffset + f.Offset, f.Stored, f.Size, f.Flag));
                }
            }
            Entries = entries;
        }
        catch { _stream.Dispose(); throw; }
    }
    public byte[] Read(BigEntry entry)
    {
        if (!Entries.Contains(entry)) throw new ArgumentException("Entry is not from this archive.");
        _stream.Position = entry.Offset;
        var stored = new byte[entry.StoredSize];
        _stream.ReadExactly(stored);
        if (entry.Flags == 0)
        {
            if (stored.Length != entry.Size) throw new InvalidDataException("Invalid raw entry size.");
            return stored;
        }
        if (entry.Flags is not (0x10 or 0x20)) throw new InvalidDataException("Unknown archive compression.");
        using var z = new ZLibStream(new MemoryStream(stored), CompressionMode.Decompress);
        var result = new byte[entry.Size];
        z.ReadExactly(result);
        if (z.ReadByte() != -1) throw new InvalidDataException("Archive entry exceeds declared size.");
        return result;
    }
    public void Dispose() => _stream.Dispose();
}
