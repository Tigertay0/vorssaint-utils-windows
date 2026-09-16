// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors MetricHistory in Sources/Vorssaint/Services/Metrics/MetricFormat.swift (lines 412-433)

namespace Faqra.Core.Metrics;

/// <summary>The newest readings of one metric, oldest first, for a graph. Not thread-safe.</summary>
public sealed class MetricHistory
{
    /// <summary>Samples per graph, upstream's SystemMonitor.historyCapacity.</summary>
    public const int DefaultCapacity = 120;

    private readonly Queue<double> _values;
    private readonly int _capacity;

    public MetricHistory(int capacity = DefaultCapacity)
    {
        _capacity = Math.Max(1, capacity);
        _values = new Queue<double>(_capacity + 1);
    }

    public IReadOnlyList<double> Values => _values.ToArray();

    public void Push(double value)
    {
        _values.Enqueue(value);
        while (_values.Count > _capacity)
        {
            _values.Dequeue();
        }
    }

    /// <summary>The values while a graph is on screen; nothing otherwise, without losing what was collected.</summary>
    public IReadOnlyList<double> PublishedValues(bool whileVisible) => whileVisible ? Values : [];
}
