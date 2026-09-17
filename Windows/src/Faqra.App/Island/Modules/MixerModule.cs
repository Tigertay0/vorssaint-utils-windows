// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors NotchView.swift line 254: the island's mixer page is upstream's MixerSection, non-collapsible,
// on the notch's black surface.

using System.Windows;
using System.Windows.Controls;
using Faqra.App.Panel.Sections;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Services.Audio;

namespace Faqra.App.Island.Modules;

/// <summary>The mixer section on the island. Listens to the mixer only while it is on screen.</summary>
public sealed class MixerModule : UserControl
{
    private readonly ISettingsStore _store;
    private readonly Func<AppFeature, bool> _isAvailable;
    private readonly AppVolumeMixer _mixer;
    private MixerSectionView? _view;

    public MixerModule(ISettingsStore store, Func<AppFeature, bool> isAvailable, AppVolumeMixer mixer)
    {
        _store = store;
        _isAvailable = isAvailable;
        _mixer = mixer;
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        Attach();
    }

    private void Attach()
    {
        if (_view is not null)
        {
            return;
        }
        _view = new MixerSectionView(new SectionContext(_store, _isAvailable, _mixer), _mixer, SectionPalette.Island);
        Content = new ScrollViewer
        {
            Content = _view.Root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 4, 0),
        };
    }

    private void Detach()
    {
        _view?.Dispose();
        _view = null;
    }
}
