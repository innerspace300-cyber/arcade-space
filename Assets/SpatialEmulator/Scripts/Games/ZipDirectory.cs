// ZipDirectory.cs — lists the files in a zip and their CRC32s by reading
// only the zip's central directory (a few KB at the end of the file), so a
// romset can be audited without decompressing anything. MAME matches ROMs by
// CRC too, which is why this is enough to tell whether a romset will load.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SpatialEmulator.Games
{
    public static class ZipDirectory
    {
        public struct Entry
        {
            public string name;
            public uint crc;
            public long size;
        }

        const uint EndOfCentralDirSig = 0x06054b50;
        const uint CentralFileHeaderSig = 0x02014b50;
        const int EndRecordSize = 22;
        const int MaxCommentSize = 0xFFFF;

        /// The files in the zip, or null if it isn't a readable zip.
        public static List<Entry> Read(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var reader = new BinaryReader(stream);
                long length = stream.Length;
                if (length < EndRecordSize) return null;

                // The end record sits in the last 22 bytes plus an optional comment.
                int tail = (int)Math.Min(length, EndRecordSize + MaxCommentSize);
                stream.Seek(length - tail, SeekOrigin.Begin);
                byte[] buffer = reader.ReadBytes(tail);
                int end = -1;
                for (int i = buffer.Length - EndRecordSize; i >= 0; i--)
                    if (BitConverter.ToUInt32(buffer, i) == EndOfCentralDirSig) { end = i; break; }
                if (end < 0) return null;

                int count = BitConverter.ToUInt16(buffer, end + 10);
                uint dirOffset = BitConverter.ToUInt32(buffer, end + 16);
                if (count == 0xFFFF || dirOffset == 0xFFFFFFFF) return null; // Zip64: not used by romsets

                var entries = new List<Entry>(count);
                stream.Seek(dirOffset, SeekOrigin.Begin);
                for (int i = 0; i < count; i++)
                {
                    if (reader.ReadUInt32() != CentralFileHeaderSig) return null;
                    stream.Seek(12, SeekOrigin.Current);      // versions, flags, method, time, date
                    uint crc = reader.ReadUInt32();
                    reader.ReadUInt32();                       // compressed size
                    uint size = reader.ReadUInt32();
                    int nameLength = reader.ReadUInt16();
                    int extraLength = reader.ReadUInt16();
                    int commentLength = reader.ReadUInt16();
                    stream.Seek(12, SeekOrigin.Current);      // disk, attributes, local header offset
                    string name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                    stream.Seek(extraLength + commentLength, SeekOrigin.Current);
                    if (!name.EndsWith("/", StringComparison.Ordinal))
                        entries.Add(new Entry { name = name, crc = crc, size = size });
                }
                return entries;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
