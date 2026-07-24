using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class BackgroundImageDecoderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(800)]
    [InlineData(3840)]
    public void SelectBoundedDecodeWidth_ReturnsNullWhenNoDownscaleNeeded(int? sourceWidth)
    {
        Assert.Null(BackgroundImageDecoder.SelectBoundedDecodeWidth(sourceWidth, 3840));
    }

    [Theory]
    [InlineData(3841)]
    [InlineData(8000)]
    [InlineData(20000)]
    public void SelectBoundedDecodeWidth_CapsOversizedImages(int sourceWidth)
    {
        Assert.Equal(3840, BackgroundImageDecoder.SelectBoundedDecodeWidth(sourceWidth, 3840));
    }

    [Fact]
    public void TryProbeImageWidth_ReadsWidthFromPngHeader()
    {
        using var stream = new MemoryStream(CreatePngHeader(8000, 2));

        Assert.Equal(8000, BackgroundImageDecoder.TryProbeImageWidth(stream));
    }

    [Fact]
    public void TryProbeImageWidth_RestoresStreamPosition()
    {
        using var stream = new MemoryStream(CreatePngHeader(640, 480));

        BackgroundImageDecoder.TryProbeImageWidth(stream);

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void TryProbeImageWidth_ReturnsNullForGarbageData()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });

        Assert.Null(BackgroundImageDecoder.TryProbeImageWidth(stream));
    }

    [Fact]
    public void TryProbeImageWidth_ReturnsNullForUnseekableStream()
    {
        using var stream = new NonSeekableStream(CreatePngHeader(640, 480));

        Assert.Null(BackgroundImageDecoder.TryProbeImageWidth(stream));
    }

    private static byte[] CreatePngHeader(int width, int height)
    {
        using var stream = new MemoryStream();
        stream.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        var ihdr = new byte[13];
        WriteUInt32BigEndian(ihdr, 0, (uint)width);
        WriteUInt32BigEndian(ihdr, 4, (uint)height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 6; // color type: RGBA
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace

        WriteChunk(stream, "IHDR", ihdr);
        WriteChunk(stream, "IEND", Array.Empty<byte>());
        return stream.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var lengthBytes = new byte[4];
        WriteUInt32BigEndian(lengthBytes, 0, (uint)data.Length);
        stream.Write(lengthBytes);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        var crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, typeBytes.Length);

        var crcBytes = new byte[4];
        WriteUInt32BigEndian(crcBytes, 0, Crc32(crcInput));
        stream.Write(crcBytes);
    }

    private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableStream(byte[] data)
        {
            _inner = new MemoryStream(data);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
