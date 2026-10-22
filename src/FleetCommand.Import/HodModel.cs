using System.Buffers.Binary;
using System.Text;

namespace FleetCommand.Import;

public sealed record ImportedPart(string Name, int Material, float[] Positions, float[] Normals, float[] Uv, int[] Indices);
public sealed record ImportedTexture(string Name, string Format, int Width, int Height, string File);
public sealed record ImportedModel(int SchemaVersion, string SourceSha256, ImportedPart[] Parts, ImportedTexture[] Textures);

/// <summary>Static HOD 2 mesh reader. Animation, joints, original shaders and team-color masks are intentionally not reconstructed yet.</summary>
public static class HodModel
{
    private sealed record Chunk(string Id, int Version, byte[] Data);
    private static IEnumerable<Chunk> Chunks(byte[] bytes)
    {
        int p = 0;
        while (p < bytes.Length)
        {
            if (bytes.Length - p < 8) throw new InvalidDataException("Truncated HOD chunk.");
            string tag = Encoding.ASCII.GetString(bytes, p, 4);
            int size = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(p + 4)));
            if (size > bytes.Length - p - 8) size = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(p + 4)));
            if (size < 0 || size > bytes.Length - p - 8) throw new InvalidDataException("Invalid HOD chunk length.");
            int start = p + 8, skip = tag == "FORM" ? 4 : tag == "NRML" ? 8 : 0;
            if (size < skip) throw new InvalidDataException("Invalid HOD container.");
            string id = skip == 0 ? tag : Encoding.ASCII.GetString(bytes, start, 4);
            int version = tag == "NRML" ? BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(start + 4)) : 0;
            var data = bytes.AsSpan(start + skip, size - skip).ToArray();
            if (tag == "FORM" && id is "HVMD" or "MSHL")
                foreach (var child in Chunks(data)) yield return child;
            else yield return new(id, version, data);
            p += 8 + size;
        }
    }
    private static string Text(BinaryReader r)
    {
        int size = r.ReadInt32();
        if (size < 0 || size > 4096 || size > r.BaseStream.Length - r.BaseStream.Position) throw new InvalidDataException("Invalid HOD string.");
        return Encoding.UTF8.GetString(r.ReadBytes(size)).TrimEnd('\0');
    }
    private static byte[] Pool(BinaryReader r)
    {
        int stored = r.ReadInt32(), size = r.ReadInt32();
        if (stored < 0 || size < 0 || size > 256_000_000 || stored > r.BaseStream.Length - r.BaseStream.Position)
            throw new InvalidDataException("Invalid HOD pool size.");
        var source = r.ReadBytes(stored);
        return stored == size ? source : DecodePool(source, size);
    }

    // HOD uses 31 command bits followed by a sentinel. Decode one command at a time.
    internal static byte[] DecodePool(byte[] input, int size)
    {
        var output = new byte[size];
        int src = 0, dst = 0;
        uint flags = 1;
        uint ReadWord(int count)
        {
            if (src + count > input.Length) throw new InvalidDataException("Truncated compressed pool.");
            uint v = 0;
            for (int i = 0; i < count; i++) v |= (uint)input[src + i] << (8 * i);
            src += count; return v;
        }
        while (dst < size)
        {
            if (flags == 1) flags = ReadWord(4);
            if (flags == 0) throw new InvalidDataException("Invalid pool command word.");
            if ((flags & 1) == 0) output[dst++] = (byte)ReadWord(1);
            else
            {
                if (src >= input.Length) throw new InvalidDataException("Truncated pool match.");
                int type = input[src] & 7;
                int count = (type & 3) == 0 ? 1 : (type & 3) is 1 or 2 ? 2 : type == 3 ? 3 : 4;
                uint word = ReadWord(count);
                int distance = (int)(word >> (count == 1 || type is 1 or 5 ? 2 : type is 2 or 6 ? 6 : type == 3 ? 8 : 11));
                int length = type is 2 or 6 ? (int)((word >> 2) & 15) + 3 : type == 3 ? (int)((word >> 3) & 31) + 3 : type == 7 ? (int)((word >> 3) & 255) + 3 : 3;
                int origin = dst - distance;
                if (distance > dst || dst + length > size) throw new InvalidDataException("Invalid pool back reference.");
                for (int i = 0; i < length; i++)
                {
                    int sourceIndex = origin + i - (distance is > 0 and < 4 && i >= 3 ? 2 + (distance & 1) : 0);
                    output[dst++] = distance == 0 ? (byte)0 : sourceIndex < 0 ? throw new InvalidDataException("Invalid short match.") : output[sourceIndex];
                }
            }
            flags >>= 1;
        }
        return output;
    }

    public static ImportedModel Convert(byte[] input, string outputDirectory)
    {
        var chunks = Chunks(input).ToArray();
        var poolChunk = chunks.SingleOrDefault(c => c.Id == "POOL") ?? throw new NotSupportedException("This importer currently supports HOD 2 pooled meshes only.");
        using var poolReader = new BinaryReader(new MemoryStream(poolChunk.Data));
        poolReader.ReadUInt32();
        byte[] textures = Pool(poolReader), vertices = Pool(poolReader), faces = Pool(poolReader);
        Directory.CreateDirectory(outputDirectory);
        var textureList = new List<ImportedTexture>();
        int textureOffset = 0;
        foreach (var chunk in chunks.Where(c => c.Id == "LMIP"))
        {
            using var r = new BinaryReader(new MemoryStream(chunk.Data));
            string name = Text(r), format = Encoding.ASCII.GetString(r.ReadBytes(4));
            int mips = r.ReadInt32(), width = 0, height = 0, total = 0;
            if (mips is < 1 or > 20 || format is not ("DXT1" or "DXT3" or "DXT5")) throw new NotSupportedException("Unsupported texture format.");
            for (int i = 0; i < mips; i++)
            {
                int w = r.ReadInt32(), h = r.ReadInt32();
                if (w is < 1 or > 16384 || h is < 1 or > 16384) throw new InvalidDataException("Invalid texture dimensions.");
                if (i == 0) { width = w; height = h; }
                total = checked(total + Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * (format == "DXT1" ? 8 : 16));
            }
            if (textureOffset + total > textures.Length) throw new InvalidDataException("Texture pool exhausted.");
            string file = $"texture-{textureList.Count}.dds";
            using (var writer = new BinaryWriter(File.Create(Path.Combine(outputDirectory, file))))
            {
                writer.Write(Encoding.ASCII.GetBytes("DDS ")); writer.Write(124); writer.Write(0x00021007);
                writer.Write(height); writer.Write(width); writer.Write(total); writer.Write(0); writer.Write(mips);
                writer.Write(new byte[44]); writer.Write(32); writer.Write(4); writer.Write(Encoding.ASCII.GetBytes(format));
                writer.Write(new byte[20]); writer.Write(0x00401008); writer.Write(new byte[16]);
                writer.Write(textures, textureOffset, total);
            }
            textureOffset += total;
            textureList.Add(new(name, format, width, height, file));
        }
        var parts = new List<ImportedPart>();
        using var vr = new BinaryReader(new MemoryStream(vertices));
        using var fr = new BinaryReader(new MemoryStream(faces));
        void Mesh(Chunk chunk, string name)
        {
            using var r = new BinaryReader(new MemoryStream(chunk.Data));
            int lod = r.ReadInt32(), count = r.ReadInt32();
            if (count is < 0 or > 4096) throw new InvalidDataException("Invalid mesh part count.");
            for (int part = 0; part < count; part++)
            {
                int material = r.ReadInt32(); uint mask = r.ReadUInt32(); int n = r.ReadInt32();
                r.ReadInt16(); int ni = r.ReadInt32();
                if (n is < 0 or > 65536 || ni is < 0 or > 4_000_000 || (mask & ~0x603fu) != 0)
                    throw new NotSupportedException("Unsupported vertex layout.");
                var positions = new float[n * 3]; var normals = new float[n * 3]; var uv = new float[n * 2];
                for (int v = 0; v < n; v++)
                {
                    if ((mask & 1) != 0) { for (int j = 0; j < 3; j++) positions[v * 3 + j] = vr.ReadSingle(); vr.ReadSingle(); }
                    if ((mask & 2) != 0) { for (int j = 0; j < 3; j++) normals[v * 3 + j] = vr.ReadSingle(); vr.ReadSingle(); }
                    if ((mask & 4) != 0) vr.ReadUInt32();
                    if ((mask & 8) != 0) { uv[v * 2] = vr.ReadSingle(); uv[v * 2 + 1] = 1 - vr.ReadSingle(); }
                    if (chunk.Version == 1401 && (mask & 16) != 0) vr.ReadBytes(8);
                    if (chunk.Version == 1401 && (mask & 32) != 0) vr.ReadBytes(8);
                    if ((mask & 0x2000) != 0) vr.ReadBytes(12);
                    if ((mask & 0x4000) != 0) vr.ReadBytes(12);
                }
                var indices = new int[ni];
                for (int j = 0; j < ni; j++) { indices[j] = fr.ReadUInt16(); if (indices[j] >= n) throw new InvalidDataException("Invalid triangle index."); }
                if (ni % 3 != 0 || positions.Any(v => !float.IsFinite(v)) || normals.Any(v => !float.IsFinite(v)))
                    throw new InvalidDataException("Invalid mesh geometry.");
                if (lod == 0 && n > 0 && ni > 0) parts.Add(new(name, material, positions, normals, uv, indices));
            }
        }
        foreach (var chunk in chunks)
        {
            if (chunk.Id == "BMSH") Mesh(chunk, "Root");
            else if (chunk.Id is "MULT" or "GOBG")
            {
                using var r = new BinaryReader(new MemoryStream(chunk.Data));
                string name = Text(r); Text(r); r.ReadInt32();
                foreach (var child in Chunks(r.ReadBytes((int)(r.BaseStream.Length - r.BaseStream.Position))))
                    if (child.Id == "BMSH") Mesh(child, name);
            }
        }
        if (parts.Count == 0) throw new InvalidDataException("No supported LOD0 geometry found.");
        return new(1, System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input)), [.. parts], [.. textureList]);
    }
}
