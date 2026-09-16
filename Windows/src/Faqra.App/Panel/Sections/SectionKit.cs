// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the shared block list layout of Sources/Vorssaint/UI/MenuPanel/SystemSection.swift (lines 52-70),
// NetworkSection.swift (lines 32-49) and PowerSection.swift (lines 32-54).

using System.Windows;
using System.Windows.Controls;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Metrics;

namespace Faqra.App.Panel.Sections;

/// <summary>One panel section: built once for the current settings, refreshed on every snapshot.</summary>
internal interface IPanelSectionView
{
    UIElement Root { get; }

    void Update(SystemSnapshot snapshot);
}

/// <summary>What a section needs to decide which blocks it shows.</summary>
internal sealed record SectionContext(ISettingsStore Store, Func<AppFeature, bool> IsAvailable);

internal static class SectionKit
{
    /// <summary>Blocks in their saved order, with a divider between each, inside one card.</summary>
    public static UIElement Card(IEnumerable<UIElement> blocks)
    {
        var stack = new StackPanel();
        foreach (var block in blocks)
        {
            if (stack.Children.Count > 0)
            {
                stack.Children.Add(Divider());
            }
            stack.Children.Add(block);
        }
        return new PanelCard(stack);
    }

    public static Border Divider()
    {
        var line = new Border { Height = 1, Margin = new Thickness(0, 10, 0, 10) };
        line.SetResourceReference(Border.BackgroundProperty, "DividerStrokeColorDefaultBrush");
        return line;
    }

    /// <summary>A label on the left and content stretched or pushed to the right.</summary>
    public static Grid Row(UIElement left, UIElement right, double spacing = 8)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (left is FrameworkElement l)
        {
            l.Margin = new Thickness(0, 0, spacing, 0);
        }
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    public static StackPanel Stack(double spacing, params UIElement[] children)
    {
        var stack = new StackPanel();
        foreach (var child in children)
        {
            if (stack.Children.Count > 0 && child is FrameworkElement element)
            {
                element.Margin = new Thickness(element.Margin.Left, element.Margin.Top + spacing, element.Margin.Right, element.Margin.Bottom);
            }
            stack.Children.Add(child);
        }
        return stack;
    }

    /// <summary>The saved block order for a section, keeping only blocks Windows can show.</summary>
    public static IReadOnlyList<string> Order(ISettingsStore store, string key, IReadOnlyList<string> upstreamOrder, IReadOnlySet<string> built) =>
        DefaultsSanitizers.PanelItemOrder(store.String(key), upstreamOrder).Where(built.Contains).ToList();

    public static string Or(double? value, Func<double, string> format, string placeholder) =>
        value is { } v ? format(v) : placeholder;
}
