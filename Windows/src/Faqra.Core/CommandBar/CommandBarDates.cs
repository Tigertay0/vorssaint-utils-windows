// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/CommandBar/CommandBarDates.swift

using System.Globalization;
using System.Text;

namespace Faqra.Core.CommandBar;

/// <summary>
/// The questions about time that people put into a search field: what day is it three weeks
/// from now, how many days until a date, what time it is somewhere else. Answered from an
/// injected clock and time zone — never <c>DateTime.Now</c> — so a given input always answers
/// the same way in a test and in the running app.
///
/// Three answer shapes are ported verbatim from upstream Swift (relative date, days until a
/// written date, time elsewhere). A fourth — weekday phrases such as "next friday" — has no
/// upstream equivalent; the digest that drove this port flagged its absence as a gap against
/// the stage-1 acceptance criteria, so it is added here as a Faqra-only extension.
/// </summary>
public static class CommandBarDates
{
    public readonly record struct Result(string Formatted, string Detail);

    // A phrase longer than this is prose, not a command, mirroring the Swift entry gate.
    private const int MaxInputLength = 80;

    // MARK: - Parser vocabulary (verbatim from CommandBarDates.swift, already folded: no
    // accents, lower case). Multilingual because the upstream bar answers in whatever
    // language the interface speaks; the weekday addition below is deliberately English-only.
    private static readonly HashSet<string> TodayWords = new()
    {
        "today", "hoje", "heute", "hoy", "oggi", "aujourdhui", "aujourd'hui",
        "segodnya", "сегодня", "bugun", "今日", "오늘", "今天",
    };

    private static readonly HashSet<string> FutureWords = new()
    {
        "in", "em", "daqui", "dentro", "nach", "tra", "fra", "dans", "через", "sonra", "後", "후", "后", "from",
    };

    private static readonly HashSet<string> PastWords = new()
    {
        "ago", "atras", "ha", "hace", "vor", "fa", "назад", "once", "前", "전",
    };

    private static readonly HashSet<string> UntilWords = new()
    {
        "until", "till", "ate", "hasta", "bis", "fino", "jusqu", "до", "kadar", "까지", "까지는",
    };

    private static readonly HashSet<string> TimeWords = new()
    {
        "time", "hora", "horas", "hour", "uhrzeit", "uhr", "ora", "heure",
        "время", "saat", "時刻", "時間", "시간", "时间", "现在",
    };

    private enum RelativeUnit { Day, Week, Month, Year }

    private static readonly IReadOnlyDictionary<string, RelativeUnit> UnitWords = BuildUnitWords();

    private static readonly IReadOnlyDictionary<string, DayOfWeek> WeekdaysByName = new Dictionary<string, DayOfWeek>
    {
        ["sunday"] = DayOfWeek.Sunday, ["monday"] = DayOfWeek.Monday, ["tuesday"] = DayOfWeek.Tuesday,
        ["wednesday"] = DayOfWeek.Wednesday, ["thursday"] = DayOfWeek.Thursday,
        ["friday"] = DayOfWeek.Friday, ["saturday"] = DayOfWeek.Saturday,
    };

    // Places whose everyday name differs from the one the IANA time zone id uses.
    private static readonly IReadOnlyDictionary<string, string> PlaceAliases = new Dictionary<string, string>
    {
        ["londres"] = "London", ["lisboa"] = "Lisbon", ["roma"] = "Rome",
        ["moscou"] = "Moscow", ["moscovo"] = "Moscow", ["москва"] = "Moscow",
        ["nova york"] = "New York", ["nueva york"] = "New York", ["nova iorque"] = "New York",
        ["cidade do mexico"] = "Mexico City", ["ciudad de mexico"] = "Mexico City",
        ["pequim"] = "Beijing", ["pequin"] = "Beijing", ["toquio"] = "Tokyo", ["tokio"] = "Tokyo",
        ["genebra"] = "Geneva", ["viena"] = "Vienna", ["copenhague"] = "Copenhagen",
        ["praga"] = "Prague", ["atenas"] = "Athens", ["varsovia"] = "Warsaw",
        ["bruxelas"] = "Brussels", ["zurique"] = "Zurich", ["munique"] = "Munich",
        ["colonia"] = "Cologne", ["estocolmo"] = "Stockholm", ["hamburgo"] = "Hamburg",
    };

    // .NET/Windows has no equivalent of enumerating `TimeZone.knownTimeZoneIdentifiers` by IANA
    // city name (`TimeZoneInfo.GetSystemTimeZones()` is keyed by Windows names, not IANA ones).
    // ICU-backed `FindSystemTimeZoneById` still resolves IANA ids directly on .NET 8 / Windows
    // 10+, so this is a curated table of common cities built the same way (last path component,
    // underscores to spaces) rather than the full tzdata; a city outside this set simply yields
    // no answer instead of throwing.
    private static readonly string[] CuratedZoneIds =
    {
        "Europe/London", "Europe/Lisbon", "Europe/Rome", "Europe/Moscow", "Europe/Paris", "Europe/Berlin",
        "Europe/Madrid", "Europe/Vienna", "Europe/Copenhagen", "Europe/Prague", "Europe/Athens", "Europe/Warsaw",
        "Europe/Brussels", "Europe/Zurich", "Europe/Stockholm", "Europe/Dublin", "Europe/Amsterdam",
        "America/New_York", "America/Chicago", "America/Denver", "America/Los_Angeles", "America/Mexico_City",
        "America/Sao_Paulo", "America/Argentina/Buenos_Aires", "America/Toronto",
        "Asia/Tokyo", "Asia/Shanghai", "Asia/Hong_Kong", "Asia/Singapore", "Asia/Seoul", "Asia/Kolkata", "Asia/Dubai",
        "Africa/Cairo", "Africa/Johannesburg", "Australia/Sydney", "Pacific/Auckland",
    };

    private static readonly IReadOnlyDictionary<string, TimeZoneInfo> ZonesByCity = BuildZonesByCity();

    // MARK: - Answering

    /// <summary>
    /// Answers one of the three upstream shapes, or the Faqra-only weekday shape, or nothing.
    /// <paramref name="now"/> is the instant to answer from and <paramref name="local"/> the
    /// observer's zone (what "today" means); both are injected so a given input always answers
    /// the same way.
    /// </summary>
    public static Result? Evaluate(string input, DateTimeOffset now, TimeZoneInfo local, CultureInfo? culture = null)
    {
        var effectiveCulture = culture ?? CultureInfo.InvariantCulture;
        var trimmed = input.Trim();
        if (trimmed.Length > MaxInputLength) return null;

        var tokens = Tokenize(trimmed);
        if (tokens.Length == 0) return null;

        // Faqra addition: a lone weekday name ("friday") is specific enough that it never
        // collides with an ordinary one-word search the way a bare number would, so it is
        // allowed to answer at a single token — unlike every shape below, which keeps
        // upstream's `tokens.count >= 2` requirement (a bare word or number is a search).
        if (tokens.Length == 1) return WeekdayPhrase(tokens, now, local, effectiveCulture);

        return RelativeDate(tokens, now, local, effectiveCulture)
            ?? DaysUntil(tokens, now, local, effectiveCulture)
            ?? WeekdayPhrase(tokens, now, local, effectiveCulture)
            ?? TimeSomewhereElse(tokens, now, effectiveCulture);
    }

    /// "in 3 weeks", "daqui 10 dias", "3 days ago", "today + 10 days".
    private static Result? RelativeDate(string[] tokens, DateTimeOffset now, TimeZoneInfo local, CultureInfo culture)
    {
        // The number and its unit have to sit together; everything else only decides
        // direction. First matching adjacent pair wins, scan stops there.
        int? amount = null;
        RelativeUnit? unit = null;
        for (var index = 0; index < tokens.Length - 1; index++)
        {
            if (int.TryParse(tokens[index], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
                && Math.Abs(value) <= 10_000
                && UnitWords.TryGetValue(tokens[index + 1], out var matchedUnit))
            {
                amount = value;
                unit = matchedUnit;
                break;
            }
        }
        if (amount is not int amountValue || unit is not RelativeUnit unitValue) return null;

        var hasToday = tokens.Any(TodayWords.Contains);
        var hasPast = tokens.Any(PastWords.Contains);
        var hasFuture = tokens.Any(FutureWords.Contains);
        var hasMinus = tokens.Contains("-");
        // Without a direction "3 days" is not a question; answering it would push aside
        // whatever the person was really searching for.
        if (!hasToday && !hasPast && !hasFuture) return null;

        var backwards = hasPast || hasMinus;
        var today = LocalToday(now, local);
        var target = AddUnit(today, unitValue, backwards ? -amountValue : amountValue);
        if (target is not DateOnly targetDate) return null;

        return new Result(LongDate(targetDate, culture), WeekdayName(targetDate, culture));
    }

    /// "days until 25/12", "dias ate 25/12": how far away a written date is.
    private static Result? DaysUntil(string[] tokens, DateTimeOffset now, TimeZoneInfo local, CultureInfo culture)
    {
        var untilIndex = Array.FindIndex(tokens, UntilWords.Contains);
        if (untilIndex < 0 || untilIndex + 1 >= tokens.Length) return null;

        var written = string.Join(' ', tokens.Skip(untilIndex + 1));
        var today = LocalToday(now, local);
        var target = ParseWrittenDate(written, today, culture);
        if (target is not DateOnly targetDate) return null;

        var days = Math.Abs(targetDate.DayNumber - today.DayNumber);
        return new Result(DayCountText(days), LongDate(targetDate, culture));
    }

    /// "time in tokyo", "hora em londres", "tokyo time".
    private static Result? TimeSomewhereElse(string[] tokens, DateTimeOffset now, CultureInfo culture)
    {
        if (!tokens.Any(TimeWords.Contains)) return null;

        // Whatever is left after the time word and the little joining words is the place
        // name, which may well be two words long.
        var rest = tokens.Where(t => !TimeWords.Contains(t) && !FutureWords.Contains(t)
            && t != "a" && t != "at" && t != "de").ToArray();
        if (rest.Length == 0) return null;

        var name = string.Join(' ', rest);
        // A place with no matching entry in the curated table just yields no answer.
        if (!ZonesByCity.TryGetValue(name, out var zone) && !ZonesByCity.TryGetValue(rest[^1], out zone)) return null;

        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var place = PlaceName(zone);
        return new Result(
            localNow.ToString("t", culture),
            $"{place} · {FullDate(DateOnly.FromDateTime(localNow.DateTime), culture)}");
    }

    /// Faqra addition (no upstream equivalent): "friday", "next friday", "this friday",
    /// "last friday" — English-only, case-insensitive (folded by <see cref="Tokenize"/> like
    /// every other phrase here).
    private static Result? WeekdayPhrase(string[] tokens, DateTimeOffset now, TimeZoneInfo local, CultureInfo culture)
    {
        string? qualifier;
        string weekdayToken;
        if (tokens.Length == 1)
        {
            qualifier = null;
            weekdayToken = tokens[0];
        }
        else if (tokens.Length == 2 && tokens[0] is "next" or "this" or "last")
        {
            qualifier = tokens[0];
            weekdayToken = tokens[1];
        }
        else
        {
            return null;
        }
        if (!WeekdaysByName.TryGetValue(weekdayToken, out var weekday)) return null;

        var today = LocalToday(now, local);
        // "next" = the first such weekday strictly after today. "this" (and the bare form,
        // which reads the same as "this") = today-or-later within the current Monday-start
        // (ISO) week; if that weekday already passed earlier this same week there is no
        // answer, by design — the phrase falls through to an ordinary search rather than
        // reaching back into last week. "last" mirrors "next": the most recent occurrence
        // strictly before today.
        DateOnly? target = qualifier switch
        {
            "next" => NextStrictlyAfter(today, weekday),
            "last" => LastStrictlyBefore(today, weekday),
            _ => ThisIsoWeekOnOrAfterToday(today, weekday),
        };
        if (target is not DateOnly targetDate) return null;

        return new Result(LongDate(targetDate, culture), WeekdayName(targetDate, culture));
    }

    private static DateOnly NextStrictlyAfter(DateOnly today, DayOfWeek weekday)
    {
        for (var offset = 1; offset <= 7; offset++)
        {
            var candidate = today.AddDays(offset);
            if (candidate.DayOfWeek == weekday) return candidate;
        }
        return today; // unreachable: every weekday occurs within the next 7 days
    }

    private static DateOnly LastStrictlyBefore(DateOnly today, DayOfWeek weekday)
    {
        for (var offset = 1; offset <= 7; offset++)
        {
            var candidate = today.AddDays(-offset);
            if (candidate.DayOfWeek == weekday) return candidate;
        }
        return today; // unreachable: every weekday occurs within the past 7 days
    }

    private static DateOnly? ThisIsoWeekOnOrAfterToday(DateOnly today, DayOfWeek weekday)
    {
        // .NET's DayOfWeek is Sunday=0..Saturday=6; ISO/Monday-start distance-from-Monday.
        var mondayOffset = ((int)today.DayOfWeek + 6) % 7;
        var monday = today.AddDays(-mondayOffset);
        var isoIndex = weekday == DayOfWeek.Sunday ? 6 : (int)weekday - 1; // Monday=0 .. Sunday=6
        var candidate = monday.AddDays(isoIndex);
        return candidate >= today ? candidate : null;
    }

    // MARK: - Writing and reading dates

    private static DateOnly LocalToday(DateTimeOffset now, TimeZoneInfo local) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, local).DateTime);

    private static DateOnly? AddUnit(DateOnly date, RelativeUnit unit, int amount)
    {
        try
        {
            return unit switch
            {
                RelativeUnit.Day => date.AddDays(amount),
                RelativeUnit.Week => date.AddDays(amount * 7),
                RelativeUnit.Month => date.AddMonths(amount),
                RelativeUnit.Year => date.AddYears(amount),
                _ => null,
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// A date the person wrote, read the way this culture writes dates. A year left out means
    /// the next time that day comes around, which is what someone counting days to a birthday
    /// means.
    private static DateOnly? ParseWrittenDate(string written, DateOnly today, CultureInfo culture)
    {
        if (DateTime.TryParseExact(written, culture.DateTimeFormat.ShortDatePattern, culture,
                DateTimeStyles.None, out var exact))
        {
            return DateOnly.FromDateTime(exact);
        }

        // Without a year the culture-exact parse above refuses, so fall back to reading
        // exactly two numbers out of the text and ordering them by day/month convention.
        var groups = DigitGroups(written);
        if (groups.Length != 2 || !int.TryParse(groups[0], out var first) || !int.TryParse(groups[1], out var second))
        {
            return null;
        }

        var dayFirst = DayComesFirst(culture);
        var day = dayFirst ? first : second;
        var month = dayFirst ? second : first;
        if (day is < 1 or > 31 || month is < 1 or > 12 || day > DateTime.DaysInMonth(today.Year, month)) return null;

        var candidate = new DateOnly(today.Year, month, day);
        return candidate < today ? new DateOnly(today.Year + 1, month, day) : candidate;
    }

    private static string[] DigitGroups(string text)
    {
        var groups = new List<string>();
        var current = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsDigit(ch))
            {
                current.Append(ch);
            }
            else if (current.Length > 0)
            {
                groups.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0) groups.Add(current.ToString());
        return groups.ToArray();
    }

    /// Whether this culture writes the day before the month in its short date pattern.
    private static bool DayComesFirst(CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.ShortDatePattern;
        var dayIndex = pattern.IndexOf('d');
        var monthIndex = pattern.IndexOf('M');
        return dayIndex >= 0 && monthIndex >= 0 && dayIndex < monthIndex;
    }

    private static string DayCountText(int days) => days == 1 ? "1 day" : $"{days} days";

    // Apple's `.long` date style never includes the weekday (e.g. "August 18, 2026"), unlike
    // `.full`. .NET's culture long-date pattern leads with "dddd, ", so that token (and the
    // separator after it) is stripped to match.
    private static string LongDate(DateOnly date, CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.LongDatePattern
            .Replace("dddd, ", string.Empty, StringComparison.Ordinal)
            .Replace("dddd", string.Empty, StringComparison.Ordinal)
            .Trim();
        return date.ToDateTime(TimeOnly.MinValue).ToString(pattern, culture);
    }

    // Apple's `.full` date style includes the weekday (e.g. "Tuesday, July 28, 2026"), which is
    // exactly .NET's standard "D" (long date) pattern.
    private static string FullDate(DateOnly date, CultureInfo culture) =>
        date.ToDateTime(TimeOnly.MinValue).ToString("D", culture);

    private static string WeekdayName(DateOnly date, CultureInfo culture) =>
        date.ToDateTime(TimeOnly.MinValue).ToString("dddd", culture);

    private static string PlaceName(TimeZoneInfo zone)
    {
        var lastSlash = zone.Id.LastIndexOf('/');
        var city = lastSlash >= 0 ? zone.Id[(lastSlash + 1)..] : zone.Id;
        return city.Replace('_', ' ');
    }

    // MARK: - Vocabulary construction

    private static Dictionary<string, RelativeUnit> BuildUnitWords()
    {
        var map = new Dictionary<string, RelativeUnit>();
        void Add(IEnumerable<string> names, RelativeUnit unit)
        {
            foreach (var name in names) map[name] = unit;
        }
        Add(new[]
        {
            "day", "days", "dia", "dias", "tag", "tage", "giorno", "giorni", "jour", "jours", "den", "dnya",
            "dney", "дня", "дней", "день", "gun", "gunler", "日", "일", "天",
        }, RelativeUnit.Day);
        Add(new[]
        {
            "week", "weeks", "semana", "semanas", "woche", "wochen", "settimana", "settimane", "semaine",
            "semaines", "nedelya", "недели", "недель", "неделя", "hafta", "週", "주", "周", "星期",
        }, RelativeUnit.Week);
        Add(new[]
        {
            "month", "months", "mes", "meses", "monat", "monate", "mese", "mesi", "mois", "месяц", "месяца",
            "месяцев", "ay", "月", "월", "个月",
        }, RelativeUnit.Month);
        Add(new[]
        {
            "year", "years", "ano", "anos", "jahr", "jahre", "anno", "anni", "an", "ans", "annee", "annees",
            "god", "года", "лет", "год", "yil", "年", "년",
        }, RelativeUnit.Year);
        return map;
    }

    private static Dictionary<string, TimeZoneInfo> BuildZonesByCity()
    {
        var map = new Dictionary<string, TimeZoneInfo>();
        foreach (var id in CuratedZoneIds)
        {
            TimeZoneInfo zone;
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                continue; // missing zone: no answer, not a crash
            }
            catch (InvalidTimeZoneException)
            {
                continue;
            }

            var lastSlash = id.LastIndexOf('/');
            var cityRaw = (lastSlash >= 0 ? id[(lastSlash + 1)..] : id).Replace('_', ' ');
            // First identifier wins, mirroring upstream's reliance on a stable/sorted list.
            map.TryAdd(Normalize(cityRaw), zone);
        }
        foreach (var (alias, city) in PlaceAliases)
        {
            if (map.TryGetValue(Normalize(city), out var zone)) map[Normalize(alias)] = zone;
        }
        return map;
    }

    // MARK: - Tokenizing

    private static string[] Tokenize(string trimmed) =>
        Normalize(trimmed).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    // Case/diacritic fold, self-contained (this file has no dependency on the shared search
    // normalizer, which does not exist yet on the Windows side): NFKD-decompose then drop
    // non-spacing marks, lower-case under the invariant culture so a Turkish "I" never turns
    // into a dotless "ı" and breaks an otherwise-plain match.
    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch);
        }
        return builder.ToString().ToLowerInvariant();
    }
}
