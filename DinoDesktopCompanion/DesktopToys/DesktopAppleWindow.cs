using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DinoDesktopCompanion.DesktopToys;

public sealed class DesktopAppleWindow : Window
{
    private readonly DispatcherTimer _placementTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly System.Windows.Controls.Image _image;
    private bool _isPlacing = true;

    public event EventHandler? PlacementConfirmed;
    public event EventHandler? PlacementCancelled;

    public DesktopAppleWindow()
    {
        Width = 84;
        Height = 84;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Cursor = System.Windows.Input.Cursors.Hand;
        ToolTip = "Linksklick: Apfel ablegen · Rechtsklick: Abbrechen";

        _image = new System.Windows.Controls.Image
        {
            Source = LoadAppleImage(),
            Stretch = Stretch.Uniform,
            Margin = new Thickness(5),
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1),
            SnapsToDevicePixels = true
        };
        Content = _image;

        _placementTimer.Tick += (_, _) => FollowCursor();
        MouseLeftButtonUp += (_, e) =>
        {
            if (!_isPlacing) return;
            e.Handled = true;
            _isPlacing = false;
            _placementTimer.Stop();
            Cursor = System.Windows.Input.Cursors.Arrow;
            ToolTip = "Apfel";
            PlacementConfirmed?.Invoke(this, EventArgs.Empty);
        };
        MouseRightButtonUp += (_, e) =>
        {
            if (!_isPlacing) return;
            e.Handled = true;
            _placementTimer.Stop();
            PlacementCancelled?.Invoke(this, EventArgs.Empty);
            Close();
        };
        Closed += (_, _) => _placementTimer.Stop();
    }

    public void BeginPlacement()
    {
        FollowCursor();
        Show();
        _placementTimer.Start();
    }

    public void BeginEatingAnimation(TimeSpan duration)
    {
        _isPlacing = false;
        _placementTimer.Stop();
        Topmost = false;
        IsHitTestVisible = false;
        ToolTip = null;

        var easing = new QuadraticEase { EasingMode = EasingMode.EaseIn };
        var scaleAnimation = new DoubleAnimation(1, 0.35, duration) { EasingFunction = easing };
        var opacityAnimation = new DoubleAnimation(1, 0.15, duration) { EasingFunction = easing };
        if (_image.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }
        _image.BeginAnimation(OpacityProperty, opacityAnimation);
    }

    private void FollowCursor()
    {
        var cursor = Forms.Cursor.Position;
        var screen = Forms.Screen.FromPoint(cursor);
        var dpi = VisualTreeHelper.GetDpi(this);
        var left = cursor.X / dpi.DpiScaleX - Width / 2;
        var top = cursor.Y / dpi.DpiScaleY - Height / 2;
        var workLeft = screen.WorkingArea.Left / dpi.DpiScaleX;
        var workTop = screen.WorkingArea.Top / dpi.DpiScaleY;
        var workRight = screen.WorkingArea.Right / dpi.DpiScaleX;
        var workBottom = screen.WorkingArea.Bottom / dpi.DpiScaleY;
        Left = Math.Clamp(left, workLeft, Math.Max(workLeft, workRight - Width));
        Top = Math.Clamp(top, workTop, Math.Max(workTop, workBottom - Height));
    }

    private static BitmapImage? LoadAppleImage()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Desktop", "Toys", "apple.png");
        if (!System.IO.File.Exists(path)) return null;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 256;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
