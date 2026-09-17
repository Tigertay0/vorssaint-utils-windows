// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors CommandBarService in Sources/Vorssaint/Services/CommandBar/CommandBarService.swift: show and hide,
// the search, argument and confirm modes, the key handling, running a row and recording what was used.
// Windows cannot type into a window that is not active, so the bar activates and hands focus back to
// the window that was in front when it closes without opening something else (design doc B.8).

using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Faqra.Core.CommandBar;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Localization;
using Faqra.Core.Panel;
using Faqra.Core.Settings;
using Faqra.Core.Shortcuts;
using Faqra.Win32.Display;
using Faqra.Win32.Shell;
using Faqra.Win32.Windows;

namespace Faqra.App.CommandBar;

internal enum CommandBarMode
{
    Search,
    Argument,
    Confirm,
}

/// <summary>Owns the command bar window and everything it does. UI thread only.</summary>
public sealed class CommandBarController : IDisposable
{
    private const int MarginDip = 16;
    private const int MaxIndexShortcut = 9;
    private static readonly TimeSpan AppsFreshFor = TimeSpan.FromMinutes(5);

    private readonly AppServices _services;
    private readonly CommandBarCatalog _catalog;
    private readonly CommandBarQueryMemory _queryMemory = new();
    private CommandBarWindow? _window;
    private List<CommandBarRow> _pool = [];
    private List<CommandBarListItem> _items = [];
    private IReadOnlyList<InstalledApp> _apps = [];
    private DateTime _appsLoadedAt = DateTime.MinValue;
    private bool _appsLoading;
    private int _selected;
    private int _step;
    private CommandBarMode _mode = CommandBarMode.Search;
    private CommandBarRow? _modeRow;
    private string _savedQuery = string.Empty;
    private IntPtr _previousForeground;
    private PixelPoint _origin;
    private MonitorGeometry? _monitor;
    private IReadOnlyDictionary<string, CommandBarUse>? _usage;
    private bool _hiding;

    public CommandBarController(AppServices services)
    {
        _services = services;
        _catalog = new CommandBarCatalog(services);
    }

    public bool IsVisible => _window?.IsVisible == true;

    private static CommandBarStrings Bar => CommandBarStrings.For(L10n.Shared.Language);

    /// <summary>Starts reading the app list so the first opening already finds apps.</summary>
    public void Prepare()
    {
        if (_services.FeatureRuntime.IsAvailable(AppFeature.CommandBar))
        {
            LoadAppsIfStale();
        }
    }

    public void Toggle()
    {
        if (IsVisible)
        {
            Hide(restoreFocus: true);
        }
        else
        {
            Show();
        }
    }

    public void Show()
    {
        if (!_services.FeatureRuntime.IsAvailable(AppFeature.CommandBar))
        {
            return;
        }
        var window = EnsureWindow();
        var foreground = Win32.Native.User32.GetForegroundWindow();
        if (foreground != window.Handle)
        {
            _previousForeground = foreground;
        }
        _mode = CommandBarMode.Search;
        _modeRow = null;
        window.Query = string.Empty;
        RebuildPool();
        LoadAppsIfStale();
        Refresh();

        // Placed once per opening on the display under the pointer; the list then grows downward from a fixed top edge.
        _monitor = MonitorInfo.UnderPointer();
        window.Show();
        window.UpdateLayout();
        var work = new PixelRect(_monitor.WorkArea.Left, _monitor.WorkArea.Top, _monitor.WorkArea.Right, _monitor.WorkArea.Bottom);
        _origin = CommandBarPlacement.Origin(work, _monitor.ToPixels(window.ActualWidth), _monitor.ToPixels(window.ActualHeight), _monitor.ToPixels(MarginDip));
        Place();
        window.Activate();
        window.FocusQuery();
    }

    public void Hide(bool restoreFocus)
    {
        // Window.Hide raises Deactivated, which calls back in here; the outer call owns the focus hand-back.
        if (_hiding || _window is null || !_window.IsVisible)
        {
            return;
        }
        _hiding = true;
        try
        {
            HideWindow(_window, restoreFocus);
        }
        finally
        {
            _hiding = false;
        }
    }

    private void HideWindow(CommandBarWindow window, bool restoreFocus)
    {
        var previous = _previousForeground;
        _previousForeground = IntPtr.Zero;
        _usage = null;
        window.Hide();
        _mode = CommandBarMode.Search;
        _modeRow = null;
        // Nothing typed here is kept (CommandBarService.swift:371-411).
        window.Query = string.Empty;
        _pool = [];
        _items = [];
        if (restoreFocus && previous != IntPtr.Zero)
        {
            OpenWindows.Activate(previous);
        }
    }

    /// <summary>What was used, read once per opening rather than on every keystroke.</summary>
    private IReadOnlyDictionary<string, CommandBarUse> Usage => _usage ??= CommandBarUsage.Load(_services.Store);

    private CommandBarWindow EnsureWindow()
    {
        if (_window is not null)
        {
            return _window;
        }
        var window = new CommandBarWindow();
        window.QueryChanged += OnQueryChanged;
        window.KeyPressed += OnKey;
        window.RowHovered += index =>
        {
            if (_mode == CommandBarMode.Search && index != _selected)
            {
                _selected = index;
                window.Select(index);
            }
        };
        window.RowClicked += index =>
        {
            _selected = index;
            RunSelected();
        };
        // Clicking another window or switching apps closes the bar, like upstream's activation observer.
        window.Deactivated += (_, _) => Hide(restoreFocus: false);
        window.SizeChanged += (_, _) => Place();
        _window = window;
        return window;
    }

    private void RebuildPool()
    {
        var rows = _catalog.Build();
        rows.AddRange(CommandBarCatalog.Apps(_apps));
        rows.AddRange(CommandBarCatalog.Windows(OpenWindows.Enumerate()));
        _pool = rows;
    }

    private void LoadAppsIfStale()
    {
        if (_appsLoading || DateTime.UtcNow - _appsLoadedAt < AppsFreshFor)
        {
            return;
        }
        _appsLoading = true;
        var dispatcher = Application.Current.Dispatcher;
        Task.Run(AppsFolder.Enumerate).ContinueWith(task =>
        {
            dispatcher.BeginInvoke(() =>
            {
                _appsLoading = false;
                if (task.IsCompletedSuccessfully)
                {
                    _apps = task.Result;
                    _appsLoadedAt = DateTime.UtcNow;
                    // A list read for an earlier opening is still the right list for this one.
                    if (IsVisible && _mode == CommandBarMode.Search)
                    {
                        RebuildPool();
                        Refresh();
                    }
                }
            });
        }, TaskScheduler.Default);
    }

    private void OnQueryChanged()
    {
        switch (_mode)
        {
            case CommandBarMode.Confirm:
                // Typing cancels an armed confirmation, so a destructive Enter is never left waiting behind a search.
                _mode = CommandBarMode.Search;
                _modeRow = null;
                Refresh();
                break;
            case CommandBarMode.Search:
                Refresh();
                break;
        }
    }

    /// <summary>Recomputes the list for the current query and draws the current mode.</summary>
    private void Refresh()
    {
        if (_window is null)
        {
            return;
        }
        var bar = Bar;
        var shortcutState = _services.HotKeys?.State(GlobalShortcutRole.CommandBar);
        var shortcut = shortcutState?.Registered?.DisplayText ?? GlobalShortcutRole.CommandBar.Saved(_services.Store).DisplayText;
        _window.SetFooter(shortcut, "Ctrl+P  Ctrl+N  ↑↓   Enter   Esc");

        switch (_mode)
        {
            case CommandBarMode.Argument when _modeRow?.Argument is { } argument:
                _window.SetModeChip(_modeRow.Entry.Title);
                _window.SetPlaceholder(string.Format(bar.ArgumentRangeFormat, argument.Minimum, argument.Maximum));
                _window.RenderCard(CommandBarWindow.ArgumentCard(_modeRow, bar.ArgumentHint));
                return;
            case CommandBarMode.Confirm when _modeRow is not null:
                var row = _modeRow;
                _window.SetModeChip(null);
                _window.RenderCard(CommandBarWindow.ConfirmCard(row, bar, () => Finish(row, null), StepBack));
                return;
        }

        _window.SetModeChip(null);
        _window.SetPlaceholder(bar.SearchPlaceholder);
        var query = _window.Query;
        _items = string.IsNullOrWhiteSpace(query) ? HomeItems(bar) : SearchItems(query, bar);
        _selected = 0;
        _window.RenderList(_items, _selected, query, bar, string.IsNullOrWhiteSpace(query) ? null : GoHome);
    }

    private List<CommandBarListItem> HomeItems(CommandBarStrings bar)
    {
        var byId = UniqueById(_pool);
        var usage = Usage;
        var sections = CommandBarCatalogSupport.Home(byId.Values.Select(r => r.Entry).ToList(), usage, CommandBarCatalog.CuratedSuggestionIds, bar);
        var items = new List<CommandBarListItem>();
        foreach (var section in sections)
        {
            for (var i = 0; i < section.Entries.Count; i++)
            {
                items.Add(new CommandBarListItem(byId[section.Entries[i].Id], i == 0 ? section.Title : null));
            }
        }
        return items;
    }

    private List<CommandBarListItem> SearchItems(string query, CommandBarStrings bar)
    {
        var items = new List<CommandBarListItem>();
        // Answers and a typed address lead, ahead of ranking (CommandBarService.swift:1534-1536).
        var culture = CultureInfo.CurrentCulture;
        if (CommandBarCatalogSupport.Answer(query, DateTimeOffset.Now, TimeZoneInfo.Local, culture, bar) is { } answer)
        {
            var value = answer.Value;
            items.Add(new CommandBarListItem(new CommandBarRow(answer.Entry, new CommandBarIcon.Symbol(Wpf.Ui.Controls.SymbolRegular.Calculator24),
                _ => CopyAnswer(value), AnswerValue: value), null));
        }
        if (CommandBarCatalogSupport.TypedUrl(query) is { } url)
        {
            items.Add(new CommandBarListItem(CommandBarCatalog.OpenUrl(url, query.Trim()), null));
        }

        var byId = UniqueById(_pool);
        var text = SearchText(query, byId.Values);
        var ranked = CommandBarRanking.Rank(byId.Values.Select(r => r.Entry).ToList(), text,
            Usage, _queryMemory, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        items.AddRange(ranked.Select(entry => new CommandBarListItem(byId[entry.Id], null)));
        return items;
    }

    /// <summary>
    /// "volume 40" searches "volume" only when a row that takes a number matches the rest; otherwise the
    /// digits stay part of the search (CommandBarService.swift numeric argument split).
    /// </summary>
    private static string SearchText(string query, IEnumerable<CommandBarRow> rows)
    {
        var split = CommandBarSearch.SplitTrailingNumber(query);
        return split.Number is not null && rows.Any(r => r.Argument is not null && CommandBarSearch.Matches(r.Entry.Title, r.Entry.Keywords, split.Text))
            ? split.Text
            : query;
    }

    private static Dictionary<string, CommandBarRow> UniqueById(IEnumerable<CommandBarRow> rows)
    {
        // Several providers can emit the same id; the first one wins (CommandBarService.uniqued).
        var byId = new Dictionary<string, CommandBarRow>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            byId.TryAdd(row.Entry.Id, row);
        }
        return byId;
    }

    private void GoHome()
    {
        if (_window is not null)
        {
            _window.Query = string.Empty;
            Refresh();
            _window.FocusQuery();
        }
    }

    private void OnKey(KeyEventArgs e)
    {
        if (_window is null)
        {
            return;
        }
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        switch (key)
        {
            case Key.Escape:
                StepBack();
                break;
            case Key.Enter:
                RunSelected();
                break;
            case Key.Down when _mode == CommandBarMode.Search:
            case Key.N when ctrl && _mode == CommandBarMode.Search:
                MoveSelection(1);
                break;
            case Key.Up when _mode == CommandBarMode.Search:
            case Key.P when ctrl && _mode == CommandBarMode.Search:
                MoveSelection(-1);
                break;
            case Key.Tab when _mode == CommandBarMode.Search:
                CompleteSelection();
                break;
            case Key.OemComma when ctrl:
                Hide(restoreFocus: false);
                App.ShowSettings(SettingsPage.CommandBar);
                break;
            case >= Key.D1 and <= Key.D9 when ctrl && _mode == CommandBarMode.Search:
                var index = key - Key.D1;
                if (index < Math.Min(_items.Count, MaxIndexShortcut))
                {
                    _selected = index;
                    RunSelected();
                }
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void MoveSelection(int delta)
    {
        if (_items.Count == 0 || _window is null)
        {
            return;
        }
        // Wraps around both ends (CommandBarService.swift:1558-1562).
        _selected = ((_selected + delta) % _items.Count + _items.Count) % _items.Count;
        _window.Select(_selected);
    }

    private void CompleteSelection()
    {
        if (_window is null || _selected >= _items.Count || _items[_selected].Row.AnswerValue is not null)
        {
            return;
        }
        _window.Query = _items[_selected].Row.Entry.Title;
    }

    /// <summary>Esc: leave a mode, else clear the field, else close (CommandBarService.stepBack).</summary>
    private void StepBack()
    {
        if (_window is null)
        {
            return;
        }
        if (_mode != CommandBarMode.Search)
        {
            _mode = CommandBarMode.Search;
            _modeRow = null;
            _window.Query = _savedQuery;
            Refresh();
            _window.FocusQuery();
            return;
        }
        if (_window.Query.Length > 0)
        {
            _window.Query = string.Empty;
            Refresh();
            return;
        }
        Hide(restoreFocus: true);
    }

    private void RunSelected()
    {
        if (_window is null)
        {
            return;
        }
        switch (_mode)
        {
            case CommandBarMode.Confirm when _modeRow is not null:
                Finish(_modeRow, null);
                return;
            case CommandBarMode.Argument when _modeRow?.Argument is { } argument:
                if (CommandBarSearch.ArgumentValue(_window.Query, argument.Minimum, argument.Maximum) is { } value)
                {
                    Finish(_modeRow, value);
                }
                else
                {
                    System.Media.SystemSounds.Beep.Play();
                }
                return;
        }
        if (_selected < 0 || _selected >= _items.Count)
        {
            return;
        }
        Run(_items[_selected].Row);
    }

    /// <summary>Confirm first, then take a typed or asked-for number, else run (CommandBarService.run).</summary>
    private void Run(CommandBarRow row)
    {
        if (_window is null)
        {
            return;
        }
        if (row.ConfirmationPrompt is not null)
        {
            _savedQuery = _window.Query;
            _mode = CommandBarMode.Confirm;
            _modeRow = row;
            Refresh();
            return;
        }
        if (row.Argument is { } argument)
        {
            var typed = CommandBarSearch.SplitTrailingNumber(_window.Query).Number;
            if (typed is { } number)
            {
                Finish(row, Math.Clamp(number, argument.Minimum, argument.Maximum));
            }
            else if (argument.Optional)
            {
                Finish(row, null);
            }
            else
            {
                _savedQuery = _window.Query;
                _mode = CommandBarMode.Argument;
                _modeRow = row;
                _window.Query = string.Empty;
                Refresh();
                _window.FocusQuery();
            }
            return;
        }
        Finish(row, null);
    }

    private void Finish(CommandBarRow row, int? value)
    {
        var query = _mode == CommandBarMode.Search ? _window?.Query ?? string.Empty : _savedQuery;
        if (row.Entry.CountsUsage)
        {
            _queryMemory.Record(query, row.Entry.Id, ++_step);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            CommandBarUsage.Save(_services.Store, CommandBarUsage.Recording(Usage, row.Entry.Id, now));
        }
        // Rows that bring another window forward must not have focus snatched back to the old one.
        Hide(restoreFocus: !row.ActivatesAnotherWindow);
        row.Run(value);
    }

    private static void CopyAnswer(string value)
    {
        try
        {
            Clipboard.SetText(value);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another app holds the clipboard open; say so rather than fail silently.
            System.Media.SystemSounds.Beep.Play();
        }
    }

    private void Place()
    {
        if (_window is null || _monitor is null || !_window.IsVisible)
        {
            return;
        }
        var width = _monitor.ToPixels(_window.ActualWidth);
        var height = _monitor.ToPixels(_window.ActualHeight);
        WindowStyles.SetBounds(_window.Handle, _origin.X, _origin.Y, width, height);
    }

    public void Dispose()
    {
        _window?.Close();
        _window = null;
    }
}
