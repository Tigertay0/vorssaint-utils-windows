// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors EnergySettings in Sources/Vorssaint/UI/Settings/SettingsView.swift (632-849) and the keep-awake
// global shortcut block of the General page (454-472): session, automation, pause while locked, battery
// protection, the active tray icon, and the shortcut. Closed-lid mode and pointer jiggle are macOS
// workarounds with no Windows counterpart; the menu bar countdown has no tray equivalent (the tooltip
// carries the end time).

using System.Windows;
using System.Windows.Controls;
using Faqra.App.Tray;
using Faqra.Core.Defaults;
using Faqra.Core.KeepAwake;
using Faqra.Core.Localization;
using Faqra.Core.Settings;
using Faqra.Core.Shortcuts;
using Faqra.Core.Tray;
using Wpf.Ui.Controls;

namespace Faqra.App.Settings.Pages;

public sealed class EnergyPage : UserControl
{
    private static readonly KeepAwakeActiveIcon[] Icons = Enum.GetValues<KeepAwakeActiveIcon>();
    private static readonly KeepAwakeIconTint[] Tints = Enum.GetValues<KeepAwakeIconTint>();

    private readonly KeepAwakeStrings _ks = KeepAwakeStrings.For(L10n.Shared.Language);
    private readonly Strings _s = L10n.Shared.S;

    public EnergyPage()
    {
        var page = new StackPanel();
        page.Children.Add(Text(_s.SettingsPageTitles[SettingsPage.Energy], "PageTitle"));

        Header(page, _ks.SessionSection, null);
        page.Children.Add(Picker(SymbolRegular.Timer24, _ks.DefaultDuration, DefaultsKey.DefaultDuration,
            ContextMenuBuilder.DurationMinutes, m => KeepAwakeFormat.DurationTitle(m, _s, _ks), DefaultsSanitizers.DefaultDuration, top: 0));
        page.Children.Add(Toggle(SymbolRegular.Play24, _ks.AutoStart, _ks.AutoStartCaption, DefaultsKey.KeepAwakeAutoStart));
        page.Children.Add(Toggle(SymbolRegular.CursorClick24, _ks.RightClickToggle, _ks.RightClickToggleCaption, DefaultsKey.KeepAwakeRightClickToggle));
        page.Children.Add(Toggle(SymbolRegular.Desktop24, _ks.AllowDisplaySleep, _ks.AllowDisplaySleepCaption, DefaultsKey.KeepAwakeAllowDisplaySleep));

        Header(page, _ks.AutomationSection, _ks.AutomationCaption);
        page.Children.Add(Toggle(SymbolRegular.DesktopArrowRight24, _ks.ExternalDisplayToggle, null, DefaultsKey.KeepAwakeExternalDisplay, top: 0));
        page.Children.Add(Toggle(SymbolRegular.PlugConnected24, _ks.PowerToggle, null, DefaultsKey.KeepAwakeConnectedToPower));
        page.Children.Add(Toggle(SymbolRegular.LockClosed24, _ks.PauseWhenLocked, _ks.PauseWhenLockedCaption, DefaultsKey.KeepAwakePauseWhenLocked, top: 12));

        if (AppServices.Current.Monitor.HasBattery)
        {
            Header(page, _ks.BatteryProtectionSection, _ks.BatteryProtectionCaption);
            page.Children.Add(Picker(SymbolRegular.Battery1024, _ks.BatteryDisableBelow, DefaultsKey.BatteryLimit,
                DefaultsSanitizers.AllowedBatteryLimits, p => p == 0 ? _ks.BatteryNever : $"{p}%", DefaultsSanitizers.BatteryLimit, top: 0));
        }

        Header(page, _s.KeepAwakeTitle, null);
        page.Children.Add(Picker(SymbolRegular.Image24, _ks.ActiveIconLabel, DefaultsKey.KeepAwakeActiveIcon,
            Icons, IconTitle, raw => DefaultsSanitizers.ActiveIcon(raw), v => v.RawValue(), preview: true, top: 0));
        page.Children.Add(Picker(SymbolRegular.Color24, _ks.IconTintLabel, DefaultsKey.KeepAwakeIconTint,
            Tints, TintTitle, raw => DefaultsSanitizers.IconTint(raw), v => v.RawValue(), preview: true));

        Header(page, _ks.GlobalHotkeySection, _ks.HotkeyCaption);
        page.Children.Add(Toggle(SymbolRegular.Keyboard24, _ks.HotkeyToggle, null, DefaultsKey.HotkeyEnabled, top: 0));
        var services = AppServices.Current;
        var hub = FeatureHubStrings.For(L10n.Shared.Language);
        Recorder = new ShortcutRecorder(GlobalShortcutRole.KeepAwake, Store, services.HotKeys, services.FeatureRuntime.IsAvailable,
            role => hub.FeatureTitles[role.Feature()]);
        page.Children.Add(Card(SymbolRegular.KeyCommand24, _s.KeepAwakeTitle, null, Recorder, top: 6));

        Content = page;
    }

    /// <summary>The keep-awake shortcut recorder; exposed for render tests.</summary>
    internal ShortcutRecorder Recorder { get; }

    private static ISettingsStore Store => AppServices.Current.Store;

    private string IconTitle(KeepAwakeActiveIcon icon) => icon switch
    {
        KeepAwakeActiveIcon.Coffee => _ks.ActiveIconCoffee,
        KeepAwakeActiveIcon.Eye => _ks.ActiveIconEye,
        KeepAwakeActiveIcon.Moon => _ks.ActiveIconMoon,
        KeepAwakeActiveIcon.Light => _ks.ActiveIconLight,
        _ => _ks.ActiveIconBrand,
    };

    private string TintTitle(KeepAwakeIconTint tint) => tint switch
    {
        KeepAwakeIconTint.Green => _ks.TintGreen,
        KeepAwakeIconTint.Blue => _ks.TintBlue,
        KeepAwakeIconTint.Purple => _ks.TintPurple,
        KeepAwakeIconTint.Pink => _ks.TintPink,
        KeepAwakeIconTint.None => _ks.TintNone,
        _ => _ks.TintOrange,
    };

    /// <summary>A picker over an integer setting with a fixed set of values.</summary>
    private static UIElement Picker(SymbolRegular icon, string title, string key, IReadOnlyList<int> values, Func<int, string> label, Func<int, int> sanitize, double top = 6)
    {
        var box = new ComboBox { MinWidth = 190 };
        foreach (var value in values)
        {
            box.Items.Add(label(value));
        }
        box.SelectedIndex = values.ToList().IndexOf(sanitize(Store.Int(key)));
        System.Windows.Automation.AutomationProperties.SetName(box, title);
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex >= 0)
            {
                Store.Set(key, values[box.SelectedIndex]);
            }
        };
        return Card(icon, title, null, box, top);
    }

    /// <summary>A picker over a string setting backed by an enum; the icon pickers preview the active tray glyph.</summary>
    private UIElement Picker<T>(SymbolRegular icon, string title, string key, IReadOnlyList<T> values, Func<T, string> label, Func<string?, T> parse, Func<T, string> raw, bool preview, double top = 6)
        where T : struct, Enum
    {
        var box = new ComboBox { MinWidth = 190 };
        foreach (var value in values)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            if (preview)
            {
                row.Children.Add(PreviewImage(value));
            }
            row.Children.Add(new System.Windows.Controls.TextBlock { Text = label(value), VerticalAlignment = VerticalAlignment.Center });
            box.Items.Add(row);
        }
        box.SelectedIndex = values.ToList().IndexOf(parse(Store.String(key)));
        System.Windows.Automation.AutomationProperties.SetName(box, title);
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex >= 0)
            {
                Store.Set(key, raw(values[box.SelectedIndex]));
            }
        };
        return Card(icon, title, null, box, top);
    }

    /// <summary>The tray glyph as it would look while keep awake runs, for the icon and the color choices.</summary>
    private static System.Windows.Controls.Image PreviewImage<T>(T value) where T : struct, Enum
    {
        var light = Wpf.Ui.Appearance.ApplicationThemeManager.GetAppTheme() == Wpf.Ui.Appearance.ApplicationTheme.Light;
        var iconChoice = value is KeepAwakeActiveIcon i ? i : DefaultsSanitizers.ActiveIcon(Store.String(DefaultsKey.KeepAwakeActiveIcon));
        var tintChoice = value is KeepAwakeIconTint t ? t : DefaultsSanitizers.IconTint(Store.String(DefaultsKey.KeepAwakeIconTint));
        return new System.Windows.Controls.Image
        {
            Source = TrayIconBitmap.Render(20, active: true, lightTaskbar: light, iconChoice, tintChoice),
            Width = 20,
            Height = 20,
            Margin = new Thickness(0, 0, 8, 0),
        };
    }

    private static UIElement Toggle(SymbolRegular icon, string title, string? caption, string key, double top = 6)
    {
        var toggle = new ToggleSwitch { IsChecked = Store.Bool(key) };
        System.Windows.Automation.AutomationProperties.SetName(toggle, title);
        toggle.Click += (_, _) => Store.Set(key, toggle.IsChecked == true);
        return Card(icon, title, caption, toggle, top);
    }

    private static CardControl Card(SymbolRegular icon, string title, string? caption, UIElement control, double top)
    {
        var header = new StackPanel();
        header.Children.Add(Text(title, "Body"));
        if (caption is not null)
        {
            var hint = Text(caption, "Caption");
            hint.TextWrapping = TextWrapping.Wrap;
            header.Children.Add(hint);
        }
        return new CardControl
        {
            Icon = new SymbolIcon(icon),
            Header = header,
            Content = control,
            Margin = new Thickness(0, top, 0, 0),
        };
    }

    private static void Header(StackPanel page, string title, string? caption)
    {
        page.Children.Add(Text(title, "SectionHeader"));
        if (caption is not null)
        {
            var hint = Text(caption, "Caption");
            hint.Margin = new Thickness(0, -4, 0, 8);
            page.Children.Add(hint);
        }
    }

    private static System.Windows.Controls.TextBlock Text(string text, string style) =>
        new() { Text = text, Style = (Style)Application.Current.Resources[style] };
}
