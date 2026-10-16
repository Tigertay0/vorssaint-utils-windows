// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.App.Island.Modules;
using Faqra.Core.Features;
using Faqra.Core.Island;

namespace Faqra.App.Island;

/// <summary>The island's side of Home: building it, where its rows lead, and which rows Windows can fill.</summary>
public sealed partial class IslandController
{
    private HomeModule? _homeModule;

    private HomeModule BuildHomeModule()
    {
        var module = new HomeModule(_store, _nowPlaying, _agents, AppServices.Current.Timer, _systemMonitor, HomeItemAvailable);
        module.SectionRequested += target => Expand(takeFocus: false, target);
        module.SessionRequested += sessionId =>
        {
            _agentsFocus = sessionId;
            Expand(takeFocus: false, IslandModule.FaqraAgents);
        };
        _homeModule = module;
        return module;
    }

    /// <summary>A Home row is offered only when its source exists on this PC.</summary>
    private bool HomeItemAvailable(HomeItem item) => item switch
    {
        HomeItem.NowPlaying => true,
        HomeItem.Agents => IslandModule.FaqraAgents.IsAvailable(_runtime.IsAvailable),
        HomeItem.Timer => IslandModule.Timer.IsAvailable(_runtime.IsAvailable),
        HomeItem.System => IslandModule.System.IsAvailable(_runtime.IsAvailable),
        HomeItem.Battery => _systemMonitor.HasBattery,
        _ => false,
    };

    /// <summary>Home is open with a System row, so the monitor should sample as it does for the System section.</summary>
    private bool HomeShowsSystem() => _module == IslandModule.FaqraHome && _homeModule?.Shows(HomeItem.System) == true;
}
