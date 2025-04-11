using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Lunaqua.Components;

public partial class RotatableContentControl : ContentControl
{
    public RotatableContentControl()
    {
        InitializeComponent();
    }


    public static readonly StyledProperty<Control> PortraitContentProperty =
        AvaloniaProperty.Register<RotatableContentControl, Control>(nameof(PortraitContent));

    public Control PortraitContent
    {
        get => GetValue(PortraitContentProperty);
        set => SetValue(PortraitContentProperty, value);
    }

    public static readonly StyledProperty<Control> LandscapeContentProperty =
        AvaloniaProperty.Register<RotatableContentControl, Control>(nameof(LandscapeContent));

    public Control LandscapeContent
    {
        get => GetValue(LandscapeContentProperty);
        set => SetValue(LandscapeContentProperty, value);
    }

    private void ResetContent(TopLevel topLevel)
    {
        if (topLevel.Screens is not { } screens) return;
        var activeScreen = screens.ScreenFromTopLevel(topLevel);
        if (activeScreen is null)
            throw new AvaloniaInternalException("Current platform doesn't support Screens API.");
        LandscapeContent.IsVisible =
            activeScreen.CurrentOrientation is ScreenOrientation.Landscape or ScreenOrientation.LandscapeFlipped;
        PortraitContent.IsVisible =
            activeScreen.CurrentOrientation is ScreenOrientation.Portrait or ScreenOrientation.PortraitFlipped;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel?.Screens is not { } screens) return;
        ResetContent(topLevel);

        if (topLevel is Window w)
        {
            w.PositionChanged += (_, _) =>
            {
                ResetContent(topLevel);
                InvalidateVisual();
            };
        }

        screens.Changed += (_, _) =>
        {
            ResetContent(topLevel);
            Debug.WriteLine("Screens Changed");
            InvalidateVisual();
        };
    }
}