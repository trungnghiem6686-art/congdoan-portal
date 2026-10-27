using CongDoan.Application.Cms;
using SkiaSharp;

namespace CongDoan.Infrastructure.Cms;

public sealed class SkiaImageProcessor : IImageProcessor
{
    public ImageInfo? Inspect(Stream image)
    {
        using var bmp = SKBitmap.Decode(image);
        return bmp is null || bmp.Width == 0 ? null : new ImageInfo(bmp.Width, bmp.Height);
    }

    public void WriteThumbnail(Stream image, Stream output, int width)
    {
        using var codec = SKCodec.Create(image) ?? throw new InvalidOperationException("Cannot decode image.");
        using var original = SKBitmap.Decode(codec);
        var oriented = Orient(original, codec.EncodedOrigin);
        try
        {
            var targetW = Math.Min(width, oriented.Width);
            var targetH = Math.Max(1, (int)Math.Round(oriented.Height * (double)targetW / oriented.Width));
            using var resized = oriented.Resize(new SKImageInfo(targetW, targetH), SKFilterQuality.Medium);
            using var img = SKImage.FromBitmap(resized);
            using var data = img.Encode(SKEncodedImageFormat.Jpeg, 82);
            data.SaveTo(output);
        }
        finally
        {
            if (!ReferenceEquals(oriented, original)) oriented.Dispose();
        }
    }

    private static SKBitmap Orient(SKBitmap bmp, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default) return bmp;
        var rotate90 = origin is SKEncodedOrigin.RightTop or SKEncodedOrigin.LeftBottom;
        var result = new SKBitmap(rotate90 ? bmp.Height : bmp.Width, rotate90 ? bmp.Width : bmp.Height);
        using var canvas = new SKCanvas(result);
        switch (origin)
        {
            case SKEncodedOrigin.BottomRight: canvas.RotateDegrees(180, bmp.Width / 2f, bmp.Height / 2f); break;
            case SKEncodedOrigin.RightTop: canvas.Translate(result.Width, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.LeftBottom: canvas.Translate(0, result.Height); canvas.RotateDegrees(270); break;
        }
        canvas.DrawBitmap(bmp, 0, 0);
        return result;
    }
}
