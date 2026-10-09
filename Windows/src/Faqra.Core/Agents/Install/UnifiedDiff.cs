// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Core.Agents.Install;

/// <summary>A line diff in unified form, for showing the owner exactly what an install changes.</summary>
public static class UnifiedDiff
{
    /// <summary>Above this many line pairs the diff gives up on alignment and shows a full replacement.</summary>
    private const long MaxCells = 4_000_000;

    public enum LineKind { Hunk, Context, Removed, Added }

    public readonly record struct DiffLine(LineKind Kind, string Text);

    private enum Op { Equal, Remove, Add }

    public static IReadOnlyList<DiffLine> Lines(string before, string after, int context = 3)
    {
        var a = Split(before);
        var b = Split(after);
        var ops = Script(a, b);
        var aBefore = new int[ops.Count + 1];
        var bBefore = new int[ops.Count + 1];
        for (var i = 0; i < ops.Count; i++)
        {
            aBefore[i + 1] = aBefore[i] + (ops[i].Op == Op.Add ? 0 : 1);
            bBefore[i + 1] = bBefore[i] + (ops[i].Op == Op.Remove ? 0 : 1);
        }

        var result = new List<DiffLine>();
        var index = 0;
        while (index < ops.Count)
        {
            if (ops[index].Op == Op.Equal)
            {
                index++;
                continue;
            }
            var start = Math.Max(0, index - context);
            var end = index;
            while (true)
            {
                while (end < ops.Count && ops[end].Op != Op.Equal)
                {
                    end++;
                }
                var next = end;
                while (next < ops.Count && ops[next].Op == Op.Equal)
                {
                    next++;
                }
                if (next < ops.Count && next - end <= 2 * context)
                {
                    end = next;
                    continue;
                }
                end = Math.Min(ops.Count, end + context);
                break;
            }
            var aCount = aBefore[end] - aBefore[start];
            var bCount = bBefore[end] - bBefore[start];
            var aStart = aBefore[start] + (aCount == 0 ? 0 : 1);
            var bStart = bBefore[start] + (bCount == 0 ? 0 : 1);
            result.Add(new DiffLine(LineKind.Hunk, $"@@ -{aStart},{aCount} +{bStart},{bCount} @@"));
            for (var i = start; i < end; i++)
            {
                var kind = ops[i].Op switch { Op.Remove => LineKind.Removed, Op.Add => LineKind.Added, _ => LineKind.Context };
                result.Add(new DiffLine(kind, ops[i].Text));
            }
            index = end;
        }
        return result;
    }

    private static string[] Split(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }
        var lines = text.Replace("\r\n", "\n").Split('\n');
        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    private static List<(Op Op, string Text)> Script(string[] a, string[] b)
    {
        var ops = new List<(Op, string)>();
        if ((long)a.Length * b.Length > MaxCells)
        {
            ops.AddRange(a.Select(line => (Op.Remove, line)));
            ops.AddRange(b.Select(line => (Op.Add, line)));
            return ops;
        }
        var lcs = new int[a.Length + 1, b.Length + 1];
        for (var x = a.Length - 1; x >= 0; x--)
        {
            for (var y = b.Length - 1; y >= 0; y--)
            {
                lcs[x, y] = a[x] == b[y] ? lcs[x + 1, y + 1] + 1 : Math.Max(lcs[x + 1, y], lcs[x, y + 1]);
            }
        }
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (a[i] == b[j])
            {
                ops.Add((Op.Equal, a[i]));
                i++;
                j++;
            }
            else if (lcs[i + 1, j] >= lcs[i, j + 1])
            {
                ops.Add((Op.Remove, a[i++]));
            }
            else
            {
                ops.Add((Op.Add, b[j++]));
            }
        }
        while (i < a.Length)
        {
            ops.Add((Op.Remove, a[i++]));
        }
        while (j < b.Length)
        {
            ops.Add((Op.Add, b[j++]));
        }
        return ops;
    }
}
