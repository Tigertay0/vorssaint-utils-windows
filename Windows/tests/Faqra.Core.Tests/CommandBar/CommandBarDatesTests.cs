// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/CommandBar/CommandBarDates.swift

using System.Globalization;
using Faqra.Core.CommandBar;

namespace Faqra.Core.Tests;

// `now` is fixed to the same anchor the upstream Swift suite uses (Tests/MetricsTests.swift,
// digest section 9.5): 2026-07-28, a Tuesday, so every relative/weekday answer below is
// reproducible without touching the host clock.
public class CommandBarDatesTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    // 9.5: relative-date answer shape, ported verbatim (en-US unless noted).
    [Theory]
    [InlineData("in 3 weeks", "August 18, 2026")]
    [InlineData("3 days ago", "July 25, 2026")]
    [InlineData("ha 3 dias", "July 25, 2026")]
    [InlineData("today + 10 days", "August 7, 2026")]
    [InlineData("today - 10 days", "July 18, 2026")]
    public void RelativeDate_MatchesUpstreamTable(string input, string expectedFormatted)
    {
        var result = CommandBarDates.Evaluate(input, Now, Utc, EnUs);
        Assert.NotNull(result);
        Assert.Equal(expectedFormatted, result!.Value.Formatted);
    }

    [Fact]
    public void RelativeDate_PortugueseLocale_UsesPtBrLongDate()
    {
        var result = CommandBarDates.Evaluate("daqui 10 dias", Now, Utc, PtBr);
        Assert.NotNull(result);
        Assert.Equal("7 de agosto de 2026", result!.Value.Formatted);
    }

    [Fact]
    public void RelativeDate_DetailIsTheLocalizedWeekday()
    {
        var result = CommandBarDates.Evaluate("in 3 weeks", Now, Utc, EnUs);
        Assert.NotNull(result);
        Assert.Equal("Tuesday", result!.Value.Detail);
    }

    // 9.5/9.4: a bare "N unit" phrase (no today/past/future word) is a search, not a question.
    [Fact]
    public void RelativeDate_WithoutDirection_ReturnsNull() =>
        Assert.Null(CommandBarDates.Evaluate("3 days", Now, Utc, EnUs));

    // 9.2: "days until <date>" — no explicit year falls back to the day/month-order guess,
    // then the current year (or next year if that date already passed).
    [Fact]
    public void DaysUntil_MatchesUpstreamCount()
    {
        var result = CommandBarDates.Evaluate("days until 12/25", Now, Utc, EnUs);
        Assert.NotNull(result);
        Assert.Equal("150 days", result!.Value.Formatted);
        Assert.Equal("December 25, 2026", result.Value.Detail);
    }

    [Fact]
    public void DaysUntil_SingleDayIsSingular()
    {
        var result = CommandBarDates.Evaluate("until 7/29", Now, Utc, EnUs);
        Assert.NotNull(result);
        Assert.Equal("1 day", result!.Value.Formatted);
    }

    // 9.3: "time in <city>" / "hora em <city>" — the multilingual joiner words and the
    // Portuguese/Spanish place-alias table both come straight from the Swift source.
    [Theory]
    [InlineData("time in tokyo", "Tokyo")]
    [InlineData("hora em londres", "London")]
    public void TimeSomewhereElse_DetailStartsWithThePlace(string input, string expectedPlacePrefix)
    {
        var result = CommandBarDates.Evaluate(input, Now, Utc, EnUs);
        Assert.NotNull(result);
        Assert.StartsWith(expectedPlacePrefix, result!.Value.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void TimeSomewhereElse_TokyoIsNineHoursAheadOfUtc()
    {
        var result = CommandBarDates.Evaluate("time in tokyo", Now, Utc, EnUs);
        Assert.NotNull(result);
        Assert.Equal("9:00 AM", result!.Value.Formatted);
    }

    // 9.6 / task requirement: a place with no matching zone is wrapped to "no answer", not a throw.
    [Fact]
    public void TimeSomewhereElse_UnknownPlace_ReturnsNull() =>
        Assert.Null(CommandBarDates.Evaluate("time in nowheresville", Now, Utc, EnUs));

    [Fact]
    public void TimeSomewhereElse_WithoutPlace_ReturnsNull() =>
        Assert.Null(CommandBarDates.Evaluate("time", Now, Utc, EnUs));

    // 9.4: the regression set every false-positive shape must still fall through as `null`.
    [Theory]
    [InlineData("1password")]
    [InlineData("2 monitors")]
    [InlineData("3 tags")]
    [InlineData("notes")]
    [InlineData("day one")]
    [InlineData("5 minutes")]
    [InlineData("2026-07-28")]
    [InlineData("the 3 body problem")]
    [InlineData("2+2")]
    [InlineData("100 km to mi")]
    public void Evaluate_NegativeSpace_NeverHijacksASearch(string input) =>
        Assert.Null(CommandBarDates.Evaluate(input, Now, Utc, EnUs));

    // --- Faqra addition: weekday phrases (no upstream equivalent, see the digest's gap note) ---

    // Design decision (documented again beside the implementation): "next <weekday>" is the
    // first such weekday strictly after today, so it always lands 1-7 days out and never
    // answers with today itself even when today is that weekday.
    [Theory]
    [InlineData("next sunday", "August 2, 2026")]
    [InlineData("next monday", "August 3, 2026")]
    [InlineData("next tuesday", "August 4, 2026")]
    [InlineData("next wednesday", "July 29, 2026")]
    [InlineData("next thursday", "July 30, 2026")]
    [InlineData("next friday", "July 31, 2026")]
    [InlineData("next saturday", "August 1, 2026")]
    public void NextWeekday_AllSevenDays_AnswerInline(string input, string expectedFormatted)
    {
        var result = CommandBarDates.Evaluate(input, Now, Utc, EnUs);
        Assert.NotNull(result);
        Assert.Equal(expectedFormatted, result!.Value.Formatted);
    }

    // Required acceptance (design doc line 112, digest section 11): must answer inline.
    [Fact]
    public void NextFriday_RequiredAcceptance_AnswersInline()
    {
        var result = CommandBarDates.Evaluate("next friday", Now, Utc, EnUs);
        Assert.NotNull(result);
        Assert.Equal("July 31, 2026", result!.Value.Formatted);
        Assert.Equal("Friday", result.Value.Detail);
    }

    // Design decision: "this <weekday>" is today-or-later within the current Monday-start
    // (ISO) week. Monday already passed this week (today is Tuesday), so it alone has no
    // answer here — the phrase falls through to an ordinary search rather than reaching back.
    [Theory]
    [InlineData("this monday", null)]
    [InlineData("this tuesday", "July 28, 2026")]
    [InlineData("this wednesday", "July 29, 2026")]
    [InlineData("this thursday", "July 30, 2026")]
    [InlineData("this friday", "July 31, 2026")]
    [InlineData("this saturday", "August 1, 2026")]
    [InlineData("this sunday", "August 2, 2026")]
    public void ThisWeekday_AllSevenDays_TodayOrLaterWithinIsoWeek(string input, string? expectedFormatted)
    {
        var result = CommandBarDates.Evaluate(input, Now, Utc, EnUs);
        if (expectedFormatted is null)
        {
            Assert.Null(result);
            return;
        }
        Assert.NotNull(result);
        Assert.Equal(expectedFormatted, result!.Value.Formatted);
    }

    // Design decision: "last <weekday>" mirrors "next" — the most recent occurrence strictly
    // before today, so it always lands 1-7 days back, symmetric with the "next" definition.
    [Theory]
    [InlineData("last sunday", "July 26, 2026")]
    [InlineData("last monday", "July 27, 2026")]
    [InlineData("last tuesday", "July 21, 2026")]
    [InlineData("last wednesday", "July 22, 2026")]
    [InlineData("last thursday", "July 23, 2026")]
    [InlineData("last friday", "July 24, 2026")]
    [InlineData("last saturday", "July 25, 2026")]
    public void LastWeekday_AllSevenDays_AnswerInline(string input, string expectedFormatted)
    {
        var result = CommandBarDates.Evaluate(input, Now, Utc, EnUs);
        Assert.NotNull(result);
        Assert.Equal(expectedFormatted, result!.Value.Formatted);
    }

    // Design decision: a bare weekday name reads the same as "this <weekday>" — the most
    // common everyday meaning of just saying "friday" is this week's Friday.
    [Fact]
    public void BareWeekday_MatchesThisWeekday()
    {
        var bare = CommandBarDates.Evaluate("friday", Now, Utc, EnUs);
        var thisWeek = CommandBarDates.Evaluate("this friday", Now, Utc, EnUs);
        Assert.Equal(thisWeek, bare);
    }

    [Fact]
    public void BareWeekday_PastThisWeek_ReturnsNull() =>
        Assert.Null(CommandBarDates.Evaluate("monday", Now, Utc, EnUs));

    // Weekday names are English-only and case-insensitive, matching every other Command Bar
    // phrase (folded through the same normalization as the number/unit vocabulary).
    [Theory]
    [InlineData("NEXT FRIDAY")]
    [InlineData("Next Friday")]
    [InlineData("FRIDAY")]
    public void Weekday_IsCaseInsensitive(string input) =>
        Assert.NotNull(CommandBarDates.Evaluate(input, Now, Utc, EnUs));

    // An abbreviation or an unrecognized qualifier is not part of this vocabulary; it must
    // fall through instead of guessing.
    [Theory]
    [InlineData("next fri")]
    [InlineData("nextish friday")]
    [InlineData("next friday extra")]
    public void Weekday_UnrecognizedShape_ReturnsNull(string input) =>
        Assert.Null(CommandBarDates.Evaluate(input, Now, Utc, EnUs));
}
