using System;
using System.IO;
using System.IO.Compression;
using Unity.Mathematics;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace NHSRemont.Networking
{
    public static class NetworkingUtils
    {
        public static void Write(this BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }
        
        public static Vector3 ReadVector3(this BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        public static unsafe void WriteHalf(this BinaryWriter writer, half value)
        {
            ushort num = *(ushort*) &value;
            writer.Write((byte) num);
            writer.Write((byte) (num >> 8));
        }

        public static unsafe half ReadHalf(this BinaryReader reader)
        {
            ushort num = (ushort)reader.ReadInt16();
            return *(half*) &num;
        }
        
        //https://stackoverflow.com/a/10599797
        public static byte[] CompressData(ReadOnlySpan<byte> data)
        {
            int uncompSize = data.Length;
            using var compressStream = new MemoryStream();
            using(var compressor = new DeflateStream(compressStream, CompressionLevel.Optimal))
            {
                compressor.Write(data);
            }
            var output = compressStream.ToArray();
            Debug.Log("uncompressed: " + uncompSize + ", compressed: " + output.Length);
            return output;
        }
        public static Stream DecompressStream(byte[] input)
        {
            var output = new MemoryStream();

            using (var compressStream = new MemoryStream(input))
            using (var decompressor = new DeflateStream(compressStream, CompressionMode.Decompress))
                decompressor.CopyTo(output);

            output.Position = 0;
            return output;
        }
    }
}