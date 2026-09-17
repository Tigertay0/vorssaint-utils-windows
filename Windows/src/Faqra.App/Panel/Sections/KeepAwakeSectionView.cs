// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors KeepAwakeCard in Sources/Vorssaint/UI/MenuPanel/MenuPanelView.swift (2444-2845): the status line
// with its switch, +15/+30/+60 min while a timed session runs, the duration picker while idle, and the
// Options disclosure (display sleep, open-at-launch, automation with pause while locked). The icon picker
// lives on the Energy settings page; closed-lid mode and pointer jiggle are not ported.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Faqra.Core.Defaults;
using Faqra.Core.KeepAwake;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;
using Faqra.Core.Tray;
using Faqra.Services.KeepAwake;

namespace Faqra.App.Panel.Sections;

internal sealed class KeepAwakeSectionView : IPanelSectionView, IDisposable
{
    private static readonly int[] ExtendMinutes = [15, 30, 60];
    private const string ChevronRight = "";
    private const string ChevronDown = "";

    private readonly KeepAwakeManager _manager;
    private readonly ISettingsStore _store;
    private readonly SectionPalette _palette;
    private readonly KeepAwakeStrings _ks = KeepAwakeStrings.For(L10n.Shared.Language);
    private readonly Strings _s = L10n.Shared.S;
    private readonly StackPanel _body = new();
    private readonly TextBlock _status;
    private readonly Wpf.Ui.Controls.ToggleSwitch _toggle = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private readonly UIElement _statusRow;
    private readonly DispatcherTimer _countdown = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _optionsOpen;
    private bool _automationOpen;
    private bool _disposed;

    public KeepAwakeSectionView(SectionContext context, KeepAwakeManager manager, SectionPalette palette)
    {
        _manager = manager;
        _store = context.Store;
        _palette = palette;
        _status = palette.Label(string.Empty, 12);
        _status.TextWrapping = TextWrapping.Wrap;
        System.Windows.Automation.AutomationProperties.SetName(_toggle, _s.KeepAwakeTitle);
        _toggle.Click += (_, _) =>
        {
            if (_toggle.IsChecked == true)
            {
                _manager.Session.Activate(DefaultsSanitizers.DefaultDuration(_store.Int(DefaultsKey.DefaultDuration)));
            }
            else if (_manager.Session.IsActive)
            {
                _manager.Session.Toggle();
            }
        };
        _statusRow = SectionKit.Row(_status, _toggle);
        _countdown.Tick += (_, _) => UpdateStatus();
        Root = palette.IsIsland ? _body : new PanelCard(_body);
        _manager.Changed += Rebuild;
        Rebuild();
    }

    public UIElement Root { get; }

    public void Update(SystemSnapshot snapshot)
    {
        // Keep awake has its own source; system samples do not concern it.
    }

    private void Rebuild()
    {
        if (_disposed)
        {
            return;
        }
        var session = _manager.Session;
        _body.Children.Clear();
        _toggle.IsChecked = session.IsActive;
        _body.Children.Add(_statusRow);
        UpdateStatus();

        if (session.IsActive && session.EndsAt is not null)
        {
            _body.Children.Add(ExtendRow());
        }
        if (!session.IsActive)
        {
            _body.Children.Add(DurationRow());
        }
        _body.Children.Add(Options());

        var timed = session.IsActive && session.EndsAt is not null;
        if (timed && !_countdown.IsEnabled)
        {
            _countdown.Start();
        }
        else if (!timed)
        {
            _countdown.Stop();
        }
    }

    private void UpdateStatus()
    {
        var session = _manager.Session;
        _status.Text = !session.IsActive ? _ks.NormalRules
            : session.Trigger == KeepAwakeTrigger.Automation ? KeepAwakeFormat.ActiveStatus(session.ActiveConditions, _ks)
            : session.EndsAt is { } end ? $"{_ks.EndsIn} {KeepAwakeFormat.Remaining((int)(end - DateTime.UtcNow).TotalSeconds)}"
            : _ks.UntilDisabled;
    }

    private UIElement ExtendRow()
    {
        var row = new UniformGrid3();
        foreach (var minutes in ExtendMinutes)
        {
            var button = new Wpf.Ui.Controls.Button
            {
                Content = string.Format(_ks.ExtendFormat, minutes),
                FontSize = 12,
                Padding = new Thickness(8, 3, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(row.Children.Count == 0 ? 0 : 3, 0, row.Children.Count == 2 ? 0 : 3, 0),
            };
            button.Click += (_, _) => _manager.Session.Extend(minutes);
            row.Children.Add(button);
        }
        row.Margin = new Thickness(0, 10, 0, 0);
        return row;
    }

    private UIElement DurationRow()
    {
        var picker = new ComboBox { Width = 150, HorizontalAlignment = HorizontalAlignment.Right };
        var presets = ContextMenuBuilder.DurationMinutes;
        foreach (var minutes in presets)
        {
            picker.Items.Add(KeepAwakeFormat.DurationTitle(minutes, _s, _ks));
        }
        picker.SelectedIndex = presets.ToList().IndexOf(DefaultsSanitizers.DefaultDuration(_store.Int(DefaultsKey.DefaultDuration)));
        System.Windows.Automation.AutomationProperties.SetName(picker, _ks.Duration);
        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedIndex >= 0)
            {
                _store.Set(DefaultsKey.DefaultDuration, presets[picker.SelectedIndex]);
            }
        };
        var row = SectionKit.Row(_palette.Label(_ks.Duration, 12), picker);
        row.Margin = new Thickness(0, 10, 0, 0);
        return row;
    }

    private UIElement Options()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        var chevron = _palette.Glyph(_optionsOpen ? ChevronDown : ChevronRight, 10);
        stack.Children.Add(Disclosure(chevron, _ks.Options, () =>
        {
            _optionsOpen = !_optionsOpen;
            Rebuild();
        }));
        if (!_optionsOpen)
        {
            return stack;
        }

        var content = new StackPanel { Margin = new Thickness(20, 4, 0, 0) };
        content.Children.Add(Switch(_ks.AllowDisplaySleep, DefaultsKey.KeepAwakeAllowDisplaySleep));
        content.Children.Add(Switch(_ks.AutoStart, DefaultsKey.KeepAwakeAutoStart));

        var automationChevron = _palette.Glyph(_automationOpen ? ChevronDown : ChevronRight, 9);
        var summary = _palette.Label(AutomationSummary(), 12, PaletteRole.Tertiary);
        content.Children.Add(Disclosure(automationChevron, _ks.AutomationSection, () =>
        {
            _automationOpen = !_automationOpen;
            Rebuild();
        }, summary));
        if (_automationOpen)
        {
            var automation = new StackPanel { Margin = new Thickness(20, 2, 0, 0) };
            automation.Children.Add(Switch(_ks.ExternalDisplayToggle, DefaultsKey.KeepAwakeExternalDisplay));
            automation.Children.Add(Switch(_ks.PowerToggle, DefaultsKey.KeepAwakeConnectedToPower));
            automation.Children.Add(Switch(_ks.PauseWhenLocked, DefaultsKey.KeepAwakePauseWhenLocked));
            content.Children.Add(automation);
        }
        stack.Children.Add(content);
        return stack;
    }

    /// <summary>"Off", or the names of the automation switches that are on.</summary>
    private string AutomationSummary()
    {
        var on = new List<string>();
        if (_store.Bool(DefaultsKey.KeepAwakeExternalDisplay))
        {
            on.Add(_ks.ExternalDisplayToggle);
        }
        if (_store.Bool(DefaultsKey.KeepAwakeConnectedToPower))
        {
            on.Add(_ks.PowerToggle);
        }
        return on.Count == 0 ? _ks.AutomationOff : string.Join(", ", on);
    }

    private UIElement Switch(string title, string key)
    {
        var toggle = new Wpf.Ui.Controls.ToggleSwitch { IsChecked = _store.Bool(key), HorizontalAlignment = HorizontalAlignment.Right };
        System.Windows.Automation.AutomationProperties.SetName(toggle, title);
        toggle.Click += (_, _) =>
        {
            _store.Set(key, toggle.IsChecked == true);
            Rebuild();
        };
        var label = _palette.Label(title, 12);
        label.TextWrapping = TextWrapping.Wrap;
        var row = SectionKit.Row(label, toggle);
        row.Margin = new Thickness(0, 6, 0, 0);
        return row;
    }

    private FrameworkElement Disclosure(TextBlock chevron, string title, Action toggle, TextBlock? trailing = null)
    {
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(chevron);
        var label = _palette.Label(title, 12, PaletteRole.Secondary, FontWeights.SemiBold);
        Grid.SetColumn(label, 1);
        content.Children.Add(label);
        if (trailing is not null)
        {
            Grid.SetColumn(trailing, 2);
            content.Children.Add(trailing);
        }
        var button = new GlyphButton(string.Empty, title, _palette)
        {
            Content = content,
            Width = double.NaN,
            Height = 28,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        button.Click += (_, _) => toggle();
        return button;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _countdown.Stop();
        _manager.Changed -= Rebuild;
    }

    /// <summary>Three equal columns.</summary>
    private sealed class UniformGrid3 : System.Windows.Controls.Primitives.UniformGrid
    {
        public UniformGrid3()
        {
            Rows = 1;
            Columns = 3;
        }
    }
}
