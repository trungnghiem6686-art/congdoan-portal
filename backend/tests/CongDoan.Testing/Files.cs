using SkiaSharp;

namespace CongDoan.Testing;

public static class Files
{
    public static byte[] Png(int width = 1200, int height = 800)
    {
        using var bmp = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bmp)) canvas.Clear(new SKColor(18, 54, 91));
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    /// <summary>Bytes with a real MP4 "ftyp" signature followed by random payload.</summary>
    public static byte[] Mp4(int size)
    {
        var bytes = new byte[size];
        new Random(42).NextBytes(bytes);
        byte[] header = { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32, 0x00, 0x00, 0x00, 0x00, 0x69, 0x73, 0x6F, 0x6D, 0x6D, 0x70, 0x34, 0x32 };
        header.CopyTo(bytes, 0);
        return bytes;
    }

    /// <summary>Windows executable header ("MZ").</summary>
    public static byte[] Exe() => new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0xFF, 0xFF };

    public static MultipartFormDataContent Form(byte[] bytes, string fileName, string contentType, params (string Key, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        foreach (var (k, v) in fields) form.Add(new StringContent(v), k);
        return form;
    }
}
