// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Faqra.Core.Agents;

namespace Faqra.App.Island.Modules;

/// <summary>Claude's Markdown as island text: headings, paragraphs, lists, quotes, code blocks and rules, with styled runs.</summary>
internal static class MarkdownView
{
    private const double ListIndent = 14;
    private const double BlockGap = 8;
    private const double ItemGap = 2;

    public static StackPanel Build(string? markdown)
    {
        var panel = new StackPanel();
        var blocks = MarkdownLite.Parse(markdown);
        for (var i = 0; i < blocks.Count; i++)
        {
            var element = Block(blocks[i]);
            if (i > 0)
            {
                var gap = IsItem(blocks[i - 1]) && IsItem(blocks[i]) ? ItemGap : BlockGap;
                element.Margin = new Thickness(element.Margin.Left, gap, 0, 0);
            }
            panel.Children.Add(element);
        }
        return panel;
    }

    /// <summary>Only web links open: a click never runs a file or a script Claude named.</summary>
    internal static Uri? WebLink(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? uri : null;

    private static bool IsItem(MdBlock block) => block.Kind is MdBlockKind.Bullet or MdBlockKind.Numbered;

    private static FrameworkElement Block(MdBlock block) => block.Kind switch
    {
        MdBlockKind.Heading => Text(block.Inlines, block.Level == 1 ? 14 : 13, FontWeights.SemiBold),
        MdBlockKind.Bullet => Item("•", block),
        MdBlockKind.Numbered => Item(block.Marker, block),
        MdBlockKind.Quote => new Border
        {
            BorderBrush = IslandPalette.FillStrong,
            BorderThickness = new Thickness(2, 0, 0, 0),
            Padding = new Thickness(8, 0, 0, 0),
            Child = Text(block.Inlines, 12, FontWeights.Normal, IslandPalette.Secondary),
        },
        MdBlockKind.Code => new Border
        {
            Background = IslandPalette.FillStrong,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 8, 6),
            Child = new TextBlock
            {
                Text = block.Code,
                FontFamily = AgentCardParts.MonoFont,
                FontSize = 11.5,
                Foreground = IslandPalette.Primary,
                TextWrapping = TextWrapping.Wrap,
            },
        },
        MdBlockKind.Rule => new Border { Height = 1, Background = IslandPalette.FillStrong },
        _ => Text(block.Inlines, 12, FontWeights.Normal),
    };

    private static Grid Item(string marker, MdBlock block)
    {
        var grid = new Grid { Margin = new Thickness(block.Level * ListIndent, 0, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var mark = new TextBlock
        {
            Text = marker,
            FontFamily = AgentCardParts.TextFont,
            FontSize = 12,
            Foreground = IslandPalette.Secondary,
            MinWidth = ListIndent,
            Margin = new Thickness(0, 0, 6, 0),
        };
        var text = Text(block.Inlines, 12, FontWeights.Normal);
        Grid.SetColumn(text, 1);
        grid.Children.Add(mark);
        grid.Children.Add(text);
        return grid;
    }

    private static TextBlock Text(IReadOnlyList<MdInline> inlines, double size, FontWeight weight, Brush? ink = null)
    {
        var text = new TextBlock
        {
            FontFamily = AgentCardParts.TextFont,
            FontSize = size,
            FontWeight = weight,
            Foreground = ink ?? IslandPalette.Primary,
            TextWrapping = TextWrapping.Wrap,
        };
        foreach (var inline in inlines)
        {
            text.Inlines.Add(Run(inline));
        }
        return text;
    }

    private static Inline Run(MdInline inline) => inline.Style switch
    {
        MdStyle.Bold => new Run(inline.Text) { FontWeight = FontWeights.SemiBold },
        MdStyle.Italic => new Run(inline.Text) { FontStyle = FontStyles.Italic },
        MdStyle.Code => new Run(inline.Text) { FontFamily = AgentCardParts.MonoFont, Background = IslandPalette.FillStrong },
        MdStyle.Link when WebLink(inline.Url) is { } uri => Link(inline.Text, uri),
        _ => new Run(inline.Text),
    };

    private static Hyperlink Link(string text, Uri uri)
    {
        var link = new Hyperlink(new Run(text)) { NavigateUri = uri, Foreground = IslandPalette.Accent, ToolTip = uri.AbsoluteUri };
        link.RequestNavigate += (_, e) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                // No browser answered; the link stays readable on the card.
                Trace.TraceWarning($"Faqra agents link: {ex.GetType().Name}: {ex.Message}");
            }
            e.Handled = true;
        };
        return link;
    }
}
