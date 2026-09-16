// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors DoneStep in Sources/Vorssaint/UI/Onboarding/OnboardingView.swift (lines 507-541)

using System.Windows.Controls;
using Faqra.App.Tray;
using Faqra.Core.Localization;

namespace Faqra.App.Onboarding;

public partial class DoneStep : UserControl
{
    public DoneStep()
    {
        InitializeComponent();
        var s = L10n.Shared.S;
        GlyphImage.Source = TrayIconBitmap.Render(224, active: false, lightTaskbar: false);
        TitleText.Text = s.OnboardingDoneTitle;
        BodyText.Text = s.OnboardingDoneBody;
        HintText.Text = s.OnboardingDoneHint;
    }
}
