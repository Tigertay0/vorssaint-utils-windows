// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the reusable pieces of Sources/Vorssaint/UI/MenuPanel/MixerSection.swift: EditableVolumePercent
// (883-962) and the app icon lookup (ResponsibleProcess.icon, 32 pt). The palette seam lets the island
// reuse the same section on its fixed black surface.

using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.App.Island.Modules;
using Faqra.Core.Mixer;
using Faqra.Win32.Icons;

namespace Faqra.App.Panel.Sections;

/// <summary>Color roles, resolved from the Fluent theme in the panel and from the fixed island palette on the island.</summary>
internal enum PaletteRole { Primary, Secondary, Tertiary, Accent, Success, Critical, Fill }

internal sealed class SectionPalette
{
    private readonly bool _island;

    private SectionPalette(bool island) => _island = island;

    public static SectionPalette Panel { get; } = new(island: false);

    public static SectionPalette Island { get; } = new(island: true);

    public bool IsIsland => _island;

    public void Apply(FrameworkElement element, DependencyProperty property, PaletteRole role)
    {
        if (_island)
        {
            element.SetValue(property, role switch
            {
                PaletteRole.Primary => IslandPalette.Primary,
                PaletteRole.Secondary => IslandPalette.Secondary,
                PaletteRole.Tertiary => IslandPalette.Tertiary,
                PaletteRole.Accent => IslandPalette.Accent,
                PaletteRole.Fill => IslandPalette.Fill,
                PaletteRole.Success => IslandSuccess,
                _ => IslandCritical,
            });
            return;
        }
        element.SetResourceReference(property, role switch
        {
            PaletteRole.Primary => PanelBrushes.Primary,
            PaletteRole.Secondary => PanelBrushes.Secondary,
            PaletteRole.Tertiary => PanelBrushes.Tertiary,
            PaletteRole.Accent => PanelBrushes.Accent,
            PaletteRole.Fill => PanelBrushes.Subtle,
            PaletteRole.Success => PanelBrushes.Success,
            _ => PanelBrushes.Critical,
        });
    }

    public TextBlock Label(string text, double size = 12, PaletteRole role = PaletteRole.Secondary, FontWeight? weight = null)
    {
        var block = PanelText.Label(text, size, PanelBrushes.Secondary, weight);
        Apply(block, TextBlock.ForegroundProperty, role);
        return block;
    }

    public TextBlock Value(string text, double size = 12, PaletteRole role = PaletteRole.Primary)
    {
        var block = PanelText.Value(text, size, PanelBrushes.Primary, FontWeights.Normal);
        Apply(block, TextBlock.ForegroundProperty, role);
        return block;
    }

    public TextBlock Glyph(string glyph, double size = 12, PaletteRole role = PaletteRole.Secondary)
    {
        var block = PanelText.Glyph(glyph, size, PanelBrushes.Secondary);
        Apply(block, TextBlock.ForegroundProperty, role);
        return block;
    }

    public Border Divider()
    {
        var line = SectionKit.Divider();
        if (_island)
        {
            line.Background = IslandPalette.Fill;
        }
        return line;
    }

    private static readonly Brush IslandSuccess = Frozen(Color.FromRgb(0x6C, 0xCB, 0x5F));
    private static readonly Brush IslandCritical = Frozen(Color.FromRgb(0xFF, 0x99, 0xA4));

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

/// <summary>A borderless glyph button with a hover fill, for mute and reset. Named for screen readers.</summary>
internal sealed class GlyphButton : Button
{
    private readonly TextBlock _glyph;

    public GlyphButton(string glyph, string name, SectionPalette palette, double size = 12)
    {
        _glyph = palette.Glyph(glyph, size);
        Content = _glyph;
        Width = 24;
        Height = 24;
        Padding = new Thickness(0);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        Cursor = Cursors.Hand;
        Focusable = true;
        System.Windows.Automation.AutomationProperties.SetName(this, name);
        Template = BuildTemplate(palette);
    }

    public void Set(string glyph, PaletteRole role, SectionPalette palette, string name)
    {
        _glyph.Text = glyph;
        palette.Apply(_glyph, TextBlock.ForegroundProperty, role);
        System.Windows.Automation.AutomationProperties.SetName(this, name);
    }

    private static ControlTemplate BuildTemplate(SectionPalette palette)
    {
        var template = new ControlTemplate(typeof(Button));
        var chrome = new FrameworkElementFactory(typeof(Border), "Chrome");
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        chrome.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, new TemplateBindingExtension(HorizontalContentAlignmentProperty));
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetValue(MarginProperty, new TemplateBindingExtension(PaddingProperty));
        chrome.AppendChild(presenter);
        template.VisualTree = chrome;
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(palette.IsIsland
            ? new Setter(Border.BackgroundProperty, IslandPalette.Fill, "Chrome")
            : new Setter(Border.BackgroundProperty, new DynamicResourceExtension(PanelBrushes.Subtle), "Chrome"));
        template.Triggers.Add(hover);
        return template;
    }
}

/// <summary>
/// A percent reading that turns into a selected text field when clicked, so the next keystroke replaces
/// it. Enter commits (clamped), an unreadable value beeps and stays open, Esc or leaving cancels.
/// </summary>
internal sealed class PercentField : Grid
{
    private readonly int _maximumPercent;
    private readonly Action<double> _commit;
    private readonly Button _reading;
    private readonly TextBlock _readingText;
    private readonly TextBox _editor;
    private int _percent;

    public PercentField(double width, int maximumPercent, SectionPalette palette, string name, Action<double> commit)
    {
        _maximumPercent = maximumPercent;
        _commit = commit;
        Width = width;
        Height = 22;

        _readingText = palette.Value("100%", 12, PaletteRole.Secondary);
        _readingText.HorizontalAlignment = HorizontalAlignment.Right;
        _reading = new Button
        {
            Content = _readingText,
            HorizontalContentAlignment = HorizontalAlignment.Right,
            Cursor = Cursors.IBeam,
            Template = PlainTemplate(),
        };
        System.Windows.Automation.AutomationProperties.SetName(_reading, name);
        _reading.Click += (_, _) => BeginEditing();

        _editor = new TextBox
        {
            Visibility = Visibility.Collapsed,
            TextAlignment = TextAlignment.Right,
            FontSize = 12,
            Padding = new Thickness(2, 0, 2, 0),
            MinHeight = 0,
            MinWidth = 0,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _editor.SetResourceReference(Control.FontFamilyProperty, "FaqraMonoFont");
        System.Windows.Automation.AutomationProperties.SetName(_editor, name);
        _editor.PreviewKeyDown += OnEditorKey;
        _editor.LostKeyboardFocus += (_, _) => EndEditing();

        Children.Add(_reading);
        Children.Add(_editor);
    }

    public bool IsEditing => _editor.Visibility == Visibility.Visible;

    public void Set(double volume)
    {
        _percent = (int)Math.Round(volume * 100);
        _readingText.Text = $"{_percent}%";
    }

    private void BeginEditing()
    {
        _editor.Text = _percent.ToString(System.Globalization.CultureInfo.CurrentCulture);
        _editor.Visibility = Visibility.Visible;
        _reading.Visibility = Visibility.Hidden;
        // The box only takes focus once it is laid out, so focus after the layout pass.
        Dispatcher.BeginInvoke(() =>
        {
            _editor.Focus();
            _editor.SelectAll();
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnEditorKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (MixerRoutingSupport.VolumeFraction(_editor.Text, _maximumPercent) is { } fraction)
            {
                EndEditing();
                _commit(fraction);
            }
            else
            {
                SystemSounds.Beep.Play();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            EndEditing();
            e.Handled = true;
        }
    }

    private void EndEditing()
    {
        if (!IsEditing)
        {
            return;
        }
        _editor.Visibility = Visibility.Collapsed;
        _reading.Visibility = Visibility.Visible;
    }

    private static ControlTemplate PlainTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        var chrome = new FrameworkElementFactory(typeof(Border));
        chrome.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        chrome.AppendChild(presenter);
        template.VisualTree = chrome;
        return template;
    }
}

/// <summary>Executable icons at the mixer's 32 px, cached per path for the session.</summary>
internal static class AppIcons
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? For(string? path)
    {
        if (path is null)
        {
            return null;
        }
        if (Cache.TryGetValue(path, out var cached))
        {
            return cached;
        }
        ImageSource? image = null;
        using (var icon = ShellIcons.LargeIconFor(path))
        {
            if (icon is not null)
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                image = source;
            }
        }
        Cache[path] = image;
        return image;
    }
}
