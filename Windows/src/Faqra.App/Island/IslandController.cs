// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors NotchService in Sources/Vorssaint/Services/Notch/NotchService.swift: the island's state
// machine. Hover timings, the focus split and the collapse rules are upstream's.

using System.IO;
using System.Windows;
using System.Windows.Threading;
using Faqra.App.Island.Modules;
using Faqra.Core;
using Faqra.Core.Agents;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Island;
using Faqra.Core.Localization;
using Faqra.Services.Agents;
using Faqra.Services.Media;
using Faqra.Services.Monitor;
using Faqra.Win32.Display;
using FeatureRuntime = Faqra.Services.FeatureRuntime;
using Faqra.Win32.Input;
using Faqra.Win32.Windows;

namespace Faqra.App.Island;

/// <summary>
/// Owns the island window: where it sits, what it shows, and when it opens and closes. One
/// instance for the whole app; the feature runtime starts and stops it.
/// </summary>
public sealed class IslandController : IDisposable
{
    /// <summary>
    /// WPF drops MouseLeave when the pointer moves onto another topmost window, so an open island
    /// also polls the live cursor. 50 ms is well inside the 180 ms exit delay.
    /// </summary>
    private static readonly TimeSpan PointerPollInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// How often the island checks which display the pointer is on while it is resting. One cursor
    /// read and one monitor lookup is cheap enough to run four times a second, and the island only
    /// moves when the answer changes.
    /// </summary>
    private static readonly TimeSpan MonitorPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly ISettingsStore _store;
    private readonly FeatureRuntime _runtime;
    private readonly NowPlayingService _nowPlaying;
    private readonly SystemMonitor _systemMonitor;
    private readonly Services.Audio.AppVolumeMixer _mixer;
    private readonly AgentHub _agents;
    private readonly IslandHoverState _hover = new();
    private readonly DispatcherTimer _openTimer;
    private readonly DispatcherTimer _closeTimer;
    private readonly DispatcherTimer _pointerPoll;
    private readonly DispatcherTimer _monitorPoll;
    private readonly OutsideClickMonitor _outsideClick = new();

    private IslandWindow? _window;
    private TopmostGuard? _topmost;
    private IslandGeometry _geometry = new(1920, 1080);
    private MonitorGeometry _monitor = MonitorInfo.Primary();
    private IslandPresentation _presentation = IslandPresentation.Collapsed;
    private IslandSizeValue _currentSize;
    private IslandModule _module = IslandModule.Music;
    private IntPtr _previousForeground;
    private bool _openedByHover;
    private bool _pinned;
    private bool _sectionsOpen;
    private bool _suspendedForFullscreen;
    private (string? Id, AgentState? State) _agentsShown;

    public IslandController(ISettingsStore store, FeatureRuntime runtime, NowPlayingService nowPlaying, SystemMonitor monitor, Services.Audio.AppVolumeMixer mixer, AgentHub agents)
    {
        _store = store;
        _runtime = runtime;
        _nowPlaying = nowPlaying;
        _systemMonitor = monitor;
        _mixer = mixer;
        _agents = agents;

        _openTimer = new DispatcherTimer { Interval = IslandHoverState.OpenDelay };
        _openTimer.Tick += (_, _) => { _openTimer.Stop(); OpenFromHover(); };

        _closeTimer = new DispatcherTimer();
        _closeTimer.Tick += (_, _) => { _closeTimer.Stop(); Collapse(); };

        _pointerPoll = new DispatcherTimer { Interval = PointerPollInterval };
        _pointerPoll.Tick += (_, _) => PollPointer();

        _monitorPoll = new DispatcherTimer { Interval = MonitorPollInterval };
        _monitorPoll.Tick += (_, _) => PollMonitor();

        _outsideClick.Pressed += OnOutsidePress;
        _nowPlaying.Changed += OnNowPlayingChanged;
        _agents.Changed += OnAgentsChanged;
        _store.Changed += OnSettingChanged;
    }

    public bool IsRunning => _window is not null;

    /// <summary>Starts or stops the island to match the feature's current state.</summary>
    public void SyncWithPreferences()
    {
        var wanted = _runtime.IsAvailable(AppFeature.Notch) && _store.Bool(DefaultsKey.NotchEnabled);
        if (!wanted)
        {
            Stop();
            return;
        }
        if (_window is null)
        {
            Start();
        }
        else
        {
            RebuildGeometry();
            Render(animate: false);
        }
    }

    private void Start()
    {
        _monitor = ChosenMonitor();
        RebuildGeometry();

        _window = new IslandWindow();
        _window.Attach(this);
        _currentSize = _geometry.RestingSize(showsContent: ShowsIdleContent());
        _window.Show();
        WindowStyles.ShowWithoutActivating(_window.Handle);

        _topmost = new TopmostGuard(_window.Handle);
        _topmost.ForegroundChanged += OnForegroundChanged;

        Render(animate: false);
        _monitorPoll.Start();
    }

    private void Stop()
    {
        _openTimer.Stop();
        _closeTimer.Stop();
        _pointerPoll.Stop();
        _monitorPoll.Stop();
        _outsideClick.Stop();
        _topmost?.Dispose();
        _topmost = null;
        _window?.Close();
        _window = null;
        _presentation = IslandPresentation.Collapsed;
        _sectionsOpen = false;
        _systemMonitor.SetNotchVisible(false);
    }

    /// <summary>
    /// Which display hosts the island. Upstream picks the notched screen; Windows has none, so
    /// automatic follows the pointer between monitors and the fixed settings pin it to primary.
    /// </summary>
    private MonitorGeometry ChosenMonitor() =>
        IslandSizes.DisplayFromRawValue(_store.String(DefaultsKey.NotchDisplay)) == IslandDisplay.Automatic
            ? MonitorInfo.UnderPointer()
            : MonitorInfo.Primary();

    private void RebuildGeometry()
    {
        var layout = IslandSizes.FromRawValue(_store.String(DefaultsKey.NotchSize));
        _geometry = new IslandGeometry(
            _monitor.WidthDip,
            _monitor.HeightDip,
            IslandGeometry.DefaultBarHeight,
            layout,
            _store.Double(DefaultsKey.NotchCustomWidth),
            _store.Double(DefaultsKey.NotchCustomHeight),
            IslandSizes.EdgeFromRawValue(_store.String(DefaultsKey.IslandEdge)));
    }

    /// <summary>
    /// Moves the island to the display the pointer is on. Only while it is resting: yanking an open
    /// island to another screen mid-use would lose whatever the user was reading.
    /// </summary>
    private void PollMonitor()
    {
        if (_window is null || _presentation != IslandPresentation.Collapsed || _suspendedForFullscreen)
        {
            return;
        }
        var target = ChosenMonitor();
        if (target.Handle == _monitor.Handle)
        {
            return;
        }
        _monitor = target;
        RebuildGeometry();
        Render(animate: false);
    }

    // MARK: presentation

    private bool ShowsIdleContent() =>
        UrgentAgent() is not null
        || IdleContent() != IslandIdleContent.None
        && (IdleContent() != IslandIdleContent.Music || _nowPlaying.Current.HasTrack);

    /// <summary>The session the resting pill shows: any agent busy or waiting outranks music and battery.</summary>
    private AgentSession? UrgentAgent() =>
        _runtime.IsAvailable(AppFeature.FaqraAgents) && _agents.Board.IsActive ? _agents.Board.MostUrgent : null;

    private IslandIdleContent IdleContent() =>
        IslandSizes.IdleContentFromRawValue(_store.String(DefaultsKey.NotchIdleContent));

    private IReadOnlyList<IslandModule> VisibleModules() => IslandModules.Visible(
        _store.String(DefaultsKey.NotchModuleOrder),
        _store.String(DefaultsKey.NotchHiddenModules),
        _runtime.IsAvailable);

    private IslandSizeValue TargetSize() => _presentation switch
    {
        IslandPresentation.Expanded => _geometry.ExpandedSize(
            _module,
            systemRows: IslandSystemCards.Rows(SystemCards().Count, _geometry.SystemColumns)),
        IslandPresentation.Peek => _geometry.Peek,
        _ => _geometry.RestingSize(ShowsIdleContent()),
    };

    /// <summary>Applies the current state to the window: size, position, content and focus.</summary>
    private void Render(bool animate = true)
    {
        if (_window is null)
        {
            return;
        }
        var target = TargetSize();
        var expanded = _presentation == IslandPresentation.Expanded;

        _window.ShowExpanded(expanded, _geometry.ContentInset, animate);
        // The monitor samples for the System module only while its cards are on screen.
        _systemMonitor.SetNotchVisible(expanded && _module == IslandModule.System && !_sectionsOpen);
        if (expanded)
        {
            _window.SetModule(ModuleTitle(_module), BuildModule(_module), animate);
            _window.SetPinned(_pinned);
        }
        else
        {
            _window.SetIdleContent(BuildIdleContent());
        }

        _window.ApplyShape(
            _currentSize,
            target,
            RectFor(Envelope(_currentSize, target)),
            RectFor(target),
            _geometry.Edge,
            _geometry.CornersFor(target),
            animate);
        _currentSize = target;

        if (_presentation == IslandPresentation.Collapsed)
        {
            _pointerPoll.Stop();
            _outsideClick.Stop();
            _monitorPoll.Start();
        }
        else
        {
            // An open island stays put, so it stops watching for a monitor change until it closes.
            _monitorPoll.Stop();
            _pointerPoll.Start();
            _outsideClick.Start();
        }
    }

    /// <summary>The screen rectangle, in physical pixels, that a size occupies on its edge.</summary>
    private ScreenRect RectFor(IslandSizeValue size)
    {
        var (dipX, dipY) = _geometry.OriginFor(size);
        var (x, y) = _monitor.ToScreenPixels(dipX, dipY);
        return new ScreenRect(x, y, _monitor.ToPixels(size.Width), _monitor.ToPixels(size.Height));
    }

    /// <summary>Backing space for both ends of a transition.</summary>
    private static IslandSizeValue Envelope(IslandSizeValue from, IslandSizeValue to) =>
        new(Math.Max(from.Width, to.Width), Math.Max(from.Height, to.Height));

    private string ModuleTitle(IslandModule module)
    {
        var hub = FeatureHubStrings.For(L10n.Shared.Language);
        return module switch
        {
            IslandModule.Music => hub.FeatureTitles[AppFeature.NotchQueue] is { Length: > 0 } ? "Music" : "Music",
            IslandModule.Timer => hub.FeatureTitles[AppFeature.NotchTimer],
            IslandModule.Mixer => hub.FeatureTitles[AppFeature.Mixer],
            IslandModule.FaqraAgents => hub.FeatureTitles[AppFeature.FaqraAgents],
            IslandModule.System => hub.GroupTitles[FeatureGroup.Monitor],
            _ => module.RawValue(),
        };
    }

    private UIElement? BuildModule(IslandModule module) => module switch
    {
        IslandModule.Music => new MusicModule(_nowPlaying),
        IslandModule.Timer => new TimerModule(),
        IslandModule.System => new SystemModule(_systemMonitor, SystemCards(), _geometry.SystemColumns),
        IslandModule.Mixer => new MixerModule(_store, _runtime.IsAvailable, _mixer),
        IslandModule.FaqraAgents => new AgentsModule(_agents, AgentHooksInstalled),
        // Modules whose feature is not ported yet keep their place in the section list; their
        // content arrives with the feature.
        _ => new ModulePlaceholder(ModuleTitle(module)),
    };

    private IReadOnlyList<IslandSystemCard> SystemCards() => IslandSystemCards.Available(_runtime.IsAvailable, _systemMonitor.HasBattery);

    private UIElement? BuildIdleContent()
    {
        var urgent = UrgentAgent();
        _agentsShown = (urgent?.Id, urgent?.State);
        if (urgent is not null)
        {
            return new IdleAgentsView(urgent);
        }
        return IdleContent() switch
        {
            IslandIdleContent.Music when _nowPlaying.Current.HasTrack => new IdleMusicView(_nowPlaying.Current),
            IslandIdleContent.Battery => new IdleBatteryView(),
            _ => null,
        };
    }

    private static bool AgentHooksInstalled()
    {
        try
        {
            return File.Exists(AppPaths.ClaudeSettingsFile)
                && Core.Agents.Install.ClaudeHookConfig.Inspect(File.ReadAllText(AppPaths.ClaudeSettingsFile)).Installed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Core.Agents.Install.ConfigFormatException)
        {
            return false;
        }
    }

    // MARK: hover

    internal void PointerEnteredShape()
    {
        if (_suspendedForFullscreen)
        {
            return;
        }
        _closeTimer.Stop();
        _hover.Update(pointerInside: true);
        if (_presentation == IslandPresentation.Collapsed)
        {
            _openTimer.Start();
        }
    }

    internal void PointerLeftShape()
    {
        _openTimer.Stop();
        _hover.Update(pointerInside: false);
        ScheduleClose();
    }

    private void ScheduleClose()
    {
        if (IslandHoverState.ExitDelay(_presentation, _openedByHover, _pinned) is not { } delay)
        {
            return;
        }
        _closeTimer.Interval = delay;
        _closeTimer.Start();
    }

    private void OpenFromHover()
    {
        var presentation = _hover.PresentationForHover(
            _store.Bool(DefaultsKey.NotchOpenOnHover),
            _store.Bool(DefaultsKey.NotchHoverExpands));
        if (presentation is null || _presentation != IslandPresentation.Collapsed)
        {
            return;
        }
        _openedByHover = true;
        _hover.Open();
        _presentation = presentation.Value;
        if (_presentation == IslandPresentation.Expanded)
        {
            _module = DefaultModule();
        }
        Render();
    }

    private IslandModule DefaultModule()
    {
        var visible = VisibleModules();
        if (visible.Count == 0)
        {
            return IslandModule.Controls;
        }
        return visible.Contains(_module) ? _module : visible[0];
    }

    /// <summary>A hover-opened island loses the pointer without a MouseLeave; the poll catches it.</summary>
    private void PollPointer()
    {
        if (_window is null)
        {
            return;
        }
        var inside = _window.PointerIsOverShape();
        _hover.Update(inside);
        if (inside)
        {
            _closeTimer.Stop();
        }
        else if (!_closeTimer.IsEnabled)
        {
            ScheduleClose();
        }
    }

    // MARK: commands

    internal void ShapeClicked()
    {
        if (_presentation == IslandPresentation.Collapsed)
        {
            Expand(takeFocus: true);
        }
        else if (_presentation == IslandPresentation.Peek)
        {
            Expand(takeFocus: true);
        }
    }

    /// <summary>Opens a module. A click-opened island takes the keyboard and does not auto-close.</summary>
    public void Expand(bool takeFocus, IslandModule? module = null)
    {
        if (_window is null)
        {
            return;
        }
        _openTimer.Stop();
        _closeTimer.Stop();
        _openedByHover = false;
        _presentation = IslandPresentation.Expanded;
        _module = module ?? DefaultModule();
        Render();

        if (takeFocus)
        {
            _previousForeground = WindowStyles.ForegroundWindow();
            WindowStyles.SetNonActivating(_window.Handle, nonActivating: false);
            WindowStyles.Focus(_window.Handle);
            _window.Activate();
            _window.Focus();
        }
    }

    public void Collapse()
    {
        if (_window is null || _presentation == IslandPresentation.Collapsed)
        {
            return;
        }
        _closeTimer.Stop();
        _sectionsOpen = false;
        _presentation = IslandPresentation.Collapsed;
        _hover.Close(pointerInside: _window.PointerIsOverShape());
        _openedByHover = false;

        // Give the keyboard back before the shape shrinks, so focus never lands on a hidden control.
        WindowStyles.SetNonActivating(_window.Handle, nonActivating: true);
        if (_previousForeground != IntPtr.Zero)
        {
            WindowStyles.Focus(_previousForeground);
            _previousForeground = IntPtr.Zero;
        }
        Render();
    }

    internal void EscapePressed()
    {
        if (_sectionsOpen)
        {
            _sectionsOpen = false;
            Render();
            return;
        }
        Collapse();
    }

    internal void TogglePinned()
    {
        _pinned = !_pinned;
        _window?.SetPinned(_pinned);
        if (_pinned)
        {
            _closeTimer.Stop();
        }
    }

    internal void ToggleSections()
    {
        _sectionsOpen = !_sectionsOpen;
        if (_window is null)
        {
            return;
        }
        if (_sectionsOpen)
        {
            _systemMonitor.SetNotchVisible(false);
            _window.SetModule(FeatureHubStrings.For(L10n.Shared.Language).TabFeatures,
                new SectionPicker(VisibleModules(), module =>
                {
                    _sectionsOpen = false;
                    Expand(takeFocus: false, module);
                }),
                animate: true);
        }
        else
        {
            Render();
        }
    }

    internal void CycleModule(int delta)
    {
        if (IslandModules.Adjacent(_module, delta, VisibleModules()) is { } next)
        {
            Expand(takeFocus: false, next);
        }
    }

    // MARK: environment

    private void OnOutsidePress(Faqra.Win32.Native.POINT point)
    {
        if (_window is null || _pinned || _presentation == IslandPresentation.Collapsed)
        {
            return;
        }
        // The hook runs on the UI thread's message loop, but marshal anyway: the press may arrive
        // while a render is mid-flight.
        _window.Dispatcher.BeginInvoke(() =>
        {
            if (!_window!.PointerIsOverShape())
            {
                Collapse();
            }
        });
    }

    private void OnForegroundChanged()
    {
        if (_window is null)
        {
            return;
        }
        _window.Dispatcher.BeginInvoke(() =>
        {
            var fullscreen = MonitorInfo.FullscreenAppIsRunning();
            if (fullscreen == _suspendedForFullscreen)
            {
                return;
            }
            _suspendedForFullscreen = fullscreen;
            // An overlay cannot sit above exclusive fullscreen, so it steps aside instead of flickering.
            if (fullscreen)
            {
                Collapse();
                WindowStyles.Hide(_window!.Handle);
            }
            else
            {
                WindowStyles.ShowWithoutActivating(_window!.Handle);
                Render(animate: false);
            }
        });
    }

    private void OnNowPlayingChanged()
    {
        _window?.Dispatcher.BeginInvoke(() =>
        {
            if (_presentation == IslandPresentation.Collapsed)
            {
                Render(animate: true);
            }
            else if (_module == IslandModule.Music)
            {
                Render(animate: false);
            }
        });
    }

    /// <summary>
    /// Re-renders the resting pill only when the session or state it shows changes, so its orb keeps
    /// moving through the many events a busy session sends.
    /// </summary>
    private void OnAgentsChanged()
    {
        if (_window is null || _presentation != IslandPresentation.Collapsed)
        {
            return;
        }
        var urgent = UrgentAgent();
        if ((urgent?.Id, urgent?.State) != _agentsShown)
        {
            Render(animate: true);
        }
    }

    private void OnSettingChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Key is not (DefaultsKey.NotchEnabled or DefaultsKey.NotchSize or DefaultsKey.NotchDisplay
            or DefaultsKey.NotchIdleContent or DefaultsKey.NotchModuleOrder or DefaultsKey.NotchHiddenModules
            or DefaultsKey.NotchCustomWidth or DefaultsKey.NotchCustomHeight or DefaultsKey.IslandEdge))
        {
            return;
        }
        Application.Current?.Dispatcher.BeginInvoke(SyncWithPreferences);
    }

    public void Dispose()
    {
        _store.Changed -= OnSettingChanged;
        _nowPlaying.Changed -= OnNowPlayingChanged;
        _agents.Changed -= OnAgentsChanged;
        _outsideClick.Dispose();
        Stop();
    }
}
