using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

const int ResampledImageWidth = 32;
const double TextMaxBrightness = 0.3;
const float BinaryLuminanceThreshold = 0.3f;
Color KnifeColor = Color.Magenta;
Color GapColor = Color.Cyan;

string sourceImageFilename = args.Length > 0 && string.IsNullOrWhiteSpace( args[0] )
    ? args[0] : "../../../images/ticket01.jpg";

using var inputStream = File.OpenRead(sourceImageFilename);

using Image<Rgba32> sourceImage = (Image<Rgba32>)Image.Load( inputStream );
//int ResampledImageWidth = ( sourceImage.Width / 32.0 ).NearestPowerOf2();

using Image<Rgba32> resampledImage = sourceImage.Clone( x => x
    .Resize( ResampledImageWidth, sourceImage.Height, KnownResamplers.Lanczos8 )
    .HistogramEqualization()
    .BinaryThreshold( BinaryLuminanceThreshold, BinaryThresholdMode.Luminance)
    );

var textBlocks = SeekTextBlocks( resampledImage );

Painter.FillGaps( sourceImage, textBlocks, GapColor );
Painter.DrawCuttingLine(sourceImage, textBlocks, KnifeColor);

SaveImage( sourceImage,
    Path.ChangeExtension( AddFileSuffix( sourceImageFilename, "_output"), ".png") );

SaveImage( resampledImage,
    Path.ChangeExtension( AddFileSuffix( sourceImageFilename, "_resampled" ), ".png" ) );

#region Kind of Magic Stuff

IReadOnlyList<TextBlock> SeekTextBlocks(Image<Rgba32> image)
{
    int topY = 0;
    List<TextBlock> textBlocks = new();
    SeekState state = SeekState.Start;
    for ( int y = 0; y < image.Height; y++ ) {
        var dark = HasDarkPixels( image, y );

        switch ( state ) {
            case SeekState.Start:
                if ( dark ) {
                    topY = y;
                    state = SeekState.End;
                }
                break;

            case SeekState.End:
                if ( !dark ) {
                    textBlocks.Add( new() { TopY = topY, BottomY = y } );
                    state = SeekState.Start;
                }
                break;
        }
    }

    if ( state == SeekState.End )
        textBlocks.Add( new() { TopY = topY, BottomY = image.Height } );

    return textBlocks;
}

bool HasDarkPixels( Image<Rgba32> image, int y )
{
    var dark = false;
    Span<Rgba32> pixelRowSpan = image.GetPixelRowSpan( y );
    for ( int x = 0; x < image.Width && !dark; x++ )
        dark = pixelRowSpan[x].Brightness() < TextMaxBrightness;

    return dark;
}

string AddFileSuffix( string filename, string suffix )
    => Path.Combine(
        Path.GetDirectoryName( filename ) ?? "./",
        Path.GetFileNameWithoutExtension( filename ) 
            + suffix 
            + Path.GetExtension( filename ) );

void SaveImage( Image image, string imageFilename )
{
    using var outStream = File.OpenWrite( imageFilename );
    image.Save( outStream, new PngEncoder() );
}

public enum SeekState { Start, End }

public struct TextBlock
{
    public int TopY;
    public int BottomY;
}

public static class Extensions
{
    public static double Brightness( this Rgba32 pixel ) 
        => (0.2126 * pixel.R + 0.7152 * pixel.G + 0.0722 * pixel.B) / 255.0;

    public static int NearestPowerOf2(this double x)
        =>  (int)Math.Pow( 2, Math.Round( Math.Log( x ) / Math.Log( 2 ) ) );
}

public static class Painter
{
    public static void DrawCuttingLine(
        Image<Rgba32> sourceImage,
        IReadOnlyList<TextBlock> textBlocks,
        Color knifeColor )
    {
        int lastBottomY = 0;
        foreach ( var block in textBlocks ) {
            var midY = lastBottomY + ( block.TopY - lastBottomY ) / 2;
            DrawHLine( sourceImage, midY, knifeColor );
            lastBottomY = block.BottomY;
        }
    }

    public static void FillGaps(
        Image<Rgba32> sourceImage,
        IReadOnlyList<TextBlock> textBlocks,
        Color knifeColor )
    {
        int yy = 0;
        foreach ( var block in textBlocks ) {
            for ( int y = block.TopY; y >= yy; y-- )
                DrawHLine( sourceImage, y, knifeColor );
            yy = block.BottomY;
        }
    }

    public static void DrawHLine(
        Image<Rgba32> sourceImage,
        int y,
        Color color )
    {
        Span<Rgba32> pixelRowSpan = sourceImage.GetPixelRowSpan( y );
        for ( int x = 0; x < sourceImage.Width; x++ )
            pixelRowSpan[x] = color;
    }
}

#endregion