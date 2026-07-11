using System.IO;

namespace DragonWarsTrainer.Game;

/// <summary>
/// Decompresses a Dragon Wars data chunk. The archive (DATA1/DATA2) stores every chunk as a
/// bit-oriented Huffman stream: a little-endian word giving the decoded length, followed by a
/// serialized canonical Huffman tree and then the encoded bytes. This is a direct port of the
/// <c>fraterrisus/dragonjars</c> <c>HuffmanDecoder</c>, verified byte-for-byte against the real
/// game files.
/// </summary>
public static class HuffmanDecoder
{
    /// <summary>Decompresses one raw chunk into its decoded bytes.</summary>
    public static byte[] Decode(byte[] raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (raw.Length < 2) throw new InvalidDataException("Chunk is too small to hold a length header.");
        var reader = new BitReader(raw);
        int size = reader.ReadByteAligned() | (reader.ReadByteAligned() << 8);
        var tree = BuildTree(reader, new List<(int Run, int Value)>(), run: 0, index: new int[1]);

        var outp = new byte[size];
        int count = size;
        int pos = 0;
        Node node = tree;
        while (count > 0)
        {
            if (node.IsLeaf)
            {
                outp[pos++] = (byte)node.Value;
                node = tree;
                count--;
                continue;
            }
            node = reader.ReadBit() ? node.Right! : node.Left!;
        }
        return outp;
    }

    private static Node BuildTree(BitReader reader, List<(int Run, int Value)> traces, int run, int[] index)
    {
        (int Run, int Value) Peek()
        {
            while (index[0] >= traces.Count)
            {
                int zeros = -1;
                bool bit = false;
                while (!bit) { zeros++; bit = reader.ReadBit(); }
                traces.Add((zeros, reader.ReadEightBits()));
            }
            return traces[index[0]];
        }

        if (run == Peek().Run)
        {
            int value = Peek().Value;
            index[0]++;
            return new Node(value);
        }
        var left = BuildTree(reader, traces, run + 1, index);
        var right = BuildTree(reader, traces, 0, index);
        return new Node(left, right);
    }

    private sealed class Node
    {
        public readonly bool IsLeaf;
        public readonly int Value;
        public readonly Node? Left;
        public readonly Node? Right;

        public Node(int value) { IsLeaf = true; Value = value; }
        public Node(Node left, Node right) { Left = left; Right = right; }
    }

    /// <summary>MSB-first bit reader over a byte buffer, matching the game's packing order.</summary>
    private sealed class BitReader
    {
        private readonly byte[] _data;
        private int _pos;
        private int _buf;
        private int _count;

        public BitReader(byte[] data) => _data = data;

        /// <summary>Reads a raw byte directly from the buffer (used only for the length header).</summary>
        public int ReadByteAligned()
        {
            if (_pos >= _data.Length) throw new InvalidDataException("Unexpected end of chunk.");
            return _data[_pos++];
        }

        public bool ReadBit()
        {
            if (_count == 0)
            {
                if (_pos >= _data.Length) throw new InvalidDataException("Unexpected end of bitstream.");
                _buf = _data[_pos++];
                _count = 8;
            }
            _count--;
            return ((_buf >> _count) & 1) != 0;
        }

        /// <summary>Reads eight bits MSB-first from the bit stream.</summary>
        public int ReadEightBits()
        {
            int v = 0;
            for (int i = 0; i < 8; i++) v = (v << 1) | (ReadBit() ? 1 : 0);
            return v;
        }
    }
}
