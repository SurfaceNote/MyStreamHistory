using System.Buffers.Binary;

namespace MyStreamHistory.ContentService.Api;

public static class ImageInspector
{
    public static string? Inspect(ReadOnlySpan<byte> bytes)
    {
        int width, height;
        string? type = null;
        if (bytes.Length >= 45 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && BinaryPrimitives.ReadUInt32BigEndian(bytes[8..12]) == 13 && bytes[12..16].SequenceEqual("IHDR"u8)
            && bytes[^8..^4].SequenceEqual("IEND"u8))
        {
            type = "image/png";
            width = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes[16..20]);
            height = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes[20..24]);
        }
        else if (bytes.Length >= 20 && bytes[^1] == 0x3b
            && (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8)))
        {
            type = "image/gif";
            width = BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..8]);
            height = BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..10]);
        }
        else if (bytes.Length >= 30 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)
            && BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..8]) + 8 <= bytes.Length)
        {
            type = "image/webp";
            if (bytes[12..16].SequenceEqual("VP8X"u8))
            {
                width = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16);
                height = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16);
            }
            else if (bytes[12..16].SequenceEqual("VP8L"u8) && bytes[20] == 0x2f)
            {
                width = 1 + (((bytes[22] & 0x3f) << 8) | bytes[21]);
                height = 1 + (((bytes[24] & 0x0f) << 10) | (bytes[23] << 2) | ((bytes[22] & 0xc0) >> 6));
            }
            else if (bytes[12..16].SequenceEqual("VP8 "u8) && bytes[23..26].SequenceEqual(new byte[] { 0x9d, 0x01, 0x2a }))
            {
                width = BinaryPrimitives.ReadUInt16LittleEndian(bytes[26..28]) & 0x3fff;
                height = BinaryPrimitives.ReadUInt16LittleEndian(bytes[28..30]) & 0x3fff;
            }
            else return null;
        }
        else if (bytes.Length >= 6 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[^2] == 0xff && bytes[^1] == 0xd9)
        {
            type = "image/jpeg";
            if (!TryReadJpegDimensions(bytes, out width, out height)) return null;
        }
        else return null;
        return width is > 0 and <= 10000 && height is > 0 and <= 10000 && (long)width * height <= 40_000_000
            ? type : null;
    }

    private static bool TryReadJpegDimensions(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = height = 0;
        for (var index = 2; index + 4 < bytes.Length;)
        {
            if (bytes[index] != 0xff) return false;
            while (index < bytes.Length && bytes[index] == 0xff) index++;
            if (index >= bytes.Length) return false;
            var marker = bytes[index++];
            if (marker is 0xd8 or 0xd9 or >= 0xd0 and <= 0xd7) continue;
            if (marker == 0xda || index + 2 > bytes.Length) return false;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes[index..(index + 2)]);
            if (length < 2 || index + length > bytes.Length) return false;
            if (marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7 or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf)
            {
                if (length < 7) return false;
                height = BinaryPrimitives.ReadUInt16BigEndian(bytes[(index + 3)..(index + 5)]);
                width = BinaryPrimitives.ReadUInt16BigEndian(bytes[(index + 5)..(index + 7)]);
                return true;
            }
            index += length;
        }
        return false;
    }
}
