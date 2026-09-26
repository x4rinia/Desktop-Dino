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
        FacingRoot.Effect = null;
        SkinOverlaysGrid.Visibility = System.Windows.Visibility.Collapsed;
        SkinBaseOverlay.Fill = null;
        SkinGradientOverlay.Fill = null;
        SkinPatternOverlay.Fill = null;
        SkinGlowOverlay.Fill = null;
        SkinParticleCanvas.Children.Clear();

        if (skin.Id == "standard") return;

        // Konturen
        if (skin.Id == "night")
        {
            FacingRoot.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Color.FromRgb(56, 189, 248),
                BlurRadius = 4.5,
                ShadowDepth = 0,
                Opacity = 0.98
            };
        }
        else if (skin.Id == "rainbow" || skin.GradientType == "Rainbow" || skin.Effect == EffectType.Rainbow)
        {
            FacingRoot.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Color.FromRgb(255, 30, 80),
                BlurRadius = 4.5,
                ShadowDepth = 0,
                Opacity = 0.95
            };
        }

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
            SkinBaseOverlay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 20, 50));
            SkinBaseOverlay.Opacity = 0.28;

            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromRgb(255, 0, 40), 0.0),
                    new(System.Windows.Media.Color.FromRgb(255, 30, 60), 0.16),
                    new(System.Windows.Media.Color.FromRgb(255, 120, 0), 0.28),
                    new(System.Windows.Media.Color.FromRgb(255, 220, 0), 0.40),
                    new(System.Windows.Media.Color.FromRgb(0, 230, 80), 0.52),
                    new(System.Windows.Media.Color.FromRgb(0, 195, 255), 0.64),
                    new(System.Windows.Media.Color.FromRgb(30, 75, 255), 0.76),
                    new(System.Windows.Media.Color.FromRgb(165, 25, 245), 0.88),
                    new(System.Windows.Media.Color.FromRgb(255, 0, 40), 1.0)
                }
            };
            var rotation = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromSeconds(6.5)) { RepeatBehavior = RepeatBehavior.Forever });
            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.82;

            SkinGlowOverlay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 255, 25, 60));
            SkinGlowOverlay.Opacity = 0.5;
        }
        else if (skin.GradientType == "Galaxy")
        {
            SkinBaseOverlay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 6, 32));
            SkinBaseOverlay.Opacity = 0.35;

            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromRgb(10, 3, 30), 0.0),
                    new(System.Windows.Media.Color.FromRgb(155, 20, 130), 0.28),
                    new(System.Windows.Media.Color.FromRgb(35, 70, 200), 0.52),
                    new(System.Windows.Media.Color.FromRgb(0, 200, 165), 0.74),
                    new(System.Windows.Media.Color.FromRgb(12, 5, 38), 1.0)
                }
            };
            var rotation = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(-18, 18, TimeSpan.FromSeconds(6.5)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.80;

            SkinGlowOverlay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 0, 205, 175));
            SkinGlowOverlay.Opacity = 0.5;
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
            SkinBaseOverlay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 40, 55));
            SkinBaseOverlay.Opacity = 0.16;

            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(0.35, 1),
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromRgb(0, 190, 220), 0.0),
                    new(System.Windows.Media.Color.FromRgb(10, 255, 145), 0.20),
                    new(System.Windows.Media.Color.FromRgb(85, 255, 205), 0.40),
                    new(System.Windows.Media.Color.FromRgb(170, 60, 245), 0.62),
                    new(System.Windows.Media.Color.FromRgb(10, 255, 145), 0.82),
                    new(System.Windows.Media.Color.FromRgb(0, 215, 245), 1.0)
                }
            };
            var rotation = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(-16, 16, TimeSpan.FromSeconds(5.5)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.82;

            SkinGlowOverlay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 10, 255, 150));
            SkinGlowOverlay.Opacity = 0.55;
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
            SkinBaseOverlay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 25, 0));
            SkinBaseOverlay.Opacity = 0.25;

            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 1), EndPoint = new System.Windows.Point(0.15, 0),
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromRgb(120, 0, 0), 0.0),
                    new(System.Windows.Media.Color.FromRgb(225, 25, 0), 0.28),
                    new(System.Windows.Media.Color.FromRgb(255, 105, 0), 0.52),
                    new(System.Windows.Media.Color.FromRgb(255, 195, 0), 0.72),
                    new(System.Windows.Media.Color.FromRgb(255, 255, 175), 0.88),
                    new(System.Windows.Media.Color.FromRgb(255, 90, 0), 1.0)
                }
            };
            var transformGroup = new TransformGroup();
            var skew = new SkewTransform(0, 0, 0.5, 1.0);
            var rotate = new RotateTransform(0, 0.5, 0.9);
            transformGroup.Children.Add(skew);
            transformGroup.Children.Add(rotate);
            brush.RelativeTransform = transformGroup;

            skew.BeginAnimation(SkewTransform.AngleXProperty,
                new DoubleAnimation(-15, 15, TimeSpan.FromSeconds(0.85)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            rotate.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(-8, 8, TimeSpan.FromSeconds(1.1)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });

            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.82;

            SkinGlowOverlay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(70, 255, 120, 0));
            SkinGlowOverlay.Opacity = 0.6;
        }
        else if (skin.GradientType == "Crystal")
        {
            SkinBaseOverlay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 110, 180));
            SkinBaseOverlay.Opacity = 0.25;

            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromRgb(255, 95, 175), 0.0),
                    new(System.Windows.Media.Color.FromRgb(255, 185, 230), 0.22),
                    new(System.Windows.Media.Color.FromRgb(56, 189, 248), 0.48),
                    new(System.Windows.Media.Color.FromRgb(37, 99, 235), 0.72),
                    new(System.Windows.Media.Color.FromRgb(244, 114, 182), 1.0)
                }
            };
            var rotation = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.8)) { RepeatBehavior = RepeatBehavior.Forever });
            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.82;

            SkinGlowOverlay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(65, 236, 72, 153));
            SkinGlowOverlay.Opacity = 0.55;
        }
        else if (skin.GradientType == "Emerald")
        {
            SkinBaseOverlay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
            SkinBaseOverlay.Opacity = 0.25;

            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new(System.Windows.Media.Color.FromRgb(5, 150, 105), 0.0),
                    new(System.Windows.Media.Color.FromRgb(16, 185, 129), 0.25),
                    new(System.Windows.Media.Color.FromRgb(163, 230, 53), 0.50),
                    new(System.Windows.Media.Color.FromRgb(250, 204, 21), 0.75),
                    new(System.Windows.Media.Color.FromRgb(5, 150, 105), 1.0)
                }
            };
            var rotation = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(-25, 25, TimeSpan.FromSeconds(3.0)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            SkinGradientOverlay.Fill = brush;
            SkinGradientOverlay.Opacity = 0.82;

            SkinGlowOverlay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 16, 185, 129));
            SkinGlowOverlay.Opacity = 0.55;
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
                else if (skin.SparkleType == "Galaxy") pColor = Random.Shared.NextDouble() > 0.4 ? System.Windows.Media.Brushes.LightCyan : (Random.Shared.NextDouble() > 0.5 ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 215, 175)) : System.Windows.Media.Brushes.White);
                else if (skin.SparkleType == "Aurora" || skin.GradientType == "Aurora")
                {
                    var auroraColors = new[] { Colors.Aquamarine, Colors.SpringGreen, System.Windows.Media.Color.FromRgb(120, 255, 180), Colors.LightCyan, System.Windows.Media.Color.FromRgb(215, 130, 255), Colors.White };
                    pColor = new SolidColorBrush(auroraColors[Random.Shared.Next(auroraColors.Length)]);
                }
                else if (skin.SparkleType == "Crystal" || skin.GradientType == "Crystal")
                {
                    var crystalColors = new[] { Colors.DeepPink, Colors.LightSkyBlue, Colors.White, System.Windows.Media.Color.FromRgb(255, 180, 230), System.Windows.Media.Color.FromRgb(100, 210, 255) };
                    pColor = new SolidColorBrush(crystalColors[Random.Shared.Next(crystalColors.Length)]);
                }
                else if (skin.SparkleType == "Emerald" || skin.GradientType == "Emerald")
                {
                    var emeraldColors = new[] { Colors.MediumSeaGreen, Colors.Gold, Colors.LightGreen, Colors.Yellow, Colors.White };
                    pColor = new SolidColorBrush(emeraldColors[Random.Shared.Next(emeraldColors.Length)]);
                }
                else if (skin.SparkleType == "Frost") pColor = Random.Shared.NextDouble() > 0.5 ? System.Windows.Media.Brushes.LightCyan : System.Windows.Media.Brushes.White;
                else if (skin.SparkleType == "Fire") pColor = Random.Shared.NextDouble() > 0.5 ? System.Windows.Media.Brushes.Orange : System.Windows.Media.Brushes.Yellow;
                else if (skin.SparkleType == "Toxic") pColor = Random.Shared.NextDouble() > 0.5 ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.MediumPurple;
                else if (skin.SparkleType == "Magic") 
                {
                    var colors = new[] { Colors.Crimson, Colors.Red, Colors.OrangeRed, Colors.Gold, Colors.LimeGreen, Colors.DeepSkyBlue, Colors.Magenta, Colors.Violet };
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
