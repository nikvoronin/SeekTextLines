using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

string sourceImageFilename = "../../../images/ticket02.jpg";
const int ResampledImageWidth = 32;
const double TextMaxBrightness = 0.3;
const float BinaryLuminanceThreshold = 0.3f;

using var inputStream = File.OpenRead(sourceImageFilename);

using Image<Rgba32> sourceImage = (Image<Rgba32>)Image.Load( inputStream );
//int ResampledImageWidth = ( sourceImage.Width / 32.0 ).NearestPowerOf2();

using Image<Rgba32> resampledImage = sourceImage.Clone( x => x
    .Resize( ResampledImageWidth, sourceImage.Height, KnownResamplers.Lanczos8 )
    .HistogramEqualization()
    .BinaryThreshold( BinaryLuminanceThreshold, BinaryThresholdMode.Luminance)
    );


// [yStart, yEnd) yEnd not included in block
List<(int, int)> textLines = new();
SeekState state = SeekState.Start;
for ( int y = 0; y < resampledImage.Height; y++ ) {
    Span<Rgba32> pixelRowSpan = resampledImage.GetPixelRowSpan( y );
    var dark = false;
    var light = true;
    for ( int x = 0; x < resampledImage.Width; x++ ) {
        var bright = pixelRowSpan[x].Brightness();
        Console.WriteLine( bright );
        if ( bright < TextMaxBrightness ) {
            dark = true;
            break;
        }
    }

    light = !dark;

    switch ( state ) {
        case SeekState.Start:
            if ( dark ) {
                textLines.Add( (y, -1) );
                state = SeekState.End;
            }
            break;
        case SeekState.End:
            if ( light ) {
                textLines[^1] = ( textLines[^1].Item1, y);
                state = SeekState.Start;
            }
            break;
    }
}

Console.WriteLine();

if ( state == SeekState.End )
    textLines[^1] = (textLines[^1].Item1, resampledImage.Height);

int yy = 0;
foreach ( var block in textLines ) {
    var y = yy + ( block.Item1 - yy) / 2;
    Span<Rgba32> pixelRowSpan = sourceImage.GetPixelRowSpan( y );
    for ( int x = 0; x < sourceImage.Width; x++ )
        pixelRowSpan[x] = Color.Cyan;
    yy = block.Item2;
}

// Fill gaps
//int yy = 0;
//foreach ( var block in textLines ) {
//    for ( int y = block.Item1; y >= yy; y-- ) {
//        Span<Rgba32> pixelRowSpan = sourceImage.GetPixelRowSpan( y );
//        for ( int x = 0; x < sourceImage.Width; x++ )
//            pixelRowSpan[x] = Color.Cyan;
//    }
//    yy = block.Item2;
//}

using var outStream = File.OpenWrite( sourceImageFilename.Replace(".jpg", ".png") );
sourceImage.Save( outStream, new PngEncoder() );//Replace Png encoder with the file format of choice

using var outStream2 = File.OpenWrite( sourceImageFilename.Replace( ".jpg", "_resampled.png" ) );
resampledImage.Save( outStream2, new PngEncoder() );//Replace Png encoder with the file format of choice

public enum SeekState { Start, End }

public static class Extensions
{
    public static double Brightness( this Rgba32 pixel ) 
        => (0.2126 * pixel.R + 0.7152 * pixel.G + 0.0722 * pixel.B) / 255.0;

    public static int NearestPowerOf2(this double x)
        =>  (int)Math.Pow( 2, Math.Round( Math.Log( x ) / Math.Log( 2 ) ) );
}