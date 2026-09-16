// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the sections grid of Sources/Vorssaint/UI/Notch/NotchView.swift, reached with Ctrl+K.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Faqra.Core.Island;

namespace Faqra.App.Island.Modules;

/// <summary>A grid of the island's visible modules. Choosing one opens it.</summary>
public sealed class SectionPicker : UserControl
{
    public SectionPicker(IReadOnlyList<IslandModule> modules, Action<IslandModule> onChosen)
    {
        var grid = new UniformGrid { Columns = Math.Min(4, Math.Max(2, modules.Count)), VerticalAlignment = VerticalAlignment.Top };
        foreach (var module in modules)
        {
            grid.Children.Add(Tile(module, onChosen));
        }
        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = grid,
        };
    }

    private static UIElement Tile(IslandModule module, Action<IslandModule> onChosen)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
        stack.Children.Add(new TextBlock
        {
            Text = module.Glyph(),
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 18,
            Foreground = IslandPalette.Primary,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        stack.Children.Add(new TextBlock
        {
            Text = Title(module),
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 11,
            Foreground = IslandPalette.Secondary,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        var button = new Button
        {
            Content = stack,
            Height = 88,
            Margin = new Thickness(4),
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
        IslandModule.Notifications => "Notifications",
        IslandModule.Timer => "Timer",
        IslandModule.Camera => "Camera",
        IslandModule.Downloads => "Downloads",
        _ => module.RawValue(),
    };
}
