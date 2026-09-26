using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DinoDesktopCompanion.Services;

public static class AppIconService
{
    private static ImageSource? _cachedImage;
    private static string PngPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "dino-app.png");
    private static string IcoPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "dino-app.ico");

    public static ImageSource? LoadImageSource()
    {
        if (_cachedImage is not null) return _cachedImage;
        if (!File.Exists(PngPath)) return null;
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(PngPath, UriKind.Absolute); image.EndInit(); image.Freeze();
        return _cachedImage = image;
    }

    public static Icon LoadDrawingIcon() => File.Exists(IcoPath) ? new Icon(IcoPath) : (Icon)SystemIcons.Application.Clone();
}
