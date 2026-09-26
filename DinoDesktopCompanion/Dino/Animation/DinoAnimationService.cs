using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DinoDesktopCompanion.Dino.States;
using DinoDesktopCompanion.Services;

namespace DinoDesktopCompanion.Dino.Animation;

public sealed class DinoAnimationService
{
    private readonly Dictionary<string, AnimationDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly FileLogger _logger;

    public DinoAnimationService(FileLogger logger)
    {
        _logger = logger;
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Animations", "animations.json");
            if (File.Exists(path)) _definitions = JsonSerializer.Deserialize<Dictionary<string, AnimationDefinition>>(File.ReadAllText(path)) ?? _definitions;
        }
        catch (Exception ex) { _logger.Error("Animationsdefinitionen konnten nicht geladen werden.", ex); }
    }

    public void Apply(FrameworkElement element, DinoState state, bool enabled, double speed)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.RenderTransform = Transform.Identity;
        if (!enabled || !_definitions.TryGetValue(state.ToString(), out var definition)) return;

        var duration = TimeSpan.FromMilliseconds(definition.DurationMs / Math.Clamp(speed, .5, 2.0));
        // Endliche Bursts vermeiden einen permanenten 60-Hz-Renderloop im stundenlangen Idle-Betrieb.
        var repeat = new RepeatBehavior(Math.Max(1, definition.RepeatCount));
        var group = new TransformGroup();
        var rotate = new RotateTransform();
        var translate = new TranslateTransform();
        group.Children.Add(rotate); group.Children.Add(translate);
        element.RenderTransformOrigin = new System.Windows.Point(.5, .8);
        element.RenderTransform = group;

        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -definition.Bob, duration)
        { AutoReverse = true, RepeatBehavior = repeat, EasingFunction = new SineEase() });
        if (definition.Rotate != 0)
            rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-definition.Rotate, definition.Rotate, duration)
            { AutoReverse = true, RepeatBehavior = repeat, EasingFunction = new SineEase() });
    }
}
