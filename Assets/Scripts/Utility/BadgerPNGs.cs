using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Utility
{
    public class Png
    {
        private readonly byte[] _header;
        private readonly IList<Chunk> _chunks;

        public Png(byte[] bytes)
        {
            _header = new byte[8];
            _chunks = new List<Chunk>();

            using (var memoryStream = new MemoryStream(bytes))
            {
                memoryStream.Seek(0, SeekOrigin.Begin);

                memoryStream.Read(_header, 0, _header.Length);

                while (memoryStream.Position < memoryStream.Length)
                    _chunks.Add(ChunkFromStream(memoryStream));

                memoryStream.Close();
            }
        }

        public void AddInternationalText(string keyword, string text)
        {
            // 1-79     (keyword)
            // 1        (null character)
            // 1        (compression flag)
            // 1        (compression method)
            // 0+       (language)
            // 1        (null character)
            // 0+       (translated keyword)
            // 1        (null character)
            // 0+       (text)

            var typeBytes = Encoding.UTF8.GetBytes("iTXt");
            var keywordBytes = Encoding.UTF8.GetBytes(keyword);
            var textBytes = Encoding.UTF8.GetBytes(text);
            var nullByte = BitConverter.GetBytes('\0')[0];
            var zeroByte = BitConverter.GetBytes(0)[0];

            var data = new List<byte>();

            data.AddRange(keywordBytes);
            data.Add(nullByte);
            data.Add(zeroByte);
            data.Add(zeroByte);
            data.Add(nullByte);
            data.Add(nullByte);
            data.AddRange(textBytes);

            var chunk = new Chunk(typeBytes, data.ToArray());

            _chunks.Insert(1, chunk);
        }

        public void RemoveChunk(string chunkName)
        {
            byte[] chunkNameBytes = Encoding.UTF8.GetBytes(chunkName);
            for(int i = 0; i < _chunks.Count; i++)
            {
                if (_chunks[i]._type.SequenceEqual(chunkNameBytes))
                {
                    _chunks.RemoveAt(i);
                    return;
                }
            }
        }

        public byte[] ToBytes()
        {
            using (var stream = new MemoryStream())
            {
                stream.Write(_header, 0, _header.Length);

                foreach (var chunk in _chunks)
                    chunk.WriteToStream(stream);

                var bytes = stream.ToArray();

                stream.Close();

                return bytes;
            }
        }

        private static Chunk ChunkFromStream(Stream stream)
        {
            var length = ReadBytes(stream, 4);
            var type = ReadBytes(stream, 4);
            var data = ReadBytes(stream, Convert.ToInt32(BitConverter.ToUInt32(length.Reverse().ToArray(), 0)));

            stream.Seek(4, SeekOrigin.Current);

            return new Chunk(type, data);
        }

        private static byte[] ReadBytes(Stream stream, int n)
        {
            var buffer = new byte[n];
            stream.Read(buffer, 0, n);
            return buffer;
        }

        private static void WriteBytes(Stream stream, byte[] bytes)
        {
            stream.Write(bytes, 0, bytes.Length);
        }

        private class Chunk
        {
            public Chunk(byte[] type, byte[] data)
            {
                _type = type;
                _data = data;
            }

            public void WriteToStream(Stream stream)
            {
                WriteBytes(stream, BitConverter.GetBytes(Convert.ToUInt32(_data.Length)).Reverse().ToArray());
                WriteBytes(stream, _type);
                WriteBytes(stream, _data);
                WriteBytes(stream, CalculateCrc(_type, _data));
            }

            private static byte[] CalculateCrc(IEnumerable<byte> type, IEnumerable<byte> data)
            {
                var bytes = new List<byte>();

                bytes.AddRange(type);
                bytes.AddRange(data);

                var hasher = new Crc32();

                using (var stream = new MemoryStream(bytes.ToArray()))
                    return hasher.ComputeHash(stream);
            }

            public readonly byte[] _type;
            public readonly byte[] _data;
        }
    }
}