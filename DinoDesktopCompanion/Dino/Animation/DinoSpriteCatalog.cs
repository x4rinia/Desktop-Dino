using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DinoDesktopCompanion.Dino.States;
using DinoDesktopCompanion.Services;

namespace DinoDesktopCompanion.Dino.Animation;

public sealed class DinoSpriteCatalog
{
    private readonly Dictionary<string, SpriteSequenceDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly FileLogger _logger;
    
    private System.Windows.Media.Color _baseColor = System.Windows.Media.Color.FromRgb(120, 158, 91);
    private System.Windows.Media.Color _shadowColor = System.Windows.Media.Color.FromRgb(80, 109, 64);
    private System.Windows.Media.Color _highlightColor = System.Windows.Media.Color.FromRgb(102, 142, 78);
    private bool _hasCustomSkin = false;

    public DinoSpriteCatalog(FileLogger logger)
    {
        _logger = logger;
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Sprites", "sprites.json");
            if (File.Exists(path)) _definitions = JsonSerializer.Deserialize<Dictionary<string, SpriteSequenceDefinition>>(File.ReadAllText(path)) ?? _definitions;
        }
        catch (Exception ex) { _logger.Error("Sprite-Definitionen konnten nicht geladen werden.", ex); }
    }

    public void SetSkin(Customization.SkinDefinition skin)
    {
        try
        {
            var baseC = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(skin.BaseColor);
            var shadowC = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(skin.AccentColor);
            var highlightC = string.IsNullOrEmpty(skin.SecondaryColor) ? baseC : (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(skin.SecondaryColor);
            
            if (baseC == _baseColor && shadowC == _shadowColor && highlightC == _highlightColor) return;
            
            _baseColor = baseC; _shadowColor = shadowC; _highlightColor = highlightC;
            _hasCustomSkin = skin.BaseColor != "#789E5B" || skin.Effect != Customization.EffectType.None;
            _cache.Clear();
        }
        catch { }
    }

    public (IReadOnlyList<ImageSource> Frames, int FrameDurationMs, bool Loop) Get(DinoState state)
    {
        if (!_definitions.TryGetValue(state.ToString(), out var sequence))
        {
            if (state != DinoState.Idle) return Get(DinoState.Idle);
            return ([], 220, false);
        }
        var frames = new List<ImageSource>();
        foreach (var relativePath in sequence.Frames)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) continue;
                if (!_cache.TryGetValue(path, out var image))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(path, UriKind.Absolute); bitmap.EndInit();
                    if (_hasCustomSkin && bitmap.Format == PixelFormats.Bgra32)
                    {
                        var wb = new WriteableBitmap(bitmap);
                        wb.Lock();
                        unsafe
                        {
                            var ptr = (byte*)wb.BackBuffer;
                            for (var i = 0; i < wb.PixelWidth * wb.PixelHeight * 4; i += 4)
                            {
                                var b = ptr[i]; var g = ptr[i + 1]; var r = ptr[i + 2]; var a = ptr[i + 3];
                                if (a > 0)
                                {
                                    if (r == 120 && g == 158 && b == 91) { ptr[i] = _baseColor.B; ptr[i + 1] = _baseColor.G; ptr[i + 2] = _baseColor.R; }
                                    else if (r == 80 && g == 109 && b == 64) { ptr[i] = _shadowColor.B; ptr[i + 1] = _shadowColor.G; ptr[i + 2] = _shadowColor.R; }
                                    else if (r == 102 && g == 142 && b == 78) { ptr[i] = _highlightColor.B; ptr[i + 1] = _highlightColor.G; ptr[i + 2] = _highlightColor.R; }
                                }
                            }
                        }
                        wb.AddDirtyRect(new System.Windows.Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
                        wb.Unlock();
                        wb.Freeze();
                        _cache[path] = image = wb;
                    }
                    else
                    {
                        bitmap.Freeze();
                        _cache[path] = image = bitmap;
                    }
                }
                frames.Add(image);
            }
            catch (Exception ex) { _logger.Error($"Sprite {relativePath} konnte nicht geladen werden.", ex); }
        }
        if (frames.Count == 0 && state != DinoState.Idle) return Get(DinoState.Idle);
        return (frames, Math.Max(80, sequence.FrameDurationMs), sequence.Loop);
    }
}
