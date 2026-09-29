using System;
using System.Collections.Generic;
using System.IO;

namespace Rage2Toolkit
{
    // ============================================================
    // TabFormat - parser + serializer TAB 3.1
    // ============================================================
    static class TabFormat
    {
        public const uint TAB_MAGIC = 0x00424154;
        public const long ALIGN = 0x1000;
        public const byte PAD = 0x30;

        public class Block { public uint CSize, USize; public bool Sentinel; }

        public class Entry
        {
            public ulong Hash;
            public uint Offset, CSize, USize;
            public ushort BIdx;
            public byte CType, CFlags;
            public Entry Clone() { return (Entry)MemberwiseClone(); }
        }

        public class Tab
        {
            public ushort Major, Minor;
            public uint Alignment, FileCount, BlockCount, Padding, MaxCompressedBlockSize, UncompressedBlockSize;
            public List<Block> Blocks = new List<Block>();
            public List<Entry> Entries = new List<Entry>();

            public static Tab Parse(string path)
            {
                byte[] d = File.ReadAllBytes(path);
                if (d.Length < 0x20) throw new Exception("TAB demasiado corto");
                if (BitConverter.ToUInt32(d, 0) != TAB_MAGIC) throw new Exception("No es un TAB");

                Tab t = new Tab();
                t.Major = BitConverter.ToUInt16(d, 4);
                t.Minor = BitConverter.ToUInt16(d, 6);
                t.Alignment = BitConverter.ToUInt32(d, 8);
                t.FileCount = BitConverter.ToUInt32(d, 0x0C);
                t.BlockCount = BitConverter.ToUInt32(d, 0x10);
                t.Padding = BitConverter.ToUInt32(d, 0x14);
                t.MaxCompressedBlockSize = BitConverter.ToUInt32(d, 0x18);
                t.UncompressedBlockSize = BitConverter.ToUInt32(d, 0x1C);

                int off = 0x20;
                for (int i = 0; i < t.BlockCount; i++)
                {
                    Block b = new Block();
                    b.CSize = BitConverter.ToUInt32(d, off);
                    b.USize = BitConverter.ToUInt32(d, off + 4);
                    b.Sentinel = (b.CSize == 0xFFFFFFFF && b.USize == 0xFFFFFFFF);
                    t.Blocks.Add(b);
                    off += 8;
                }
                for (int i = 0; i < t.FileCount; i++)
                {
                    Entry e = new Entry();
                    e.Hash = BitConverter.ToUInt64(d, off);
                    e.Offset = BitConverter.ToUInt32(d, off + 8);
                    e.CSize = BitConverter.ToUInt32(d, off + 12);
                    e.USize = BitConverter.ToUInt32(d, off + 16);
                    e.BIdx = BitConverter.ToUInt16(d, off + 20);
                    e.CType = d[off + 22];
                    e.CFlags = d[off + 23];
                    t.Entries.Add(e);
                    off += 24;
                }
                return t;
            }

            public byte[] Serialize()
            {
                int total = 0x20 + (int)BlockCount * 8 + (int)FileCount * 24;
                byte[] d = new byte[total];
                Array.Copy(BitConverter.GetBytes(TAB_MAGIC), 0, d, 0, 4);
                Array.Copy(BitConverter.GetBytes(Major), 0, d, 4, 2);
                Array.Copy(BitConverter.GetBytes(Minor), 0, d, 6, 2);
                Array.Copy(BitConverter.GetBytes(Alignment), 0, d, 8, 4);
                Array.Copy(BitConverter.GetBytes(FileCount), 0, d, 0x0C, 4);
                Array.Copy(BitConverter.GetBytes(BlockCount), 0, d, 0x10, 4);
                Array.Copy(BitConverter.GetBytes(Padding), 0, d, 0x14, 4);
                Array.Copy(BitConverter.GetBytes(MaxCompressedBlockSize), 0, d, 0x18, 4);
                Array.Copy(BitConverter.GetBytes(UncompressedBlockSize), 0, d, 0x1C, 4);

                int off = 0x20;
                foreach (var b in Blocks)
                {
                    Array.Copy(BitConverter.GetBytes(b.CSize), 0, d, off, 4);
                    Array.Copy(BitConverter.GetBytes(b.USize), 0, d, off + 4, 4);
                    off += 8;
                }
                foreach (var e in Entries)
                {
                    Array.Copy(BitConverter.GetBytes(e.Hash), 0, d, off, 8);
                    Array.Copy(BitConverter.GetBytes(e.Offset), 0, d, off + 8, 4);
                    Array.Copy(BitConverter.GetBytes(e.CSize), 0, d, off + 12, 4);
                    Array.Copy(BitConverter.GetBytes(e.USize), 0, d, off + 16, 4);
                    Array.Copy(BitConverter.GetBytes(e.BIdx), 0, d, off + 20, 2);
                    d[off + 22] = e.CType;
                    d[off + 23] = e.CFlags;
                    off += 24;
                }
                return d;
            }
        }
    }
}
