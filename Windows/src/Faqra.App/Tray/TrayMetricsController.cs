// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the separate-metrics status items in Sources/Vorssaint/App/StatusItemController.swift
// (lines 489-601): one item per enabled metric, its title as the tooltip, a click opens the panel.

using System.Windows;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;
using Faqra.Core.Panel;
using Faqra.Core.Tray;
using Faqra.Services.Monitor;
using Faqra.Win32.Display;
using Faqra.Win32.Icons;
using Faqra.Win32.Shell;
using Faqra.Win32.Tray;
using FeatureRuntime = Faqra.Services.FeatureRuntime;

namespace Faqra.App.Tray;

/// <summary>Adds, refreshes and removes one tray icon per enabled metric. UI thread only.</summary>
public sealed class TrayMetricsController : IDisposable
{
    /// <summary>Ids after the main icon's; the metric's enum value is added so each keeps its own id.</summary>
    private const uint FirstMetricId = 10;

    private readonly ISettingsStore _store;
    private readonly FeatureRuntime _runtime;
    private readonly SystemMonitor _monitor;
    private readonly TrayMessageWindow _window = new();
    private readonly Dictionary<TrayMetric, (TrayIcon Icon, NativeIcon? Handle, TrayMetricText Text)> _icons = [];
    private IReadOnlyList<TrayMetric> _enabled = [];

    public TrayMetricsController(ISettingsStore store, FeatureRuntime runtime, SystemMonitor monitor)
    {
        _store = store;
        _runtime = runtime;
        _monitor = monitor;
        _window.Callback += OnCallback;
        _window.TaskbarCreated += OnTaskbarCreated;
        _monitor.SnapshotChanged += OnSnapshot;
        _store.Changed += OnSettingChanged;
        _runtime.RevisionChanged += OnRuntimeChanged;
    }

    /// <summary>Raised when a metric icon is clicked, with the panel section it belongs to.</summary>
    public event Action<PanelSectionId>? SectionRequested;

    /// <summary>Brings the icon set in line with the settings and draws the latest readings.</summary>
    public void Sync()
    {
        _enabled = TrayMetrics.Enabled(_store.String(DefaultsKey.MenuBarMetricOrder), _store.Bool, _runtime.IsAvailable, _monitor.HasBattery);
        foreach (var metric in _icons.Keys.Where(metric => !_enabled.Contains(metric)).ToList())
        {
            _icons[metric].Icon.Dispose();
            _icons[metric].Handle?.Dispose();
            _icons.Remove(metric);
        }
        // Windows places a new icon to the left of the existing ones, so adding in reverse keeps the saved order left to right.
        foreach (var metric in _enabled.Reverse())
        {
            if (!_icons.ContainsKey(metric))
            {
                _icons[metric] = (new TrayIcon(_window.Handle, FirstMetricId + (uint)metric, GuidFor(metric)), null, default);
            }
        }
        Draw(_monitor.Snapshot);
    }

    private void Draw(SystemSnapshot snapshot)
    {
        var strings = MonitorStrings.For(L10n.Shared.Language);
        var memoryMetric = DefaultsSanitizers.MonitorMemoryMetric(_store.String(DefaultsKey.MonitorMemoryMetric));
        var pixels = SystemMetrics.SmallIconPixels();
        var light = TaskbarTheme.IsLight();
        foreach (var metric in _enabled.Reverse())
        {
            if (!_icons.TryGetValue(metric, out var entry))
            {
                continue;
            }
            var text = TrayMetrics.Text(metric, snapshot, strings, memoryMetric);
            // Redrawing an unchanged icon still makes the shell repaint; skip it.
            if (entry.Handle is not null && entry.Text == text)
            {
                continue;
            }
            var handle = MetricGlyphPainter.RenderIcon(pixels, text, light);
            entry.Icon.Update(handle.Handle, text.Tooltip);
            entry.Handle?.Dispose();
            _icons[metric] = (entry.Icon, handle, text);
        }
    }

    private void OnSnapshot(SystemSnapshot snapshot)
    {
        if (_enabled.Count == 0)
        {
            return;
        }
        Application.Current?.Dispatcher.BeginInvoke(() => Draw(snapshot));
    }

    private void OnSettingChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Key.StartsWith("menuBar", StringComparison.Ordinal) || e.Key == DefaultsKey.MonitorMemoryMetric)
        {
            Application.Current?.Dispatcher.BeginInvoke(Sync);
        }
    }

    private void OnRuntimeChanged(object? sender, EventArgs e) => Sync();

    private void OnTaskbarCreated()
    {
        // Explorer restarted and dropped every icon; forget the drawn state so the next draw re-adds them.
        foreach (var metric in _icons.Keys.ToList())
        {
            var entry = _icons[metric];
            entry.Icon.MarkRemoved();
            entry.Handle?.Dispose();
            _icons[metric] = (entry.Icon, null, default);
        }
        Draw(_monitor.Snapshot);
    }

    private void OnCallback(TrayCallback callback)
    {
        if (callback.IconId < FirstMetricId || !callback.IsSelect)
        {
            return;
        }
        var metric = (TrayMetric)(callback.IconId - FirstMetricId);
        if (Enum.IsDefined(metric))
        {
            SectionRequested?.Invoke(metric.Section());
        }
    }

    /// <summary>A stable identity per metric, so Windows remembers where the user pinned each icon.</summary>
    private static Guid GuidFor(TrayMetric metric) => new($"6f1c3c0e-3b1a-4b62-9a7e-3f2a1f5d9c{0x10 + (int)metric:x2}");

    public void Dispose()
    {
        _store.Changed -= OnSettingChanged;
        _runtime.RevisionChanged -= OnRuntimeChanged;
        _monitor.SnapshotChanged -= OnSnapshot;
        foreach (var (icon, handle, _) in _icons.Values)
        {
            icon.Dispose();
            handle?.Dispose();
        }
        _icons.Clear();
        _window.Dispose();
    }
}
