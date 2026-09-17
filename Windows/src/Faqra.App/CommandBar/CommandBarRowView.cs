// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the row in Sources/Vorssaint/UI/CommandBar/CommandBarView.swift (lines 498-707): icon slot, title
// with the matched letters in the accent color, subtitle, the answer value or shortcut chip, the Enter
// glyph on the selected row, and the active dot. Fluent owns the look: a subtle rounded selection fill
// and Segoe Fluent Icons instead of SF Symbols.

using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.Core.CommandBar;
using Faqra.Core.Localization;
using Faqra.Win32.Icons;
using Faqra.Win32.Shell;
using Wpf.Ui.Controls;

namespace Faqra.App.CommandBar;

internal static class CommandBarRowView
{
    private const double IconSlot = 30;
    private const double ImageSize = 24;
    private const int IconPixels = 32;

    private static readonly ConcurrentDictionary<string, ImageSource?> IconCache = new();

    public static Border Build(CommandBarRow row, string query, CommandBarStrings bar)
    {
        var isAnswer = row.AnswerValue is not null;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(IconSlot) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });

        grid.Children.Add(IconView(row));

        var text = new StackPanel { Margin = new Thickness(10, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Title(row, query, isAnswer));
        if (!string.IsNullOrEmpty(row.Entry.Subtitle))
        {
            var subtitle = new System.Windows.Controls.TextBlock { Text = row.Entry.Subtitle, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
            subtitle.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            text.Children.Add(subtitle);
        }
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        if (Accessory(row) is { } accessory)
        {
            Grid.SetColumn(accessory, 2);
            grid.Children.Add(accessory);
        }

        // Always laid out, shown only on the selected row, so moving the selection never shifts text.
        var enter = new SymbolIcon(SymbolRegular.ArrowEnterLeft24) { FontSize = 13, Opacity = 0, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        enter.SetResourceReference(Control.ForegroundProperty, "TextFillColorTertiaryBrush");
        enter.Tag = "enter";
        Grid.SetColumn(enter, 3);
        grid.Children.Add(enter);

        var border = new Border
        {
            Child = grid,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9, isAnswer ? 9 : 6, 9, isAnswer ? 9 : 6),
            Background = Brushes.Transparent,
        };
        System.Windows.Automation.AutomationProperties.SetName(border, AccessibleName(row, bar));
        return border;
    }

    public static void SetSelected(Border row, bool selected)
    {
        if (selected)
        {
            row.SetResourceReference(Border.BackgroundProperty, "SubtleFillColorSecondaryBrush");
        }
        else
        {
            row.Background = Brushes.Transparent;
        }
        if (row.Child is Grid grid)
        {
            foreach (var child in grid.Children.OfType<SymbolIcon>().Where(c => Equals(c.Tag, "enter")))
            {
                child.Opacity = selected ? 1 : 0;
            }
        }
    }

    public static FrameworkElement IconView(CommandBarRow row)
    {
        var slot = new Grid { Width = IconSlot, Height = IconSlot, VerticalAlignment = VerticalAlignment.Center };
        switch (row.Icon)
        {
            case CommandBarIcon.Symbol symbol:
                // Glyphs sit on a plate; app and file icons already carry their own shape (CommandBarView.swift:655-661).
                var plate = new Border { CornerRadius = new CornerRadius(6) };
                plate.SetResourceReference(Border.BackgroundProperty, row.IsActive ? "AccentFillColorDefaultBrush" : "SubtleFillColorSecondaryBrush");
                var glyph = new SymbolIcon(symbol.Glyph) { FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                glyph.SetResourceReference(Control.ForegroundProperty, row.IsActive ? "TextOnAccentFillColorPrimaryBrush" : "TextFillColorPrimaryBrush");
                plate.Child = glyph;
                slot.Children.Add(plate);
                break;
            default:
                var image = new System.Windows.Controls.Image { Width = ImageSize, Height = ImageSize, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                LoadImage(row.Icon, image);
                slot.Children.Add(image);
                break;
        }
        return slot;
    }

    private static UIElement Title(CommandBarRow row, string query, bool isAnswer)
    {
        var title = new System.Windows.Controls.TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        title.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        if (isAnswer)
        {
            // Answers get the loud treatment: bigger, semibold, tabular digits.
            title.Text = row.Entry.Title;
            title.FontSize = 18;
            title.FontWeight = FontWeights.SemiBold;
            title.SetResourceReference(System.Windows.Controls.TextBlock.FontFamilyProperty, "FaqraMonoFont");
            return title;
        }
        title.FontSize = 14;
        var offsets = string.IsNullOrWhiteSpace(query) ? new HashSet<int>() : CommandBarSearch.HighlightOffsets(row.Entry.Title, query);
        if (offsets.Count == 0)
        {
            title.Text = row.Entry.Title;
            return title;
        }
        // The letters that actually matched, in accent, so the row shows why it is here.
        for (var i = 0; i < row.Entry.Title.Length; i++)
        {
            var run = new Run(row.Entry.Title[i].ToString());
            if (offsets.Contains(i))
            {
                run.FontWeight = FontWeights.SemiBold;
                run.SetResourceReference(TextElement.ForegroundProperty, "AccentTextFillColorPrimaryBrush");
            }
            title.Inlines.Add(run);
        }
        return title;
    }

    private static UIElement? Accessory(CommandBarRow row)
    {
        if (row.ShortcutText is { } shortcut)
        {
            var chip = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 2), VerticalAlignment = VerticalAlignment.Center };
            chip.SetResourceReference(Border.BackgroundProperty, "SubtleFillColorSecondaryBrush");
            var label = new System.Windows.Controls.TextBlock { Text = shortcut, FontSize = 11 };
            label.SetResourceReference(System.Windows.Controls.TextBlock.FontFamilyProperty, "FaqraMonoFont");
            label.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            chip.Child = label;
            return chip;
        }
        return null;
    }

    private static string AccessibleName(CommandBarRow row, CommandBarStrings bar)
    {
        var parts = new List<string> { row.Entry.Title };
        if (!string.IsNullOrEmpty(row.Entry.Subtitle))
        {
            parts.Add(row.Entry.Subtitle);
        }
        if (row.IsActive)
        {
            parts.Add(bar.StateOn);
        }
        if (row.ShortcutText is { } shortcut)
        {
            parts.Add(shortcut);
        }
        return string.Join(", ", parts);
    }

    /// <summary>Icons load off the UI thread (the shell can take ~100 ms for one) and fill in when ready.</summary>
    private static void LoadImage(CommandBarIcon icon, System.Windows.Controls.Image image)
    {
        var key = icon switch
        {
            CommandBarIcon.App app => "app:" + app.Installed.ParsingName,
            CommandBarIcon.File file => "file:" + file.Path,
            _ => null,
        };
        if (key is null)
        {
            return;
        }
        if (IconCache.TryGetValue(key, out var cached))
        {
            image.Source = cached;
            return;
        }
        var dispatcher = image.Dispatcher;
        Task.Run(() =>
        {
            var pixels = icon switch
            {
                CommandBarIcon.App app => BitmapPixels.ReadAndDelete(AppsFolder.IconBitmap(app.Installed, IconPixels)),
                CommandBarIcon.File file => FileIconPixels(file.Path),
                _ => null,
            };
            dispatcher.BeginInvoke(() =>
            {
                ImageSource? source = null;
                if (pixels is not null)
                {
                    var bitmap = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96, PixelFormats.Pbgra32, null, pixels.Bgra, pixels.Width * 4);
                    bitmap.Freeze();
                    source = bitmap;
                }
                IconCache[key] = source;
                image.Source = source;
            });
        });
    }

    private static BitmapPixels? FileIconPixels(string path) =>
        File.Exists(path) || Directory.Exists(path)
            ? BitmapPixels.ReadAndDelete(AppsFolder.ShellIconBitmap(path, IconPixels))
            : null;
}
