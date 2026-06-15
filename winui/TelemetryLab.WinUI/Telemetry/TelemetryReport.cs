// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel Ferreira
namespace TelemetryLab.WinUI.Telemetry;

public sealed record MetricSummary(
    string Name,
    string Group,
    double Average,
    double Minimum,
    double Maximum,
    double P95,
    double P99,
    double P1,
    double P01,
    double Median,
    double StandardDeviation,
    double Last,
    int Samples);

public sealed record TelemetryReport(
    string Source,
    string Title,
    int RowCount,
    int SensorCount,
    IReadOnlyList<string> Columns,
    IReadOnlyDictionary<string, IReadOnlyList<double?>> Numeric,
    IReadOnlyList<MetricSummary> Summaries)
{
    public static TelemetryReport Empty { get; } = new(
        string.Empty,
        "Nenhum relatório carregado",
        0,
        0,
        Array.Empty<string>(),
        new Dictionary<string, IReadOnlyList<double?>>(),
        Array.Empty<MetricSummary>());
}
