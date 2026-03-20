using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class AudioFileTypeDetectorTests {
    [Theory]
    [InlineData("sample.mp3", new byte[] { 0x00, 0x00 }, ".mp3")]
    [InlineData("sample.FLAC", new byte[] { 0x00, 0x00 }, ".flac")]
    [InlineData("sample.opus", new byte[] { 0x00, 0x00 }, ".opus")]
    public void ResolveExtension_PreservesSafeFileExtension(string fileName, byte[] header, string expected) {
        var extension = AudioFileTypeDetector.ResolveExtension(fileName, header);

        Assert.Equal(expected, extension);
    }

    [Theory]
    [InlineData(new byte[] { (byte)'I', (byte)'D', (byte)'3' }, ".mp3")]
    [InlineData(new byte[] { (byte)'f', (byte)'L', (byte)'a', (byte)'C' }, ".flac")]
    [InlineData(new byte[] { (byte)'O', (byte)'g', (byte)'g', (byte)'S' }, ".ogg")]
    [InlineData(new byte[] { (byte)'#', (byte)'!', (byte)'A', (byte)'M', (byte)'R' }, ".amr")]
    [InlineData(new byte[] { 0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11, 0xA6, 0xD9, 0x00, 0xAA, 0x00, 0x62, 0xCE, 0x6C }, ".wma")]
    public void ResolveExtension_InferFromKnownHeaders(byte[] header, string expected) {
        var extension = AudioFileTypeDetector.ResolveExtension(string.Empty, header);

        Assert.Equal(expected, extension);
    }

    [Fact]
    public void ResolveExtension_DetectsWaveContainer() {
        var header = new byte[] {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F',
            0x00, 0x00, 0x00, 0x00,
            (byte)'W', (byte)'A', (byte)'V', (byte)'E'
        };

        var extension = AudioFileTypeDetector.ResolveExtension(null, header);

        Assert.Equal(".wav", extension);
    }

    [Fact]
    public void ResolveExtension_DetectsM4aContainer() {
        var header = new byte[] {
            0x00, 0x00, 0x00, 0x20,
            (byte)'f', (byte)'t', (byte)'y', (byte)'p',
            (byte)'M', (byte)'4', (byte)'A', (byte)' '
        };

        var extension = AudioFileTypeDetector.ResolveExtension(null, header);

        Assert.Equal(".m4a", extension);
    }

    [Fact]
    public void ResolveExtension_FallsBackWhenUnknown() {
        var extension = AudioFileTypeDetector.ResolveExtension(null, new byte[] { 0x01, 0x02, 0x03 });

        Assert.Equal(".audio", extension);
    }
}
