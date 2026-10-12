// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text;
using System.Text.RegularExpressions;

namespace Faqra.Core.Agents;

public enum MdBlockKind { Paragraph, Heading, Bullet, Numbered, Quote, Code, Rule }

public enum MdStyle { Plain, Bold, Italic, Code, Link }

/// <summary>A run of text in one style. For a link, Text is what shows and Url is where it points.</summary>
public sealed record MdInline(string Text, MdStyle Style, string? Url = null);

/// <summary>
/// One block of Claude's message. Inlines hold its styled text (empty for Code and Rule). Level is a heading's level
/// (1 to 6) or a list item's nesting (0 at the margin); Marker is a numbered item's number ("3."); Code is a code block's
/// text, indentation kept.
/// </summary>
public sealed record MdBlock(MdBlockKind Kind, IReadOnlyList<MdInline> Inlines, int Level = 0, string Marker = "", string Code = "");

/// <summary>
/// The Markdown Claude writes, read into blocks a card can style: headings, paragraphs, lists, quotes, code blocks and
/// rules, with bold, italic, inline code and links inside them. Anything else (tables, HTML) stays as its plain text.
/// </summary>
public static partial class MarkdownLite
{
    public static IReadOnlyList<MdBlock> Parse(string? text)
    {
        var blocks = new List<MdBlock>();
        if (string.IsNullOrEmpty(text))
        {
            return blocks;
        }
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var paragraph = new List<string>();
        void Flush()
        {
            if (paragraph.Count > 0)
            {
                blocks.Add(new MdBlock(MdBlockKind.Paragraph, Inlines(string.Join(" ", paragraph))));
                paragraph.Clear();
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                Flush();
                var fence = trimmed[..3];
                var code = new List<string>();
                for (i++; i < lines.Length && !lines[i].Trim().StartsWith(fence, StringComparison.Ordinal); i++)
                {
                    code.Add(lines[i]);
                }
                blocks.Add(new MdBlock(MdBlockKind.Code, [], Code: string.Join("\n", code)));
                continue;
            }
            if (trimmed.Length == 0)
            {
                Flush();
                continue;
            }
            if (HeadingPattern().Match(trimmed) is { Success: true } heading)
            {
                Flush();
                blocks.Add(new MdBlock(MdBlockKind.Heading, Inlines(heading.Groups[2].Value.Trim()), Level: heading.Groups[1].Value.Length));
                continue;
            }
            if (RulePattern().IsMatch(trimmed))
            {
                Flush();
                blocks.Add(new MdBlock(MdBlockKind.Rule, []));
                continue;
            }
            var nesting = (line.Length - line.TrimStart().Length) / 2;
            if (BulletPattern().Match(trimmed) is { Success: true } bullet)
            {
                Flush();
                blocks.Add(new MdBlock(MdBlockKind.Bullet, Inlines(bullet.Groups[1].Value), Level: nesting));
                continue;
            }
            if (NumberedPattern().Match(trimmed) is { Success: true } numbered)
            {
                Flush();
                blocks.Add(new MdBlock(MdBlockKind.Numbered, Inlines(numbered.Groups[2].Value), Level: nesting, Marker: numbered.Groups[1].Value + "."));
                continue;
            }
            if (trimmed.StartsWith('>'))
            {
                Flush();
                blocks.Add(new MdBlock(MdBlockKind.Quote, Inlines(trimmed.TrimStart('>').Trim())));
                continue;
            }
            paragraph.Add(trimmed);
        }
        Flush();
        return blocks;
    }

    /// <summary>Styled runs from one line of Markdown. A mark without a closing partner stays as text.</summary>
    public static IReadOnlyList<MdInline> Inlines(string text)
    {
        var runs = new List<MdInline>();
        var plain = new StringBuilder();
        void Emit(string value, MdStyle style, string? url = null)
        {
            if (plain.Length > 0)
            {
                runs.Add(new MdInline(plain.ToString(), MdStyle.Plain));
                plain.Clear();
            }
            if (value.Length > 0)
            {
                runs.Add(new MdInline(value, style, url));
            }
        }

        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            var rest = text.AsSpan(i);
            if (c == '`' && Close(text, i + 1, "`") is { } codeEnd)
            {
                Emit(text[(i + 1)..codeEnd], MdStyle.Code);
                i = codeEnd + 1;
                continue;
            }
            if (rest.StartsWith("***") && Close(text, i + 3, "***") is { } strongEnd)
            {
                Emit(text[(i + 3)..strongEnd], MdStyle.Bold);
                i = strongEnd + 3;
                continue;
            }
            if ((rest.StartsWith("**") || rest.StartsWith("__")) && Close(text, i + 2, text.Substring(i, 2)) is { } boldEnd)
            {
                Emit(text[(i + 2)..boldEnd], MdStyle.Bold);
                i = boldEnd + 2;
                continue;
            }
            // An underscore inside a word (snake_case) is not emphasis; a star may be (a*b*c).
            if ((c == '*' || (c == '_' && (i == 0 || !char.IsLetterOrDigit(text[i - 1]))))
                && i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1])
                && Close(text, i + 1, c.ToString()) is { } italicEnd)
            {
                Emit(text[(i + 1)..italicEnd], MdStyle.Italic);
                i = italicEnd + 1;
                continue;
            }
            if (c == '[' && LinkPattern().Match(text, i) is { Success: true } link && link.Index == i)
            {
                Emit(link.Groups[1].Value, MdStyle.Link, link.Groups[2].Value);
                i += link.Length;
                continue;
            }
            plain.Append(c);
            i++;
        }
        Emit(string.Empty, MdStyle.Plain);
        return runs;
    }

    /// <summary>The message without its Markdown marks, one block per line: for short previews.</summary>
    public static string PlainText(string? text) => string.Join("\n", Parse(text)
        .Select(block => block.Kind switch
        {
            MdBlockKind.Code => block.Code,
            MdBlockKind.Rule => string.Empty,
            MdBlockKind.Bullet => "• " + Joined(block.Inlines),
            MdBlockKind.Numbered => block.Marker + " " + Joined(block.Inlines),
            _ => Joined(block.Inlines),
        })
        .Where(line => line.Length > 0));

    private static string Joined(IReadOnlyList<MdInline> inlines) => string.Concat(inlines.Select(inline => inline.Text));

    /// <summary>Where <paramref name="marker"/> closes a span that starts at <paramref name="from"/>; null when it does not, or the span is empty.</summary>
    private static int? Close(string text, int from, string marker)
    {
        if (from >= text.Length)
        {
            return null;
        }
        var at = text.IndexOf(marker, from, StringComparison.Ordinal);
        return at > from ? at : null;
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^(-{3,}|\*{3,}|_{3,})$")]
    private static partial Regex RulePattern();

    [GeneratedRegex(@"^[-*+]\s+(.*)$")]
    private static partial Regex BulletPattern();

    [GeneratedRegex(@"^(\d{1,3})[.)]\s+(.*)$")]
    private static partial Regex NumberedPattern();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)\)")]
    private static partial Regex LinkPattern();
}
