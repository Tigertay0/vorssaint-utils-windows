// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the sections grid of Sources/Vorssaint/UI/Notch/NotchView.swift, reached with Ctrl+K.
// Opens on transitions.dev's menu-dropdown tokens: 250ms in from a 0.97 scale, 150ms out.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Faqra.Core.Island;

namespace Faqra.App.Island.Modules;

/// <summary>A grid of the island's visible modules. Choosing one opens it.</summary>
public sealed class SectionPicker : UserControl
{
    /// <summary>Wide enough for the longest label, so a tile is a card rather than a sliver.</summary>
    private const double TileWidth = 116;
    private const double TileHeight = 68;
    private const double TileGap = 6;

    public SectionPicker(IReadOnlyList<IslandModule> modules, Action<IslandModule> onChosen)
    {
        // A wrap panel sizes each tile to its content width and flows to the next row, so a short
        // list does not get stretched across the panel the way a uniform grid does.
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        foreach (var module in modules)
        {
            panel.Children.Add(Tile(module, onChosen));
        }

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 2, 0),
            Content = panel,
        };

        RenderTransformOrigin = new Point(0.5, 0);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            return;
        }
        var scale = new ScaleTransform(IslandMotion.DropdownPreScale, IslandMotion.DropdownPreScale);
        RenderTransform = scale;
        var grow = Motion.Double(IslandMotion.DropdownPreScale, 1, IslandMotion.DropdownOpen);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        BeginAnimation(OpacityProperty, Motion.Double(0, 1, IslandMotion.DropdownOpen));
    }

    private static UIElement Tile(IslandModule module, Action<IslandModule> onChosen)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 12, 0),
        };
        row.Children.Add(new TextBlock
        {
            Text = module.Glyph(),
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 16,
            Foreground = IslandPalette.Primary,
            VerticalAlignment = VerticalAlignment.Center,
        });
        row.Children.Add(new TextBlock
        {
            Text = Title(module),
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 12.5,
            Foreground = IslandPalette.Primary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        var button = new Button
        {
            Content = row,
            Width = TileWidth,
            Height = TileHeight,
            Margin = new Thickness(0, 0, TileGap, TileGap),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = IslandPalette.Fill,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Template = MusicModule.RoundButtonTemplate(10),
            ToolTip = $"Ctrl+Alt+{char.ToUpperInvariant(module.ShortcutKey())}",
        };
        button.Click += (_, _) => onChosen(module);
        return button;
    }

    /// <summary>Sentence-case module names. They are the island's own labels, not feature titles.</summary>
    private static string Title(IslandModule module) => module switch
    {
        IslandModule.Controls => "Controls",
        IslandModule.Mixer => "Mixer",
        IslandModule.Music => "Music",
        IslandModule.Clipboard => "Clipboard",
        IslandModule.Captures => "Captures",
        IslandModule.Files => "Files",
        IslandModule.System => "System",
        IslandModule.Tools => "Tools",
        IslandModule.Calendar => "Calendar",
        IslandModule.Notifications => "Alerts",
        IslandModule.Timer => "Timer",
        IslandModule.Camera => "Camera",
        IslandModule.FaqraAgents => "Agents",
        IslandModule.Downloads => "Downloads",
        _ => module.RawValue(),
    };
}
