using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Unplugged.App.Views;

/// <summary>
/// Adds a "there's more below" hint to a ScrollViewer: a soft fade along the bottom edge plus a
/// gently bouncing "Scroll down for more" button that scrolls when clicked. It hides itself
/// when there's nothing more to see. The ScrollViewer must sit directly inside a Panel.
/// </summary>
public static class ScrollHint
{
    public static void Attach(ScrollViewer sv, Color background, string label = "Scroll down for more  ↓")
    {
        if (sv.Parent is not Panel host) throw new InvalidOperationException("ScrollHint needs the ScrollViewer inside a Panel.");

        var fade = new Border
        {
            Height = 90,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, background.R, background.G, background.B), 0),
                    new GradientStop(Color.FromArgb(235, background.R, background.G, background.B), 0.75),
                    new GradientStop(background, 1),
                },
            },
        };

        var bounce = new TranslateTransform();
        var button = new Button
        {
            Content = label,
            Classes = { "primary" },
            FontSize = 14,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(18, 8),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 12),
            RenderTransform = bounce,
        };
        button.Click += (_, _) =>
        {
            var target = Math.Min(sv.Offset.Y + sv.Viewport.Height * 0.8, Math.Max(0, sv.Extent.Height - sv.Viewport.Height));
            sv.Offset = new Vector(sv.Offset.X, target);
        };

        host.Children.Add(fade);
        host.Children.Add(button);

        // gentle bounce, only while visible
        var start = DateTime.UtcNow;
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render,
            (_, _) => bounce.Y = Math.Abs(Math.Sin((DateTime.UtcNow - start).TotalSeconds * 2.6)) * -5);

        void Update()
        {
            bool more = sv.IsEffectivelyVisible && sv.Extent.Height - sv.Viewport.Height - sv.Offset.Y > 12;
            fade.IsVisible = button.IsVisible = more;
            if (more && !timer.IsEnabled) timer.Start();
            if (!more && timer.IsEnabled) timer.Stop();
        }

        sv.PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrollViewer.OffsetProperty || e.Property == ScrollViewer.ExtentProperty ||
                e.Property == ScrollViewer.ViewportProperty || e.Property == Visual.IsVisibleProperty)
                Update();
        };
        host.PropertyChanged += (_, e) => { if (e.Property == Visual.IsVisibleProperty) Update(); };
        Update();
    }
}
