using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DinoDesktopCompanion.Core;

namespace DinoDesktopCompanion.DesktopDigging;

public partial class DigSiteWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExNoActivate = 0x08000000L;
    private const long WsExToolWindow = 0x00000080L;
    private readonly int _requiredClicks;
    private readonly Func<bool> _canAdvance;
    private readonly Action _showBlockedMessage;
    private int _progress;
    private bool _travelRequested;
    private bool _digging;
    private readonly string _areaName;
    private readonly string _sizeName;
    private DateTime _lastClickTime = DateTime.MinValue;

    public int ProgressValue => _progress;
    public int RequiredProgress => _requiredClicks;

    public event EventHandler? TravelRequested;
    public event EventHandler? DigCompleted;
    public event EventHandler? CancelRequested;

    public DigSiteWindow(int requiredClicks, string sizeName, string areaId, DigSiteVisualType visual, int variantIndex = 0, bool isRare = false, int initialProgress = 0,
        Func<bool>? canAdvance = null, Action? showBlockedMessage = null)
    {
        _requiredClicks = Math.Max(1, requiredClicks);
        _canAdvance = canAdvance ?? (() => true);
        _showBlockedMessage = showBlockedMessage ?? (() => { });
        _progress = Math.Clamp(initialProgress, 0, _requiredClicks);
        _sizeName = sizeName;
        _areaName = areaId switch
        {
            "garten" => "Garten",
            "wald" => "Wald",
            "strand" => "Strand",
            "hoehle" => "Höhle",
            "schneeland" => "Schnee",
            _ => "Gebiet"
        };
        InitializeComponent();
        var visualScale = _requiredClicks <= 5 ? 0.76 : _requiredClicks < 10 ? 0.9 : 1.0;
        DigVisualContainer.RenderTransform = new ScaleTransform(visualScale, visualScale);
        ConfigureAppearance(visual, variantIndex, isRare);
        DigProgress.Maximum = _requiredClicks;
        UpdateProgress();
        SourceInitialized += (_, _) => ApplyNonActivatingOverlayStyle();
        
        Loaded += (_, _) =>
        {
            var overlay = new System.Windows.Shapes.Rectangle { Fill = System.Windows.Media.Brushes.White, IsHitTestVisible = false };
            System.Windows.Controls.Grid.SetRowSpan(overlay, 2);
            ((System.Windows.Controls.Grid)Content).Children.Add(overlay);
            var flash = new System.Windows.Media.Animation.DoubleAnimation { From = 0.8, To = 0.0, Duration = TimeSpan.FromSeconds(1.2), FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop };
            flash.Completed += (s, ev) => ((System.Windows.Controls.Grid)Content).Children.Remove(overlay);
            overlay.BeginAnimation(OpacityProperty, flash);
        };
    }

    public void BeginDigging()
    {
        if (_progress >= _requiredClicks) return;
        _digging = true;
        DigButton.IsEnabled = true;
        DigButton.Content = "Mithelfen!";
        UpdateProgress();
    }

    public void AdvancePassive(int amount)
    {
        if (_digging && _canAdvance()) Advance(amount);
    }

    public void ShowCompletion(string message, DinoDesktopCompanion.Collections.AlbumEntryDefinition? foundItem = null)
    {
        _digging = false;
        DigButton.IsEnabled = false;
        DigProgress.Value = _requiredClicks;
        DigLabel.Text = $"{_sizeName} · Fertig!";
        ToolTip = message;
        
        if (foundItem is not null)
        {
            StatusPanel.Visibility = Visibility.Collapsed;
            DigVisualContainer.Visibility = Visibility.Collapsed;
            FoundItemOverlay.Visibility = Visibility.Visible;
            FoundItemName.Text = foundItem.Name;
            FoundItemRarity.Text = foundItem.Rarity;
            
            var color = foundItem.Rarity.ToLowerInvariant() switch
            {
                "ungewöhnlich" => "#2E7D32",
                "selten" => "#1565C0",
                "episch" => "#6A1B9A",
                "legendär" => "#E65100",
                _ => "#555555"
            };
            FoundItemRarity.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color)!;
            
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, foundItem.AssetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(path))
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                FoundItemImage.Source = bmp;
            }
        }
    }

    public void ShowUnavailable(string message)
    {
        _digging = false;
        DigButton.IsEnabled = false;
        DigLabel.Text = "Nicht verfügbar";
        ToolTip = message;
    }

    private void DigButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_travelRequested)
        {
            _travelRequested = true;
            DigButton.IsEnabled = false;
            DigButton.Content = "Kommt …";
            TravelRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_digging)
        {
            if (!_canAdvance()) { _showBlockedMessage(); return; }
            if ((DateTime.Now - _lastClickTime).TotalMilliseconds < 150) return;
            _lastClickTime = DateTime.Now;

            Advance(2);

            var anim = new System.Windows.Media.Animation.DoubleAnimation { From = 1.1, To = 1.0, Duration = TimeSpan.FromMilliseconds(150) };
            DigVisualContainer.RenderTransform = new ScaleTransform(1, 1);
            DigVisualContainer.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            DigVisualContainer.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            DigVisualContainer.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);

            var flash = new System.Windows.Media.Animation.DoubleAnimation { From = 0.6, To = 1.0, Duration = TimeSpan.FromMilliseconds(300) };
            DigSiteImage.BeginAnimation(OpacityProperty, flash);
        }
    }

    private void Advance(int amount)
    {
        if (_progress >= _requiredClicks) return;
        _progress = Math.Min(_requiredClicks, _progress + Math.Max(1, amount));
        UpdateProgress();
        if (_progress >= _requiredClicks) DigCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateProgress()
    {
        DigProgress.Value = _progress;
        DigLabel.Text = $"{_sizeName} · {_progress}/{_requiredClicks} · {(int)Math.Round(_progress * 100d / _requiredClicks)} %";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CancelRequested?.Invoke(this, EventArgs.Empty);

    private void ConfigureAppearance(DigSiteVisualType visual, int variantIndex, bool isRare)
    {
        var assetName = visual switch
        {
            DigSiteVisualType.Earth => "garden.png",
            DigSiteVisualType.MossAndRoots => "forest.png",
            DigSiteVisualType.Sand => "beach.png",
            DigSiteVisualType.StonesAndCrystals => "cave.png",
            DigSiteVisualType.Snow => "snow.png",
            _ => "garden.png"
        };

        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Desktop", "DigSites", assetName);
        if (System.IO.File.Exists(path))
        {
            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 520;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            DigSiteImage.Source = image;
        }

        if (isRare)
        {
            var glow = new System.Windows.Media.Effects.DropShadowEffect { Color = System.Windows.Media.Colors.Gold, BlurRadius = 15, ShadowDepth = 0 };
            DigVisualContainer.Effect = glow;
            
            var pulse = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 10,
                To = 25,
                Duration = TimeSpan.FromSeconds(1.5),
                AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };
            glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, pulse);
        }
        DigButton.ToolTip = $"{_areaName}-Grabungsstelle";
    }

    private void ApplyNonActivatingOverlayStyle()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var currentStyle = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(currentStyle | WsExNoActivate | WsExToolWindow));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr newLong);
}
