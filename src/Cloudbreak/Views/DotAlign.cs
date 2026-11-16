using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Cloudbreak.Views;

/// <summary>
/// Centers a status dot on the visible glyphs of a text, not on its line box.
/// A line box includes room for descenders, so plain vertical centering puts the dot too low.
/// </summary>
public static class DotAlign
{
    public static void Apply(FrameworkElement dot, TextBlock text, string sample)
    {
        var face = new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch);
        var ft = new FormattedText(sample, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face,
            text.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(text).PixelsPerDip);
        var ink = ft.BuildGeometry(new Point(0, 0)).Bounds;
        if (ink.IsEmpty) return;
        double center = ink.Top + ink.Height / 2;
        double h = double.IsNaN(dot.Height) ? dot.ActualHeight : dot.Height;
        dot.VerticalAlignment = VerticalAlignment.Top;
        dot.Margin = new Thickness(dot.Margin.Left, Math.Max(0, center - h / 2), dot.Margin.Right, 0);
    }
}
