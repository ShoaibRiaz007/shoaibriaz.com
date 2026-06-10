namespace Portfolio.Application.Common;

/// <summary>Validates uploaded media by extension AND magic bytes (content sniffing), to stop spoofed uploads.</summary>
public static class FileSignatureValidator
{
    private static readonly string[] ImageExts = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private static readonly string[] VideoExts = { ".mp4", ".webm", ".ogg", ".mov" };

    public record Result(bool Ok, string MediaType, string Message);

    public static Result Validate(byte[] header, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var isImageExt = ImageExts.Contains(ext);
        var isVideoExt = VideoExts.Contains(ext);
        if (!isImageExt && !isVideoExt)
            return new(false, "", $"Unsupported file type '{ext}'. Allowed: images ({string.Join(", ", ImageExts)}) and video ({string.Join(", ", VideoExts)}).");

        var detected = Detect(header);
        if (detected is null)
            return new(false, "", "The file content does not match a supported image or video format.");

        var (category, _) = detected.Value;
        if (category == "image" && !isImageExt)
            return new(false, "", "File content is an image but the extension is not.");
        if (category == "video" && !isVideoExt)
            return new(false, "", "File content is a video but the extension is not.");

        return new(true, category, "");
    }

    private static (string Category, string Format)? Detect(byte[] b)
    {
        bool Match(int offset, params byte[] sig)
        {
            if (b.Length < offset + sig.Length) return false;
            for (int i = 0; i < sig.Length; i++)
                if (b[offset + i] != sig[i]) return false;
            return true;
        }

        // Images
        if (Match(0, 0xFF, 0xD8, 0xFF)) return ("image", "jpeg");
        if (Match(0, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)) return ("image", "png");
        if (Match(0, 0x47, 0x49, 0x46, 0x38)) return ("image", "gif");
        if (Match(0, 0x52, 0x49, 0x46, 0x46) && Match(8, 0x57, 0x45, 0x42, 0x50)) return ("image", "webp"); // RIFF....WEBP

        // Video
        if (Match(4, 0x66, 0x74, 0x79, 0x70)) return ("video", "mp4"); // ....ftyp (mp4/mov)
        if (Match(0, 0x1A, 0x45, 0xDF, 0xA3)) return ("video", "webm"); // EBML (webm/mkv)
        if (Match(0, 0x4F, 0x67, 0x67, 0x53)) return ("video", "ogg"); // OggS

        return null;
    }
}
