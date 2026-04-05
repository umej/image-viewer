using ImageViewer.Utilities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using Svg;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using Windows.Storage.Streams;
using WinRT;
using DrawingImage = System.Drawing.Bitmap;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace ImageViewer.Wrapper;

/// <summary>
/// RAW画像の設定情報
/// </summary>
public class RawImageConfig
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int BitDepth { get; set; } = 8; // 8-16bit
    public BayerPattern StartingColor { get; set; } = BayerPattern.RGGB;
}

/// <summary>
/// Bayer配列パターン
/// </summary>
public enum BayerPattern
{
    RGGB,
    GRBG,
    GBRG,
    BGGR
}

/// <summary>
/// 画素値情報
/// </summary>
public class PixelValues
{
    public ushort R { get; set; }
    public ushort G { get; set; }
    public ushort B { get; set; }
    public ushort A { get; set; } = 65535;

    // RAW画像用: 元のBayer画素値
    public ushort RawValue { get; set; }
}

internal partial class Image
{
    public static readonly string[] SupportedFileTypes = [".jpg", ".jpeg", ".bmp", ".png", ".gif", ".tif", ".tiff", ".tga", ".ico", ".webp", ".svg", ".raw"];
    public static readonly string[] SaveFileTypes = [".jpg", ".png", ".webp", ".bmp", ".gif", ".tiff", ".tga"];

    private readonly string[] NativeExtensions = [".jpg", ".jpeg", ".bmp", ".png", ".gif", ".tif", ".tiff", ".tga", ".webp"];

    public event EventHandler ImageLoaded;
    public event EventHandler ImageFailed;

    protected bool WorkingImageLoaded;
    protected ImageSharpImage WorkingImage;
    protected IImageEncoder Encoder = new JpegEncoder { Quality = 100 };
    
    // RAW画像用プロパティ
    protected ushort[] RawBayerData;
    protected RawImageConfig RawConfig;
    protected bool IsRawImage = false;

    public void Load(string path)
    {
        LoadImageFromPath(path);
    }

    public void Load(IInputStream stream)
    {
        LoadImageFromMemory(stream);
    }

    public bool Loaded => WorkingImageLoaded;
    public double Height => WorkingImage.Height;
    public double Width => WorkingImage.Width;

    public void Dispose()
    {
        WorkingImage?.Dispose();
        RawBayerData = null;
        WorkingImageLoaded = false;
        IsRawImage = false;
    }
    
    public string GetImageDimensionsAsString()
    {
        if(!WorkingImageLoaded) return "";
        return WorkingImage.Width + " x " + WorkingImage.Height;
    }

    public string GetDepthAsString()
    {
        if(!WorkingImageLoaded) return "";
        return WorkingImage.PixelType.BitsPerPixel + " bit";
    }

    public IRandomAccessStream GetBitmapImageSource()
    {
        if(WorkingImage == null) return null;

        MemoryStream memory = new();
        WorkingImage.Save(memory, Encoder);
        memory.Position = 0;

        return memory.AsRandomAccessStream();
    }

    /// <summary>
    /// 指定座標の画素値を取得（通常画像とRAW画像の両方に対応）
    /// </summary>
    public PixelValues GetPixelValues(int x, int y)
    {
        if(!WorkingImageLoaded || x < 0 || y < 0 || x >= WorkingImage.Width || y >= WorkingImage.Height)
            return null;

        if(IsRawImage)
        {
            return GetRawPixelValues(x, y);
        }
        else
        {
            return GetNormalPixelValues(x, y);
        }
    }

    /// <summary>
    /// 通常画像から画素値を取得
    /// </summary>
    private PixelValues GetNormalPixelValues(int x, int y)
    {
        var frame = WorkingImage.Frames[0];
        var pixelType = frame.GetType().GenericTypeArguments[0];

        var result = new PixelValues();

        if (pixelType == typeof(SixLabors.ImageSharp.PixelFormats.Rgba32))
        {
            Image<Rgba32> img = (Image<Rgba32>)WorkingImage;
            var pixel = img[x, y];
            result.R = pixel.R;
            result.G = pixel.G;
            result.B = pixel.B;
            result.A = pixel.A;
        }
        else if (pixelType == typeof(SixLabors.ImageSharp.PixelFormats.Rgb24))
        {
            Image<Rgb24> img = (Image<Rgb24>)WorkingImage;
            var pixel = img[x, y];
            result.R = pixel.R;
            result.G = pixel.G;
            result.B = pixel.B;
            result.A = 255;
        }
        else if (pixelType == typeof(SixLabors.ImageSharp.PixelFormats.L8))
        {
            Image<L8> img = (Image<L8>)WorkingImage;
            var pixel = img[x, y];
            result.R = pixel.PackedValue;
            result.G = pixel.PackedValue;
            result.B = pixel.PackedValue;
            result.A = 255;
        }
        else if (pixelType == typeof(SixLabors.ImageSharp.PixelFormats.L16))
        {
            Image<L16> img = (Image<L16>)WorkingImage;
            var pixel = img[x, y];
            result.R = pixel.PackedValue;
            result.G = pixel.PackedValue;
            result.B = pixel.PackedValue;
            result.A = 65535;
        }

        return result;
    }

    /// <summary>
    /// RAW Bayer画像から画素値を取得
    /// </summary>
    private PixelValues GetRawPixelValues(int x, int y)
    {
        var result = new PixelValues();

        if(RawBayerData == null || x < 0 || y < 0 || x >= RawConfig.Width || y >= RawConfig.Height)
            return result;

        ushort bayerValue = RawBayerData[y * RawConfig.Width + x];
        result.RawValue = bayerValue;

        // ビット深度に基づいてスケーリング
        ushort maxValue = RawConfig.BitDepth == 16 ? (ushort)65535 : (ushort)255;
        byte scaledValue = (byte)((bayerValue * 65535) / maxValue);

        // Bayer配列パターンに基づいてR/G/Bに割り当て
        bool isEvenX = (x % 2) == 0;
        bool isEvenY = (y % 2) == 0;

        var (r, g, b) = GetBayerComponents(isEvenX, isEvenY, scaledValue);

        result.R = r;
        result.G = g;
        result.B = b;
        result.A = 65535;

        return result;
    }

    /// <summary>
    /// RAW画像を読み込む
    /// </summary>
    public void LoadRawImage(string path, int width, int height, int bitDepth, BayerPattern startingColor)
    {
        RawConfig = new RawImageConfig
        {
            Width = width,
            Height = height,
            BitDepth = bitDepth,
            StartingColor = startingColor
        };

        IsRawImage = true;
        LoadRawImageFromPath(path);
    }

    public async void Save(string path, string type)
    {
        switch(type)
        {
            case ".jpg":
                await WorkingImage.SaveAsJpegAsync(path, new JpegEncoder { Quality = 100 });
                break;

            case ".png":
                await WorkingImage.SaveAsPngAsync(path);
                break;

            case ".webp":
                await WorkingImage.SaveAsWebpAsync(path);
                break;

            case ".bmp":
                await WorkingImage.SaveAsBmpAsync(path);
                break;

            case ".gif":
                await WorkingImage.SaveAsGifAsync(path);
                break;

            case ".tga":
                await WorkingImage.SaveAsTgaAsync(path);
                break;

            case ".tiff":
                await WorkingImage.SaveAsTiffAsync(path);
                break;
        }
    }

    private async void LoadImageFromPath(string path)
    {
        try
        {
            string extension = Path.GetExtension(path).ToLower();

            if(NativeExtensions.Contains(extension))
            {
                WorkingImage = await ImageSharpImage.LoadAsync(path, CancellationToken.None);
                Encoder = WorkingImage.DetectEncoder(path);

                switch(Encoder)
                {
                    case TgaEncoder:
                        Encoder = new PngEncoder();
                        break;
                    case JpegEncoder:
                        Encoder = new JpegEncoder { Quality = 100 };
                        break;
                }
            }
            else
            {
                DrawingImage tmp;

                if(extension == ".svg")
                {
                    SvgDocument svgDocument = SvgDocument.Open(path);
                    svgDocument.ShapeRendering = SvgShapeRendering.Auto;
                    tmp = svgDocument.AdjustSize(1024, 1024).Draw();

                    Encoder = new PngEncoder();
                }
                else
                {
                    byte[] fileBytes = await File.ReadAllBytesAsync(path);
                    using MemoryStream defaultMemoryStream = new(fileBytes);
                    tmp = (DrawingImage)System.Drawing.Image.FromStream(defaultMemoryStream);
                    await defaultMemoryStream.DisposeAsync();
                }

                using MemoryStream saveMemoryStream = new();
                tmp.Save(saveMemoryStream, ImageFormat.Png);
                WorkingImage = ImageSharpImage.Load(saveMemoryStream.ToArray());
                await saveMemoryStream.DisposeAsync();
            }

            WorkingImageLoaded = true;
            ImageLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch(Exception e)
        {
            ImageFailedEventArgs args = new()
            {
                Message = e.Message,
                Path = path
            };

            ImageFailed?.Invoke(this, args);
        }
    }

    private async void LoadImageFromMemory(IInputStream stream)
    {
        try
        {
            WorkingImage = await ImageSharpImage.LoadAsync(stream.AsStreamForRead());
            Encoder = new PngEncoder();

            WorkingImageLoaded = true;
            ImageLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch(Exception e)
        {
            ImageFailedEventArgs args = new()
            {
                Message = e.Message
            };

            ImageFailed?.Invoke(this, args);
        }
    }

    /// <summary>
    /// RAW Bayer画像をファイルから読み込み
    /// </summary>
    private async void LoadRawImageFromPath(string path)
    {
        try
        {
            if(RawConfig == null)
                throw new InvalidOperationException("RawImageConfig is not set. Use LoadRawImage method.");

            byte[] fileBytes = await File.ReadAllBytesAsync(path);
            
            // Bayer画像データをushortの配列に変換
            ConvertBayerData(fileBytes);

            // Bayer画像をビジュアル表示用に変換
            ConvertBayerToImage();

            WorkingImageLoaded = true;
            ImageLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch(Exception e)
        {
            ImageFailedEventArgs args = new()
            {
                Message = e.Message,
                Path = path
            };

            ImageFailed?.Invoke(this, args);
        }
    }

    /// <summary>
    /// ファイルバイト列をBayer画像データに変換
    /// </summary>
    private void ConvertBayerData(byte[] fileBytes)
    {
        int pixelCount = RawConfig.Width * RawConfig.Height;
        RawBayerData = new ushort[pixelCount];

        if(RawConfig.BitDepth == 8)
        {
            // 8bit: バイト列をそのままushortに変換
            for(int i = 0; i < pixelCount; i++)
            {
                RawBayerData[i] = fileBytes[i];
            }
        }
        else if(RawConfig.BitDepth == 16)
        {
            // 16bit: リトルエンディアン
            for(int i = 0; i < pixelCount; i++)
            {
                int byteIndex = i * 2;
                RawBayerData[i] = (ushort)(fileBytes[byteIndex] | (fileBytes[byteIndex + 1] << 8));
            }
        }
        else
        {
            throw new NotSupportedException($"BitDepth {RawConfig.BitDepth} is not supported. Use 8 or 16.");
        }
    }

    /// <summary>
    /// Bayer画像データをImageSharpのイメージに変換（Bayer配列をそのまま表示）
    /// </summary>
    private void ConvertBayerToImage()
    {
        // RGB画像として作成（ピクセル値は正規化）
        WorkingImage = new Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(RawConfig.Width, RawConfig.Height);

        ushort maxValue = RawConfig.BitDepth == 16 ? (ushort)65535 : (ushort)255;
        Image<Rgb24> pixelAccessor = (Image<Rgb24>)WorkingImage;

        for(int y = 0; y < RawConfig.Height; y++)
        {
            for(int x = 0; x < RawConfig.Width; x++)
            {
                ushort bayerValue = RawBayerData[y * RawConfig.Width + x];
                byte displayValue = (byte)((bayerValue * 255) / maxValue);

                // Bayer配列パターンに基づいてR/G/Bに割り当て
                bool isEvenX = (x % 2) == 0;
                bool isEvenY = (y % 2) == 0;
                var (r, g, b) = GetBayerComponents(isEvenX, isEvenY, displayValue);

                pixelAccessor[x, y] = new SixLabors.ImageSharp.PixelFormats.Rgb24(r, g, b);
            }
        }

        Encoder = new PngEncoder();
    }

    /// <summary>
    /// Bayer配列の座標からR/G/B成分を取得
    /// </summary>
    private (byte R, byte G, byte B) GetBayerComponents(bool isEvenX, bool isEvenY, byte value)
    {
        return RawConfig.StartingColor switch
        {
            BayerPattern.RGGB => isEvenY
                ? (isEvenX ? (value, (byte)0, (byte)0) : ((byte)0, value, (byte)0)) // R, G
                : (isEvenX ? ((byte)0, value, (byte)0) : ((byte)0, (byte)0, value)), // G, B

            BayerPattern.GRBG => isEvenY
                ? (isEvenX ? ((byte)0, value, (byte)0) : (value, (byte)0, (byte)0)) // G, R
                : (isEvenX ? ((byte)0, (byte)0, value) : ((byte)0, value, (byte)0)), // B, G

            BayerPattern.GBRG => isEvenY
                ? (isEvenX ? ((byte)0, value, (byte)0) : ((byte)0, (byte)0, value)) // G, B
                : (isEvenX ? (value, (byte)0, (byte)0) : ((byte)0, value, (byte)0)), // R, G

            BayerPattern.BGGR => isEvenY
                ? (isEvenX ? ((byte)0, (byte)0, value) : ((byte)0, value, (byte)0)) // B, G
                : (isEvenX ? ((byte)0, value, (byte)0) : (value, (byte)0, (byte)0)), // G, R

            _ => (value, value, value)
        };
    }
}

public class ImageFailedEventArgs : EventArgs
{
    public string Message { get; set; }
    public string Path { get; init; }
}