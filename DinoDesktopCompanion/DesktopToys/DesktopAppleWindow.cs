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
        Width = 120;
        Height = 120;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Cursor = System.Windows.Input.Cursors.Hand;
        ToolTip = "Linksklick: Apfel ablegen · Rechtsklick: Abbrechen";

        _image = new System.Windows.Controls.Image
        {
            Source = LoadImage("apple.png"),
            Stretch = Stretch.Uniform,
            Margin = new Thickness(5),
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1),
            SnapsToDevicePixels = true
        };
                var grid = new System.Windows.Controls.Grid();
        var canvas = new System.Windows.Controls.Canvas();
        grid.Children.Add(_image);
        grid.Children.Add(canvas);
        Content = grid;
        _placementTimer.Tick += (_, _) => FollowCursor();
        Loaded += (_, _) => { this.CaptureMouse(); };
        MouseLeftButtonUp += (_, e) =>
        {
            if (!_isPlacing) return;
            e.Handled = true;
            this.ReleaseMouseCapture();
            _isPlacing = false;
            _placementTimer.Stop();
            Cursor = System.Windows.Input.Cursors.Arrow;
            ToolTip = "Apfel";
            PlacementConfirmed?.Invoke(this, EventArgs.Empty);
        };
        MouseRightButtonUp += (_, e) =>
        {
            if (!_isPlacing) return;
            this.ReleaseMouseCapture();
            e.Handled = true;
            _placementTimer.Stop();
            PlacementCancelled?.Invoke(this, EventArgs.Empty);
            Close();
        };
        Closed += (_, _) => _placementTimer.Stop();
    }

    public void SetImage(string filename)
    {
        _image.Source = LoadImage(filename);
    }
    
    public System.Windows.Media.ImageSource ImageSource => _image.Source;

    public void BeginPlacement()
    {
        FollowCursor();
        Show();
        _placementTimer.Start();
    }

        public void BeginWashAnimation(TimeSpan duration, Window dino)
    {
        _isPlacing = false;
        _placementTimer.Stop();
        Topmost = true; // Sponge goes over Dino
        IsHitTestVisible = false;
        ToolTip = null;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        var startTime = DateTime.Now;
        var centerX = dino.Left + dino.Width / 2;
        var centerY = dino.Top + dino.Height / 2;

        timer.Tick += (s, e) =>
        {
            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            if (elapsed > duration.TotalSeconds)
            {
                timer.Stop();
                return;
            }
            // orbit around dino
            var angle = elapsed * Math.PI * 2; // 1 revolution per second
            var radiusX = dino.Width / 2 * 0.8;
            var radiusY = dino.Height / 2 * 0.6;
            
            var x = centerX + Math.Cos(angle) * radiusX - Width / 2;
            var y = centerY + Math.Sin(angle * 2) * radiusY - Height / 2; // Lissajous figure for scrubbing motion
            
            Left = x;
            Top = y;

            if (_image.RenderTransform is ScaleTransform scale)
            {
                // gentle pulsing
                scale.ScaleX = 1 + Math.Sin(elapsed * 10) * 0.1;
                scale.ScaleY = 1 + Math.Sin(elapsed * 10) * 0.1;
            }

            // Spawn bubbles
            if (Random.Shared.NextDouble() < 0.2)
            {
                var grid = Content as System.Windows.Controls.Grid;
                var canvas = grid?.Children[1] as System.Windows.Controls.Canvas;
                if (canvas != null)
                {
                    var bubble = new System.Windows.Shapes.Ellipse
                    {
                        Width = 10,
                        Height = 10,
                        Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 200, 230, 255)),
                        Stroke = System.Windows.Media.Brushes.White,
                        StrokeThickness = 1
                    };
                    System.Windows.Controls.Canvas.SetLeft(bubble, Width / 2 + Random.Shared.Next(-20, 20));
                    System.Windows.Controls.Canvas.SetTop(bubble, Height / 2 + Random.Shared.Next(-20, 20));
                    canvas.Children.Add(bubble);

                    var btimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                    var bstart = DateTime.Now;
                    btimer.Tick += (bs, be) =>
                    {
                        var bElapsed = (DateTime.Now - bstart).TotalSeconds;
                        if (bElapsed > 1.5)
                        {
                            btimer.Stop();
                            canvas.Children.Remove(bubble);
                            return;
                        }
                        System.Windows.Controls.Canvas.SetTop(bubble, System.Windows.Controls.Canvas.GetTop(bubble) - 1);
                        bubble.Width = 10 + bElapsed * 15;
                        bubble.Height = 10 + bElapsed * 15;
                        bubble.Opacity = 1 - (bElapsed / 1.5);
                    };
                    btimer.Start();
                }
            }
        };
        timer.Start();
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

    private static BitmapImage? LoadImage(string filename)
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Desktop", "Toys", filename);
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
