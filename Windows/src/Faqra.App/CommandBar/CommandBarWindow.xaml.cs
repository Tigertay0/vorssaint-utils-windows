// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors CommandBarView in Sources/Vorssaint/UI/CommandBar/CommandBarView.swift: the search field, the
// result list with inline section headings, the argument and confirm cards, the empty state and the
// footer. CommandBarController owns every decision; this only draws what it is handed.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Faqra.Core.CommandBar;
using Faqra.Core.Localization;
using Wpf.Ui.Controls;

namespace Faqra.App.CommandBar;

/// <summary>A list row with the heading that starts its section, if any.</summary>
internal sealed record CommandBarListItem(CommandBarRow Row, string? SectionTitle);

public partial class CommandBarWindow
{
    private readonly List<Border> _rowViews = [];
    private bool _settingQuery;

    public CommandBarWindow()
    {
        InitializeComponent();
        QueryBox.TextChanged += (_, _) =>
        {
            Placeholder.Visibility = QueryBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (!_settingQuery)
            {
                QueryChanged?.Invoke();
            }
        };
        PreviewKeyDown += (_, e) => KeyPressed?.Invoke(e);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // A launcher overlay, not an app window: kept out of Alt+Tab and the taskbar.
        Faqra.Win32.Windows.WindowStyles.MakeToolWindow(Handle);
    }

    internal event Action? QueryChanged;

    internal event Action<KeyEventArgs>? KeyPressed;

    internal event Action<int>? RowHovered;

    internal event Action<int>? RowClicked;

    public IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

    internal string Query
    {
        get => QueryBox.Text;
        set
        {
            _settingQuery = true;
            QueryBox.Text = value;
            QueryBox.CaretIndex = value.Length;
            _settingQuery = false;
        }
    }

    internal void FocusQuery()
    {
        QueryBox.Focus();
        Keyboard.Focus(QueryBox);
    }

    internal void SetPlaceholder(string text) => Placeholder.Text = text;

    internal void SetModeChip(string? title)
    {
        ModeChip.Visibility = title is null ? Visibility.Collapsed : Visibility.Visible;
        ModeChipText.Text = title ?? string.Empty;
    }

    internal void SetFooter(string shortcut, string hints)
    {
        FooterShortcut.Text = shortcut;
        FooterHints.Text = hints;
    }

    internal void RenderList(IReadOnlyList<CommandBarListItem> items, int selected, string query, CommandBarStrings bar, Action? goHome)
    {
        Body.Children.Clear();
        _rowViews.Clear();
        if (items.Count == 0)
        {
            if (goHome is not null)
            {
                Body.Children.Add(EmptyState(bar, goHome));
            }
            return;
        }
        string? section = null;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.SectionTitle is { } title)
            {
                Body.Children.Add(SectionHeader(title, first: index == 0));
                section = title;
            }
            // A row under a heading does not repeat the heading as its subtitle (CommandBarView.swift:640-642).
            var row = section is not null && item.Row.Entry.Subtitle == section
                ? item.Row with { Entry = item.Row.Entry with { Subtitle = string.Empty } }
                : item.Row;
            var view = CommandBarRowView.Build(row, query, bar);
            var captured = index;
            view.MouseEnter += (_, _) => RowHovered?.Invoke(captured);
            view.MouseLeftButtonUp += (_, _) => RowClicked?.Invoke(captured);
            _rowViews.Add(view);
            Body.Children.Add(view);
        }
        Select(selected);
    }

    /// <summary>Moves the highlight without rebuilding the rows, and keeps the selected row in view.</summary>
    internal void Select(int index)
    {
        for (var i = 0; i < _rowViews.Count; i++)
        {
            CommandBarRowView.SetSelected(_rowViews[i], i == index);
        }
        if (index >= 0 && index < _rowViews.Count)
        {
            _rowViews[index].BringIntoView();
        }
    }

    internal void RenderCard(UIElement card)
    {
        Body.Children.Clear();
        _rowViews.Clear();
        Body.Children.Add(card);
    }

    private static System.Windows.Controls.TextBlock SectionHeader(string title, bool first)
    {
        var header = new System.Windows.Controls.TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(9, first ? 4 : 10, 9, 4),
        };
        header.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        return header;
    }

    private static UIElement EmptyState(CommandBarStrings bar, Action goHome)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 20, 0, 20), HorizontalAlignment = HorizontalAlignment.Center };
        var title = new System.Windows.Controls.TextBlock { Text = bar.NoResultsTitle, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center };
        title.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        panel.Children.Add(title);
        var action = new Wpf.Ui.Controls.Button
        {
            Content = bar.NoResultsAction,
            Appearance = ControlAppearance.Secondary,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            Focusable = false,
        };
        action.Click += (_, _) => goHome();
        panel.Children.Add(action);
        return panel;
    }

    /// <summary>The argument card: the row's title and how to finish (CommandBarView.swift:736-755).</summary>
    internal static UIElement ArgumentCard(CommandBarRow row, string hint) =>
        Card(row, hint, danger: false, buttons: null);

    /// <summary>The confirm card, tinted with the caution color, with buttons for mouse users (CommandBarView.swift:757-790).</summary>
    internal static UIElement ConfirmCard(CommandBarRow row, CommandBarStrings bar, Action confirm, Action cancel)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var cancelButton = new Wpf.Ui.Controls.Button { Content = bar.CancelButton, Appearance = ControlAppearance.Secondary, Focusable = false, Margin = new Thickness(0, 0, 6, 0) };
        cancelButton.Click += (_, _) => cancel();
        var confirmButton = new Wpf.Ui.Controls.Button { Content = bar.ConfirmButton, Appearance = ControlAppearance.Danger, Focusable = false };
        confirmButton.Click += (_, _) => confirm();
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(confirmButton);
        return Card(row, bar.ConfirmHint, danger: true, buttons);
    }

    private static UIElement Card(CommandBarRow row, string hint, bool danger, UIElement? buttons)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = CommandBarRowView.IconView(row);
        grid.Children.Add(icon);
        var text = new StackPanel { Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        var titleText = new System.Windows.Controls.TextBlock
        {
            Text = row.ConfirmationPrompt ?? row.Entry.Title,
            FontSize = 14,
            FontWeight = danger ? FontWeights.SemiBold : FontWeights.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        titleText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        text.Children.Add(titleText);
        var hintText = new System.Windows.Controls.TextBlock { Text = hint, FontSize = 12 };
        hintText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        text.Children.Add(hintText);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        if (buttons is not null)
        {
            Grid.SetColumn(buttons, 2);
            grid.Children.Add(buttons);
        }
        var border = new Border { Padding = new Thickness(12), CornerRadius = new CornerRadius(8), Child = grid, Margin = new Thickness(4) };
        if (danger)
        {
            border.SetResourceReference(Border.BackgroundProperty, "SystemFillColorCriticalBackgroundBrush");
        }
        return border;
    }
}
