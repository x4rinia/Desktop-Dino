using System.Windows.Controls;
using DinoDesktopCompanion.Dino.States;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DinoDesktopCompanion.Customization;

namespace DinoDesktopCompanion.UI;

public partial class DinoVisual : System.Windows.Controls.UserControl
{
    private SkinDefinition? _skin;

    public DinoVisual() => InitializeComponent();

    /// <summary>Spiegelt nur Dino und Zubehör; Größen- und Zustandsanimationen des Controls bleiben unberührt.</summary>
    public bool IsFacingLeft { get; private set; }

    public void SetFacingLeft(bool facingLeft)
    {
        IsFacingLeft = facingLeft;
        FacingTransform.ScaleX = facingLeft ? -1 : 1;
    }

    public void SetSprite(ImageSource? source)
    {
        SpriteImage.Source = source;
        SpriteImage.Visibility = source is null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        AccessoryCanvas.Visibility = SpriteImage.Visibility;
        UpdateSkinMask();
    }

    public void ApplySkin(SkinDefinition skin)
    {
        _skin = skin;
        SkinOverlaysGrid.Visibility = System.Windows.Visibility.Collapsed;
        SkinBaseOverlay.Fill = null;
        SkinGradientOverlay.Fill = null;
        SkinPatternOverlay.Fill = null;
        SkinGlowOverlay.Fill = null;
        SkinParticleCanvas.Children.Clear();

        if (skin.Id == "standard") return;

        // Base color / Default 2-color gradient
        var baseColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(skin.BaseColor)!;
        var accentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(skin.AccentColor ?? skin.BaseColor)!;
        
        SkinBaseOverlay.Fill = new SolidColorBrush(baseColor);
        SkinBaseOverlay.Opacity = 0.38;

        if (string.IsNullOrEmpty(skin.GradientType) && skin.Effect != EffectType.Rainbow && skin.Id != "standard")
        {
            var gbrush = new LinearGradientBrush(baseColor, accentColor, new System.Windows.Point(0, 0), new System.Windows.Point(0.8, 1));
            if (skin.Rarity != "Gewöhnlich")
            {
                var rotation = new RotateTransform(0, 0.5, 0.5);
                gbrush.RelativeTransform = rotation;
                rotation.BeginAnimation(RotateTransform.AngleProperty,
                    new DoubleAnimation(-15, 15, TimeSpan.FromSeconds(6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            }
            SkinGradientOverlay.Fill = gbrush;
            SkinGradientOverlay.Opacity = 0.6;
        }

        // Gradient
        // Special Gradients
        if (skin.GradientType == "Rainbow" || skin.Effect == EffectType.Rainbow)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new(Colors.Red, 0), new(Colors.Orange, 0.17), new(Colors.Yellow, 0.34),
                    new(Colors.LimeGreen, 0.51), new(Colors.DeepSkyBlue, 0.68), new(Colors.Blue, 0.84), new(Colors.Violet, 1)
                }
            };
            var rotation = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromSeconds(8)) { RepeatBehavior = RepeatBehavior.Forever });
            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.55;
        }
        else if (skin.GradientType == "Galaxy")
        {
            SkinGradientOverlay.Fill = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromRgb(10, 0, 40), 0),
                    new(System.Windows.Media.Color.FromRgb(150, 20, 120), 0.5),
                    new(System.Windows.Media.Color.FromRgb(30, 80, 200), 1)
                }
            };
            SkinGradientOverlay.Opacity = 0.75;
        }
        else if (skin.GradientType == "Night")
        {
            SkinGradientOverlay.Fill = new LinearGradientBrush(
                System.Windows.Media.Color.FromRgb(5, 10, 30),
                System.Windows.Media.Color.FromRgb(20, 30, 80),
                new System.Windows.Point(0, 0), new System.Windows.Point(0, 1));
            SkinGradientOverlay.Opacity = 0.8;
        }
        else if (skin.GradientType == "Aurora")
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromRgb(0, 255, 180), 0),
                    new(System.Windows.Media.Color.FromRgb(0, 150, 255), 0.4),
                    new(Colors.DeepPink, 0.6), // pinker Strahl
                    new(System.Windows.Media.Color.FromRgb(150, 50, 255), 1)
                }
            };
            var rotation = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromSeconds(2)) { RepeatBehavior = RepeatBehavior.Forever });
            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.65;
        }
        else if (skin.GradientType == "Frost")
        {
            SkinBaseOverlay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 170, 190)); // dunkleres blaugrau
            SkinBaseOverlay.Opacity = 0.35;
            
            var brush = new LinearGradientBrush(System.Windows.Media.Color.FromRgb(180, 220, 255), System.Windows.Media.Color.FromRgb(240, 250, 255), new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(0, 50, 150), 1));
            var rotation = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(-15, 15, TimeSpan.FromSeconds(4)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.45;
        }
        else if (skin.GradientType == "Ocean")
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromRgb(150, 255, 200), 0), // Mint
                    new(System.Windows.Media.Color.FromRgb(10, 100, 200), 0.5), // Hellblau
                    new(System.Windows.Media.Color.FromRgb(0, 20, 150), 1) // Royalblau
                }
            };
            var rotation = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(-20, 20, TimeSpan.FromSeconds(5)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.75;
        }
        else if (skin.GradientType == "Forest")
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new(Colors.LimeGreen, 0),
                    new(Colors.SaddleBrown, 0.5),
                    new(Colors.DarkRed, 1)
                }
            };
            var rotation = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(-10, 10, TimeSpan.FromSeconds(6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.65;
        }
        else if (skin.GradientType == "Gold")
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromRgb(255, 230, 100), 0),
                    new(System.Windows.Media.Color.FromRgb(255, 255, 200), 0.5),
                    new(Colors.DarkOrange, 1)
                }
            };
            var rotation = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.5)) { RepeatBehavior = RepeatBehavior.Forever });
            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.55;
        }
        else if (skin.GradientType == "Pearl")
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromRgb(255, 200, 200), 0),
                    new(System.Windows.Media.Color.FromRgb(200, 255, 200), 0.33),
                    new(System.Windows.Media.Color.FromRgb(200, 200, 255), 0.66),
                    new(System.Windows.Media.Color.FromRgb(255, 200, 255), 1)
                }
            };
            var rotation = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromSeconds(2.5)) { RepeatBehavior = RepeatBehavior.Forever });
            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.7;
        }
        else if (skin.GradientType == "Toxic")
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromRgb(15, 15, 15), 0),
                    new(System.Windows.Media.Color.FromRgb(60, 0, 90), 0.5),
                    new(System.Windows.Media.Color.FromRgb(100, 255, 50), 1)
                }
            };
            var rotation = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(-15, 15, TimeSpan.FromSeconds(4)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.75;
        }
        else if (skin.GradientType == "Fire")
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 1), EndPoint = new System.Windows.Point(0, 0),
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromRgb(100, 0, 0), 0),      // dunkelrot (unten)
                    new(System.Windows.Media.Color.FromRgb(200, 0, 0), 0.25),   // rot
                    new(System.Windows.Media.Color.FromRgb(255, 100, 0), 0.5),  // orange
                    new(System.Windows.Media.Color.FromRgb(255, 200, 0), 0.75), // gelb
                    new(System.Windows.Media.Color.FromRgb(255, 255, 150), 1)   // hellgelb (oben)
                }
            };
            var rotation = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(-10, 10, TimeSpan.FromSeconds(3)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.75;
        }

        // Pattern / Glow
        if (skin.PatternType == "Forest")
        {
            SkinPatternOverlay.Fill = new RadialGradientBrush(System.Windows.Media.Color.FromRgb(40, 50, 15), Colors.Transparent) { RadiusX = 0.9, RadiusY = 0.9 };
            SkinPatternOverlay.Opacity = 0.4;
        }

        // Glow / Aura
        if (skin.Glow == "Gold" || skin.Effect == EffectType.Gloss)
        {
            SkinGlowOverlay.Fill = new LinearGradientBrush(Colors.Transparent, System.Windows.Media.Color.FromArgb(100, 255, 255, 255), 45) { Opacity = 0.3 };
            SkinBaseOverlay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 180, 0));
            SkinBaseOverlay.Opacity = 0.45;
        }
        else if (skin.Effect == EffectType.Shimmer)
        {
            SkinGlowOverlay.Fill = new RadialGradientBrush(Colors.White, Colors.Transparent) { Opacity = 0.3 };
        }
        
        if (skin.Rarity == "Legendär")
        {
            var auraBrush = new RadialGradientBrush(accentColor, Colors.Transparent) { Opacity = 0.4 };
            var anim = new DoubleAnimation(0.2, 0.5, TimeSpan.FromSeconds(3)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            auraBrush.BeginAnimation(System.Windows.Media.Brush.OpacityProperty, anim);
            SkinGlowOverlay.Fill = auraBrush;
        }
        
        // Particles
        if (!string.IsNullOrEmpty(skin.SparkleType) || skin.Effect == EffectType.Glitter || skin.Effect == EffectType.Shimmer)
        {
            int count = skin.SparkleType == "Galaxy" ? 45 : skin.SparkleType == "Gold" ? 30 : skin.SparkleType == "Magic" ? 35 : skin.SparkleType == "Fire" ? 25 : skin.SparkleType == "Frost" ? 35 : skin.SparkleType == "Toxic" ? 30 : 20;
            for (int i = 0; i < count; i++)
            {
                System.Windows.Media.Brush pColor = System.Windows.Media.Brushes.White;
                if (skin.SparkleType == "Gold") pColor = System.Windows.Media.Brushes.Gold;
                else if (skin.SparkleType == "Galaxy") pColor = System.Windows.Media.Brushes.LightCyan;
                else if (skin.SparkleType == "Frost") pColor = Random.Shared.NextDouble() > 0.5 ? System.Windows.Media.Brushes.LightCyan : System.Windows.Media.Brushes.White;
                else if (skin.SparkleType == "Fire") pColor = Random.Shared.NextDouble() > 0.5 ? System.Windows.Media.Brushes.Orange : System.Windows.Media.Brushes.Yellow;
                else if (skin.SparkleType == "Toxic") pColor = Random.Shared.NextDouble() > 0.5 ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.MediumPurple;
                else if (skin.SparkleType == "Magic") 
                {
                    var colors = new[] { Colors.HotPink, Colors.Cyan, Colors.Violet, Colors.White, Colors.LimeGreen };
                    pColor = new SolidColorBrush(colors[Random.Shared.Next(colors.Length)]);
                }

                double pSize = skin.SparkleType == "Galaxy" ? Random.Shared.Next(3, 7) : skin.SparkleType == "Frost" ? Random.Shared.Next(15, 35) : skin.SparkleType == "Toxic" ? Random.Shared.Next(3, 8) : Random.Shared.Next(2, 6);
                var el = new System.Windows.Shapes.Ellipse { Width = pSize, Height = pSize, Fill = pColor, Opacity = 0 };
                Canvas.SetLeft(el, Random.Shared.Next(-5, 190));
                
                if (skin.SparkleType == "Fire" || skin.SparkleType == "Toxic")
                {
                    Canvas.SetTop(el, Random.Shared.Next(80, 150));
                    var floatAnim = new DoubleAnimation(Canvas.GetTop(el), Canvas.GetTop(el) - Random.Shared.Next(20, 50), TimeSpan.FromSeconds(Random.Shared.NextDouble() * 1.5 + 1.0)) { RepeatBehavior = RepeatBehavior.Forever };
                    el.BeginAnimation(Canvas.TopProperty, floatAnim);
                }
                else if (skin.SparkleType == "Frost")
                {
                    Canvas.SetTop(el, Random.Shared.Next(-10, 100));
                    var fallAnim = new DoubleAnimation(Canvas.GetTop(el), Canvas.GetTop(el) + Random.Shared.Next(10, 30), TimeSpan.FromSeconds(Random.Shared.NextDouble() * 2.0 + 1.5)) { RepeatBehavior = RepeatBehavior.Forever };
                    el.BeginAnimation(Canvas.TopProperty, fallAnim);
                }
                else
                {
                    Canvas.SetTop(el, Random.Shared.Next(5, 110));
                }
                
                var anim = new DoubleAnimation(0, Random.Shared.NextDouble() * 0.7 + 0.3, TimeSpan.FromSeconds(Random.Shared.NextDouble() * 1.0 + 0.4)) {
                    AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(Random.Shared.NextDouble() * 1.5)
                };
                el.BeginAnimation(System.Windows.UIElement.OpacityProperty, anim);
                SkinParticleCanvas.Children.Add(el);
            }
        }

        UpdateSkinMask();
        SkinOverlaysGrid.Visibility = System.Windows.Visibility.Visible;
    }

    private void UpdateSkinMask()
    {
        SkinOverlaysGrid.OpacityMask = SpriteImage.Source is null
            ? null
            : new ImageBrush(SpriteImage.Source) { Stretch = Stretch.Uniform };
    }

    public void SetAccessory(string slot, ImageSource? source)
    {
        if (slot == "Head") HeadAccImage.Source = source;
        else if (slot == "Face") FaceAccImage.Source = source;
        else if (slot == "Neck") NeckAccImage.Source = source;
        else if (slot == "Back") BackAccImage.Source = source;
    }

    public void ShowState(DinoState state)
    {

        ToolTip = state switch
        {
            DinoState.Sleep or DinoState.SleepLeft or DinoState.SleepRight => "Dino schläft",
            DinoState.Doze => "Dino döst",
            DinoState.Blink => "Dino blinzelt",
            DinoState.TailWag => "Dino wackelt mit dem Schwanz",
            DinoState.Walk => "Dino läuft",
            DinoState.Dig => "Dino gräbt",
            DinoState.Hop => "Dino hüpft",
            DinoState.Sit => "Dino sitzt",
            DinoState.Home => "Dino ist zu Hause",
            _ => "Dino"
        };
    }
}
