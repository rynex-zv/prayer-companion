namespace PrayAdFree.Core.Services;

public static class AudioFileTypeDetector {
    private const int MaxSafeExtensionLength = 10;

    public static string ResolveExtension(string? fileName, ReadOnlySpan<byte> header) {
        var fromName = NormalizeExtension(Path.GetExtension(fileName ?? string.Empty));
        if (!string.IsNullOrWhiteSpace(fromName)) {
            return fromName;
        }

        if (header.Length >= 4) {
            if (header[0] == (byte)'f' &&
                header[1] == (byte)'L' &&
                header[2] == (byte)'a' &&
                header[3] == (byte)'C') {
                return ".flac";
            }

            if (header[0] == (byte)'O' &&
                header[1] == (byte)'g' &&
                header[2] == (byte)'g' &&
                header[3] == (byte)'S') {
                return ".ogg";
            }
        }

        if (header.Length >= 12) {
            if (header[0] == (byte)'R' &&
                header[1] == (byte)'I' &&
                header[2] == (byte)'F' &&
                header[3] == (byte)'F' &&
                header[8] == (byte)'W' &&
                header[9] == (byte)'A' &&
                header[10] == (byte)'V' &&
                header[11] == (byte)'E') {
                return ".wav";
            }

            if (header[4] == (byte)'f' &&
                header[5] == (byte)'t' &&
                header[6] == (byte)'y' &&
                header[7] == (byte)'p') {
                return ".m4a";
            }
        }

        if (header.Length >= 3 &&
            header[0] == (byte)'I' &&
            header[1] == (byte)'D' &&
            header[2] == (byte)'3') {
            return ".mp3";
        }

        if (header.Length >= 2 &&
            header[0] == 0xFF &&
            (header[1] & 0xE0) == 0xE0) {
            if ((header[1] & 0xF6) == 0xF0) {
                return ".aac";
            }

            return ".mp3";
        }

        if (header.Length >= 5 &&
            header[0] == (byte)'#' &&
            header[1] == (byte)'!' &&
            header[2] == (byte)'A' &&
            header[3] == (byte)'M' &&
            header[4] == (byte)'R') {
            return ".amr";
        }

        if (header.Length >= 16 &&
            header[0] == 0x30 &&
            header[1] == 0x26 &&
            header[2] == 0xB2 &&
            header[3] == 0x75 &&
            header[4] == 0x8E &&
            header[5] == 0x66 &&
            header[6] == 0xCF &&
            header[7] == 0x11) {
            return ".wma";
        }

        return ".audio";
    }

    private static string? NormalizeExtension(string? extension) {
        if (string.IsNullOrWhiteSpace(extension)) {
            return null;
        }

        var trimmed = extension.Trim();
        if (trimmed.Length <= 1 || trimmed.Length > MaxSafeExtensionLength) {
            return null;
        }

        if (trimmed[0] != '.') {
            trimmed = "." + trimmed;
        }

        for (var i = 1; i < trimmed.Length; i++) {
            if (!char.IsLetterOrDigit(trimmed[i])) {
                return null;
            }
        }

        return trimmed.ToLowerInvariant();
    }
}
