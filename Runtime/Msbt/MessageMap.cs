using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DivineDragon.Msbt
{
    /// <summary>
    /// Minimal MSBT reader modeled after the Rust MessageMap struct. It keeps entries in raw UTF-16 code units
    /// so control codes survive round-trips until a higher-level script formatter is wired up.
    /// </summary>
    public sealed class MessageMap
    {
        private readonly Dictionary<string, ushort[]> _messages;
        private int _bucketCount;

        private MessageMap(int bucketCount, Dictionary<string, ushort[]> messages)
        {
            _bucketCount = bucketCount;
            _messages = messages;
        }

        public int BucketCount => _bucketCount;

        public IReadOnlyDictionary<string, ushort[]> Messages => _messages;

        public static MessageMap Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path must not be empty.", nameof(path));
            }

            using var stream = File.OpenRead(path);
            return FromStream(stream);
        }

        public static MessageMap FromBytes(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            using var stream = new MemoryStream(data, writable: false);
            return FromStream(stream);
        }

        public static MessageMap FromStream(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanSeek)
            {
                throw new ArgumentException("Stream must support seeking.", nameof(stream));
            }

            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            var magic = ReadUtf8(reader, 8);
            if (!string.Equals(magic, "MsgStdBn", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unexpected MSBT magic '{magic}'.");
            }

            // Skip to first section (standard MSBT header is 0x20 bytes).
            reader.BaseStream.Seek(0x20, SeekOrigin.Begin);

            var labelGroups = ParseLbl1(reader);
            SkipAtr1(reader);
            var txt2 = ParseTxt2(reader);

            var labels = labelGroups.SelectMany(group => group).OrderBy(pair => pair.Id).ToList();
            var result = new Dictionary<string, ushort[]>(labels.Count, StringComparer.Ordinal);

            foreach (var label in labels)
            {
                var name = label.Label;
                var id = label.Id;
                if (id < 0 || id >= txt2.Count)
                {
                    throw new InvalidDataException($"Label '{name}' points to invalid text index {id}.");
                }

                result[name] = txt2[(int)id];
            }

            return new MessageMap(labelGroups.Count, result);
        }

        public void ReplaceMessages(IDictionary<string, ushort[]> newMessages)
        {
            if (newMessages == null)
            {
                throw new ArgumentNullException(nameof(newMessages));
            }

            _messages.Clear();
            foreach (var entry in newMessages)
            {
                _messages[entry.Key] = entry.Value.ToArray();
            }
            _bucketCount = 0;
        }

        public byte[] Serialize()
        {
            return _bucketCount == 0 ? RehashAndSerialize() : SerializeWithBucketCount(_bucketCount);
        }

        public byte[] RehashAndSerialize()
        {
            var bucketCount = _messages.Count == 0 ? 0 : _messages.Count / 2 + 1;
            return SerializeWithBucketCount(bucketCount);
        }

        public void Save(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path must not be empty.", nameof(path));
            }

            File.WriteAllBytes(path, Serialize());
        }

        public void RehashAndSave(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path must not be empty.", nameof(path));
            }

            File.WriteAllBytes(path, RehashAndSerialize());
        }

        private byte[] SerializeWithBucketCount(int bucketCount)
        {
            var lbl1 = SerializeLbl1(_messages, bucketCount);
            var atr1 = SerializeAtr1(_messages.Count);
            var txt2 = SerializeTxt2(_messages);
            var fileLength = lbl1.Length + atr1.Length + txt2.Length + 0x20;
            var buffer = new byte[fileLength];
            using var stream = new MemoryStream(buffer);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            WriteUtf8(writer, "MsgStdBn");
            writer.Write((byte)0xFF);
            writer.Write((byte)0xFE);
            writer.Write((ushort)0);
            writer.Write((byte)0x01);
            writer.Write((byte)0x03);
            writer.Write((byte)0x03);
            writer.Write((byte)0x00);
            writer.Write((uint)fileLength);
            while (writer.BaseStream.Position < 0x20)
            {
                writer.Write((byte)0);
            }

            writer.Write(lbl1);
            writer.Write(atr1);
            writer.Write(txt2);
            return buffer;
        }

        private static byte[] SerializeLbl1(IReadOnlyDictionary<string, ushort[]> messages, int bucketCount)
        {
            if (bucketCount == 0 && messages.Count > 0)
            {
                throw new InvalidOperationException("Cannot serialize entries without buckets.");
            }

            var orderedLabels = messages.Keys.ToList();
            var buckets = new List<List<(string Label, uint Id)>>(bucketCount);
            for (var i = 0; i < bucketCount; i++)
            {
                buckets.Add(new List<(string, uint)>());
            }
            if (bucketCount > 0)
            {
                for (var i = 0; i < orderedLabels.Count; i++)
                {
                    var label = orderedLabels[i];
                    var bucketIndex = HashLabel(label, bucketCount);
                    buckets[bucketIndex].Add((label, (uint)i));
                }
            }

            var basePosition = bucketCount * 8 + 4;
            var rawText = new List<byte>();
            var bucketInfo = new List<(int Length, int Offset)>();
            foreach (var bucket in buckets)
            {
                bucketInfo.Add((bucket.Count, basePosition + rawText.Count));
                foreach (var (label, id) in bucket)
                {
                    var labelBytes = Encoding.UTF8.GetBytes(label);
                    if (labelBytes.Length > byte.MaxValue)
                    {
                        throw new InvalidOperationException("Label too long for MSBT.");
                    }
                    rawText.Add((byte)labelBytes.Length);
                    rawText.AddRange(labelBytes);
                    rawText.AddRange(BitConverter.GetBytes(id));
                }
            }

            var lengthWithoutHeader = 4 + bucketInfo.Count * 8 + rawText.Count;
            var paddedLength = Align(lengthWithoutHeader + 0x10, 0x10);
            var buffer = new byte[paddedLength];
            using var stream = new MemoryStream(buffer);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            WriteUtf8(writer, "LBL1");
            writer.Write((uint)lengthWithoutHeader);
            writer.Write(new byte[8]);
            writer.Write((uint)bucketInfo.Count);
            foreach (var (length, offset) in bucketInfo)
            {
                writer.Write((uint)length);
                writer.Write((uint)offset);
            }
            writer.Write(rawText.ToArray());
            PadWith(writer, paddedLength);
            return buffer;
        }

        private static byte[] SerializeAtr1(int entryCount)
        {
            var lengthWithoutHeader = entryCount + 8;
            var paddedLength = Align(lengthWithoutHeader + 0x10, 0x10);
            var buffer = new byte[paddedLength];
            using var stream = new MemoryStream(buffer);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            WriteUtf8(writer, "ATR1");
            writer.Write((uint)lengthWithoutHeader);
            writer.Write(new byte[8]);
            writer.Write((uint)entryCount);
            writer.Write(1u);
            writer.BaseStream.Position += entryCount;
            PadWith(writer, paddedLength);
            return buffer;
        }

        private static byte[] SerializeTxt2(IReadOnlyDictionary<string, ushort[]> messages)
        {
            var orderedValues = messages.Values.ToList();
            var basePosition = orderedValues.Count * 4 + 4;
            var textOffsets = new List<int>(orderedValues.Count);
            var rawText = new List<byte>();
            foreach (var message in orderedValues)
            {
                textOffsets.Add(basePosition + rawText.Count);
                foreach (var unit in message)
                {
                    rawText.Add((byte)(unit & 0xFF));
                    rawText.Add((byte)(unit >> 8));
                }
            }

            var lengthWithoutHeader = orderedValues.Count * 4 + rawText.Count + 4;
            var paddedLength = Align(lengthWithoutHeader + 0x10, 0x10);
            var buffer = new byte[paddedLength];
            using var stream = new MemoryStream(buffer);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            WriteUtf8(writer, "TXT2");
            writer.Write((uint)lengthWithoutHeader);
            writer.Write(new byte[8]);
            writer.Write((uint)orderedValues.Count);
            foreach (var offset in textOffsets)
            {
                writer.Write((uint)offset);
            }
            writer.Write(rawText.ToArray());
            PadWith(writer, paddedLength);
            return buffer;
        }

        private static int HashLabel(string label, int bucketCount)
        {
            var sum = 0u;
            foreach (var b in Encoding.UTF8.GetBytes(label))
            {
                sum = unchecked(sum * 0x492u);
                sum = unchecked(sum + b);
            }
            return (int)(sum % (uint)bucketCount);
        }

        private static void PadWith(BinaryWriter writer, long targetPosition)
        {
            while (writer.BaseStream.Position < targetPosition)
            {
                writer.Write((byte)0xAB);
            }
        }

        private static void WriteUtf8(BinaryWriter writer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes);
        }

        private static List<List<(string Label, uint Id)>> ParseLbl1(BinaryReader reader)
        {
            var magic = ReadUtf8(reader, 4);
            if (!string.Equals(magic, "LBL1", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Expected LBL1 section, found '{magic}'.");
            }

            var sectionLength = reader.ReadUInt32();
            reader.BaseStream.Position += 8; // Skip padding/flags.
            var basePosition = reader.BaseStream.Position;

            var bucketCount = reader.ReadUInt32();
            var groups = new List<List<(string Label, uint Id)>>((int)bucketCount);

            for (var i = 0; i < bucketCount; i++)
            {
                groups.Add(ParseLabelGroup(reader, basePosition));
            }

            reader.BaseStream.Position = Align(basePosition + sectionLength, 0x10);
            return groups;
        }

        private static List<(string Label, uint Id)> ParseLabelGroup(BinaryReader reader, long basePosition)
        {
            var count = reader.ReadUInt32();
            var offset = reader.ReadUInt32();
            var returnPosition = reader.BaseStream.Position;
            reader.BaseStream.Position = basePosition + offset;

            var entries = new List<(string Label, uint Id)>((int)count);
            for (var i = 0; i < count; i++)
            {
                var nameLength = reader.ReadByte();
                var name = ReadUtf8(reader, nameLength);
                var id = reader.ReadUInt32();
                entries.Add((name, id));
            }

            reader.BaseStream.Position = returnPosition;
            return entries;
        }

        private static void SkipAtr1(BinaryReader reader)
        {
            var magic = ReadUtf8(reader, 4);
            if (!string.Equals(magic, "ATR1", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Expected ATR1 section, found '{magic}'.");
            }

            // Some tools write garbage lengths here, so mimic the Rust implementation and scan for TXT2.
            while (true)
            {
                var next = reader.ReadUInt32();
                var bytes = BitConverter.GetBytes(next);
                if (bytes[0] == (byte)'T' && bytes[1] == (byte)'X' && bytes[2] == (byte)'T' && bytes[3] == (byte)'2')
                {
                    reader.BaseStream.Position -= 4;
                    return;
                }
            }
        }

        private static List<ushort[]> ParseTxt2(BinaryReader reader)
        {
            var magic = ReadUtf8(reader, 4);
            if (!string.Equals(magic, "TXT2", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Expected TXT2 section, found '{magic}'.");
            }

            var sectionLength = reader.ReadUInt32();
            reader.BaseStream.Position += 8; // Skip padding/flags.
            var basePosition = reader.BaseStream.Position;
            var count = reader.ReadUInt32();

            var offsets = new List<long>((int)count);
            for (var i = 0; i < count; i++)
            {
                offsets.Add(basePosition + reader.ReadUInt32());
            }

            var entries = new List<ushort[]>((int)count);
            for (var i = 0; i < offsets.Count; i++)
            {
                var offset = offsets[i];
                var end = (i + 1 < offsets.Count) ? offsets[i + 1] : basePosition + sectionLength;
                reader.BaseStream.Position = offset;

                var data = new List<ushort>();
                while (reader.BaseStream.Position < end)
                {
                    data.Add(reader.ReadUInt16());
                }

                entries.Add(data.ToArray());
            }

            return entries;
        }

        private static long Align(long value, int alignment)
        {
            var mask = alignment - 1;
            return (value + mask) & ~mask;
        }

        private static string ReadUtf8(BinaryReader reader, int length)
        {
            if (length == 0)
            {
                return string.Empty;
            }

            var bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
