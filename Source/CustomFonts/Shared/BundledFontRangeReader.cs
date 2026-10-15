using System;
using System.IO;
using AssetsTools.NET.Extra;
using SevenZip.Compression.LZMA;

namespace CustomFonts
{
    internal static class BundledFontRangeReader
    {
        internal static byte[] Read(string path, BundledFontData data)
        {
            var manager = new AssetsManager();
            try
            {
                // Read the tiny directory only; do not ask AssetsTools to unpack the data.
                var bundle = manager.LoadBundleFile(path, false);
                var blocks = bundle.file.BlockAndDirInfo.BlockInfos;
                if (blocks.Length != 1 || (blocks[0].Flags & 63) != 1) return null;
                bundle.file.GetFileRange(data.AssetsIndex, out long offset, out long length);
                if (offset < 0 || data.DataOffset < 0 || data.DataLength < 0 || data.DataOffset > length - data.DataLength)
                    throw new InvalidDataException("Invalid bundled font bounds.");
                long start = checked(offset + data.DataOffset), end = checked(start + data.DataLength);
                if (end > blocks[0].DecompressedSize) throw new InvalidDataException("Invalid compressed font range.");
                var reader = bundle.file.DataReader;
                reader.Position = 0;
                byte[] properties = reader.ReadBytes(5);
                var decoder = new Decoder();
                decoder.SetDecoderProperties(properties);
                using (var output = new RangeOutput(start, data.DataLength))
                {
                    // LZMA must decode preceding bytes for its dictionary. Discard those
                    // bytes, retain only this font, and stop before the rest of the block.
                    // Buffer byte-wise range-coder input for Mono as well as CoreCLR.
                    var input = new BufferedStream(reader.BaseStream, 64 * 1024);
                    decoder.Code(input, output, blocks[0].CompressedSize - 5, end, null);
                    if (output.Copied != data.DataLength) throw new InvalidDataException("Truncated font range.");
                    return output.Bytes;
                }
            }
            finally { manager.UnloadAll(); }
        }

        private sealed class RangeOutput : Stream
        {
            private readonly long _start;
            private long _position;
            internal readonly byte[] Bytes;
            internal int Copied;
            internal RangeOutput(long start, int length) { _start = start; Bytes = new byte[length]; }
            public override void Write(byte[] buffer, int offset, int count)
            {
                long from = Math.Max(_position, _start), to = Math.Min(_position + count, _start + Bytes.Length);
                if (to > from)
                {
                    int length = checked((int)(to - from));
                    Buffer.BlockCopy(buffer, offset + checked((int)(from - _position)), Bytes, checked((int)(from - _start)), length);
                    Copied += length;
                }
                _position += count;
            }
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => _position;
            public override long Position { get => _position; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
