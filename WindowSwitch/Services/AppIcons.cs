using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace WindowSwitch.Services;

public static class AppIcons
{
    public const string RelativeIconPath = "Assets/AppIcon.ico";

    private static readonly Uri PackIconUri = new($"pack://application:,,,/{RelativeIconPath}", UriKind.Absolute);

    private static ImageSource? _imageSource;

    public static ImageSource ImageSource => _imageSource ??= CreateImageSource();

    public static void ApplyTo(Window window)
    {
        window.Icon = ImageSource;
    }

    public static Drawing.Icon CreateNotifyIcon()
    {
        return new Drawing.Icon(GetFilePath());
    }

    private static ImageSource CreateImageSource()
    {
        var source = BitmapFrame.Create(PackIconUri);
        source.Freeze();
        return source;
    }

    private static string GetFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, RelativeIconPath);
    }
}
