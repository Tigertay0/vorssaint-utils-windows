// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the popover handling in Sources/Vorssaint/App/AppDelegate.swift (setUpPopover, togglePopover,
// the global and local dismissal monitors) and the section selection of UI/MenuPanel/MenuPanelView.swift
// (lines 85, 137-152, 249-255, 313-346). Motion is transitions.dev's menu-dropdown token.

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Faqra.App.Panel.Sections;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Island;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;
using Faqra.Core.Panel;
using Faqra.Services.Audio;
using Faqra.Services.KeepAwake;
using Faqra.Services.Monitor;
using Faqra.Win32.Display;
using Faqra.Win32.Input;
using Faqra.Win32.Windows;
using FeatureRuntime = Faqra.Services.FeatureRuntime;

namespace Faqra.App.Panel;

/// <summary>Owns the popover panel: what it shows, where it opens, and when it closes. UI thread only.</summary>
public sealed class MenuPanelController : IDisposable
{
    /// <summary>Windows 11 tray flyouts float this far from the taskbar and the screen edge.</summary>
    private const double GapDip = 12;

    /// <summary>Upstream never lets the panel's maximum height drop below this.</summary>
    private const double MinMaxHeightDip = 360;

    /// <summary>A tray click right after an outside click closed the panel is that same click; ignore it.</summary>
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(350);

    private const double RevealScale = 0.97;

    private readonly ISettingsStore _store;
    private readonly FeatureRuntime _runtime;
    private readonly SystemMonitor _monitor;
    private readonly AppVolumeMixer _mixer;
    private readonly KeepAwakeManager _keepAwake;
    private readonly Func<PixelRect?> _iconRect;
    private readonly OutsideClickMonitor _outsideClick = new();

    private MenuPanelWindow? _window;
    private IPanelSectionView? _view;
    private PanelSectionId? _selected;
    private IReadOnlyList<PanelSectionId> _tabs = [];
    private DateTime _closedAt = DateTime.MinValue;
    private bool _open;

    public MenuPanelController(ISettingsStore store, FeatureRuntime runtime, SystemMonitor monitor, AppVolumeMixer mixer, KeepAwakeManager keepAwake, Func<PixelRect?> iconRect)
    {
        _store = store;
        _runtime = runtime;
        _monitor = monitor;
        _mixer = mixer;
        _keepAwake = keepAwake;
        _iconRect = iconRect;
        _outsideClick.Pressed += OnOutsidePress;
        _monitor.SnapshotChanged += OnSnapshot;
        _store.Changed += OnSettingChanged;
    }

    public bool IsOpen => _open;

    /// <summary>The sections built on Windows so far; the rest wait for their milestone.</summary>
    internal static bool IsBuilt(PanelSectionId id) =>
        id is PanelSectionId.KeepAwake or PanelSectionId.Mixer
            or PanelSectionId.System or PanelSectionId.Network or PanelSectionId.Disk or PanelSectionId.Power;

    public void Toggle()
    {
        if (_open)
        {
            Close();
        }
        else if (DateTime.UtcNow - _closedAt > ReopenGuard)
        {
            Open();
        }
    }

    /// <summary>
    /// Opens on a section, from a tray metric icon. Clicking the icon of the section already showing
    /// closes the panel, as clicking the same metric twice does upstream.
    /// </summary>
    public void Show(PanelSectionId section)
    {
        if (_open && _selected == section)
        {
            Close();
            return;
        }
        _selected = section;
        if (_open)
        {
            Rebuild(MonitorStrings.For(L10n.Shared.Language));
        }
        else if (DateTime.UtcNow - _closedAt > ReopenGuard)
        {
            Open();
        }
    }

    public void Open()
    {
        var window = EnsureWindow();
        var strings = MonitorStrings.For(L10n.Shared.Language);
        window.SetFooter(strings);
        Rebuild(strings);

        _open = true;
        window.Opacity = 0;
        window.Show();
        Place();
        window.Activate();
        _outsideClick.Start();
        Animate(window, opening: true);
    }

    public void Close()
    {
        if (!_open || _window is not { } window)
        {
            return;
        }
        _open = false;
        _closedAt = DateTime.UtcNow;
        _outsideClick.Stop();
        _monitor.SetPanelNeeds(default);
        ReleaseView();
        Animate(window, opening: false);
    }

    private MenuPanelWindow EnsureWindow()
    {
        if (_window is not null)
        {
            return _window;
        }
        var window = new MenuPanelWindow();
        window.SectionSelected += id =>
        {
            _selected = id;
            ShowSection(id, MonitorStrings.For(L10n.Shared.Language));
        };
        window.CloseRequested += Close;
        window.SettingsRequested += () =>
        {
            Close();
            App.ShowSettings(Core.Settings.SettingsPage.Monitor);
        };
        // The panel grows and shrinks with its section; keep it anchored to the taskbar as it does.
        window.SizeChanged += (_, _) =>
        {
            if (_open)
            {
                Place();
            }
        };
        _window = window;
        return window;
    }

    /// <summary>Recomputes the tabs and the active section from the current settings.</summary>
    private void Rebuild(MonitorStrings strings)
    {
        if (_window is null)
        {
            return;
        }
        _tabs = PanelLayout.Visible(
            PanelLayout.Order(_store.String(DefaultsKey.PanelSectionOrder)),
            id => _store.Bool(id.VisibilityKey()),
            _runtime.IsAvailable,
            _store.Bool(DefaultsKey.BrightnessControlEnabled),
            IsBuilt);
        var active = PanelLayout.Active(_selected, _tabs);
        _window.SetTabs(_tabs, active, strings);
        ShowSection(active, strings);
    }

    private void ShowSection(PanelSectionId id, MonitorStrings strings)
    {
        if (_window is null)
        {
            return;
        }
        ReleaseView();
        if (!_tabs.Contains(id))
        {
            _window.SetSection(PanelText.Label(strings.ComingLater, 12, PanelBrushes.Secondary));
            _monitor.SetPanelNeeds(default);
            return;
        }
        _view = CreateSection(id, new SectionContext(_store, _runtime.IsAvailable, _mixer, _keepAwake), strings);
        if (_view is DiskSectionView disk)
        {
            disk.SelectionChanged += () => _view?.Update(_monitor.Snapshot);
        }
        _view.Update(_monitor.Snapshot);
        _window.SetSection(_view.Root);
        _monitor.SetPanelNeeds(NeedsFor(id));
    }

    /// <summary>Sections with their own live source (mixer, keep awake) stop listening when they leave the screen.</summary>
    private void ReleaseView()
    {
        (_view as IDisposable)?.Dispose();
        _view = null;
    }

    internal static IPanelSectionView CreateSection(PanelSectionId id, SectionContext context, MonitorStrings strings) => id switch
    {
        PanelSectionId.KeepAwake when context.KeepAwake is { } keepAwake => new KeepAwakeSectionView(context, keepAwake, SectionPalette.Panel),
        PanelSectionId.Mixer when context.Mixer is { } mixer => new MixerSectionView(context, mixer, SectionPalette.Panel),
        PanelSectionId.Network => new NetworkSectionView(context, strings),
        PanelSectionId.Disk => new DiskSectionView(context, strings),
        PanelSectionId.Power => new PowerSectionView(context, strings),
        _ => new SystemSectionView(context, strings),
    };

    private static PanelNeeds NeedsFor(PanelSectionId id) => id switch
    {
        PanelSectionId.System => new PanelNeeds(System: true),
        PanelSectionId.Network => new PanelNeeds(Network: true),
        PanelSectionId.Disk => new PanelNeeds(Disk: true),
        PanelSectionId.Power => new PanelNeeds(Power: true),
        _ => default,
    };

    /// <summary>Anchors the panel to the tray icon, on the monitor that holds the taskbar.</summary>
    private void Place()
    {
        if (_window is null)
        {
            return;
        }
        var icon = _iconRect();
        var monitor = icon is { } r ? MonitorInfo.ForPoint(r.CenterX, r.CenterY) : MonitorInfo.UnderPointer();
        var bounds = new PixelRect(monitor.Bounds.Left, monitor.Bounds.Top, monitor.Bounds.Right, monitor.Bounds.Bottom);
        var work = new PixelRect(monitor.WorkArea.Left, monitor.WorkArea.Top, monitor.WorkArea.Right, monitor.WorkArea.Bottom);
        var gap = monitor.ToPixels(GapDip);

        var maxHeight = PanelPlacement.MaxHeight(work, gap, monitor.ToPixels(MinMaxHeightDip));
        _window.SetMaxHeight(maxHeight / monitor.Scale);
        _window.UpdateLayout();

        var width = monitor.ToPixels(_window.ActualWidth);
        var height = monitor.ToPixels(_window.ActualHeight);
        var origin = PanelPlacement.Origin(icon, bounds, work, width, height, gap);
        WindowStyles.SetBounds(_window.Handle, origin.X, origin.Y, width, height);
    }

    /// <summary>transitions.dev's menu-dropdown: fade and a 0.97 pre-scale from the corner nearest the tray.</summary>
    private static void Animate(MenuPanelWindow window, bool opening)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            window.Opacity = opening ? 1 : 0;
            if (!opening)
            {
                window.Hide();
            }
            return;
        }
        var duration = opening ? IslandMotion.DropdownOpen : IslandMotion.DropdownClose;
        var fade = Motion.Double(opening ? 0 : 1, opening ? 1 : 0, duration);
        if (!opening)
        {
            fade.Completed += (_, _) =>
            {
                // A reopen during the fade owns the window now; only hide if it is still meant to be closed.
                if (window.Opacity == 0)
                {
                    window.Hide();
                }
            };
        }
        window.BeginAnimation(UIElement.OpacityProperty, fade);
        var scale = Motion.Double(opening ? RevealScale : 1, opening ? 1 : RevealScale, duration);
        window.RevealScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        window.RevealScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
    }

    private void OnOutsidePress(Win32.Native.POINT point)
    {
        if (!_open || _window is null)
        {
            return;
        }
        _window.Dispatcher.BeginInvoke(() =>
        {
            if (!_open || _window is null)
            {
                return;
            }
            // A press on the tray icon toggles the panel through the icon itself.
            if (_iconRect() is { } icon && point.X >= icon.Left && point.X < icon.Right && point.Y >= icon.Top && point.Y < icon.Bottom)
            {
                return;
            }
            var local = _window.PointFromScreen(new Point(point.X, point.Y));
            var inside = local.X >= 0 && local.Y >= 0 && local.X <= _window.ActualWidth && local.Y <= _window.ActualHeight;
            if (!inside)
            {
                Close();
            }
        });
    }

    private void OnSnapshot(SystemSnapshot snapshot) =>
        _window?.Dispatcher.BeginInvoke(() =>
        {
            if (_open)
            {
                _view?.Update(snapshot);
            }
        });

    private void OnSettingChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (!e.Key.StartsWith("monitor", StringComparison.Ordinal)
            && !e.Key.StartsWith("panel", StringComparison.Ordinal)
            && !e.Key.StartsWith(DefaultsKey.FeatureAvailable(string.Empty), StringComparison.Ordinal))
        {
            return;
        }
        _window?.Dispatcher.BeginInvoke(() =>
        {
            if (_open)
            {
                Rebuild(MonitorStrings.For(L10n.Shared.Language));
            }
        });
    }

    public void Dispose()
    {
        _store.Changed -= OnSettingChanged;
        _monitor.SnapshotChanged -= OnSnapshot;
        _outsideClick.Dispose();
        ReleaseView();
        _window?.Close();
        _window = null;
    }
}
