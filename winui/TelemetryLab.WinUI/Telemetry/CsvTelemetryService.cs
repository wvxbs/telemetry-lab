// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel Ferreira
using System.Globalization;
using System.Text;

namespace TelemetryLab.WinUI.Telemetry;

public sealed class CsvTelemetryService
{
    public async Task<TelemetryReport> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1024 * 128,
            useAsync: true);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        return Parse(path, bytes);
    }

    public TelemetryReport Parse(string source, byte[] bytes)
    {
        var text = Decode(bytes);
        var rows = ParseCsv(text);
        if (rows.Count == 0)
        {
            return TelemetryReport.Empty with { Source = source, Title = InferTitle(source) };
        }

        var columns = Dedupe(rows[0]);
        var width = columns.Count;
        var numeric = columns.ToDictionary(col => col, _ => new List<double?>());
        var rowCount = 0;

        foreach (var row in rows.Skip(1))
        {
            if (row.Count == 0 || row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rowCount++;
            for (var i = 0; i < width; i++)
            {
                var value = i < row.Count ? row[i] : string.Empty;
                numeric[columns[i]].Add(ParseNumber(value));
            }
        }

        var summaries = numeric
            .Select(pair => BuildSummary(pair.Key, pair.Value))
            .Where(summary => summary.Samples > 0)
            .OrderBy(summary => RankGroup(summary.Group))
            .ThenBy(summary => summary.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new TelemetryReport(
            source,
            InferTitle(source),
            rowCount,
            columns.Count,
            columns,
            numeric.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<double?>)pair.Value),
            summaries);
    }

    public IReadOnlyList<MetricSummary> CuratedMetrics(TelemetryReport report, string group, int limit = 8)
    {
        return report.Summaries
            .Where(metric => string.Equals(metric.Group, group, StringComparison.OrdinalIgnoreCase))
            .OrderBy(metric => RankMetric(metric.Name))
            .ThenBy(metric => metric.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private static MetricSummary BuildSummary(string name, IReadOnlyList<double?> values)
    {
        var clean = values.Where(value => value.HasValue).Select(value => value!.Value).Order().ToArray();
        if (clean.Length == 0)
        {
            return new MetricSummary(name, GroupFor(name), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        var average = clean.Average();
        var variance = clean.Sum(value => Math.Pow(value - average, 2)) / clean.Length;
        return new MetricSummary(
            name,
            GroupFor(name),
            average,
            clean[0],
            clean[^1],
            Percentile(clean, 0.95),
            Percentile(clean, 0.99),
            Percentile(clean, 0.01),
            Percentile(clean, 0.001),
            Percentile(clean, 0.50),
            Math.Sqrt(variance),
            values.LastOrDefault(value => value.HasValue) ?? clean[^1],
            clean.Length);
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        var index = Math.Clamp((int)Math.Ceiling(sorted.Count * percentile) - 1, 0, sorted.Count - 1);
        return sorted[index];
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes[3..]);
        }

        var utf8 = new UTF8Encoding(false, true);
        try
        {
            return utf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if ((ch == '\r' || ch == '\n') && !inQuotes)
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(cell.ToString());
                cell.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else
            {
                cell.Append(ch);
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private static List<string> Dedupe(IReadOnlyList<string> columns)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        for (var i = 0; i < columns.Count; i++)
        {
            var name = RepairMojibake(columns[i]).Trim().TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"column_{i}";
            }

            if (!seen.TryAdd(name, 1))
            {
                seen[name]++;
                name = $"{name}#{seen[name]}";
            }

            result.Add(name);
        }

        return result;
    }

    private static double? ParseNumber(string value)
    {
        var text = value.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (text.Count(ch => ch == ',') == 1 && !text.Contains('.'))
        {
            text = text.Replace(',', '.');
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string InferTitle(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "Relatório geral";
        }

        var path = new FileInfo(source);
        var parent = path.Directory?.Name ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(source);
        return string.IsNullOrWhiteSpace(parent) ? RepairMojibake(stem) : $"{RepairMojibake(parent)} / {RepairMojibake(stem)}";
    }

    private static string RepairMojibake(string value)
    {
        if (!value.Contains('Ã') && !value.Contains('Â') && !value.Contains('�'))
        {
            return value;
        }

        try
        {
            var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(value);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return value;
        }
    }

    public static string Fold(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    public static bool IsTemperatureMetric(string name)
    {
        var low = Fold(name);
        return low.Contains("temperatura") || low.Contains("temperature") || low.Contains("hotspot") || low.Contains("[°c]") || low.Contains("[c]");
    }

    public static bool IsFpsMetric(string name)
    {
        var low = Fold(name);
        return low.Contains("fps") || low.Contains("quadros") || low.Contains("frame rate") || low.Contains("framerate");
    }

    public static bool IsPowerMetric(string name)
    {
        var low = Fold(name);
        return low.Contains("[w]") || low.Contains("power") || low.Contains("potencia") || low.Contains("consumo de energia");
    }

    public static string GroupFor(string name)
    {
        var low = Fold(name);
        if (IsFpsMetric(name)) return "FPS";
        if (IsTemperatureMetric(name)) return "Temperatura";
        if (IsPowerMetric(name)) return "Potência";
        if (low.Contains("%") || low.Contains("load") || low.Contains("carga") || low.Contains("uso")) return "Carga";
        if (low.Contains("clock") || low.Contains("mhz") || low.Contains("frequencia")) return "Frequencia";
        if (low.Contains("memoria") || low.Contains("memory") || low.Contains("ram") || low.Contains("vram")) return "Memoria";
        return "Outros";
    }

    private static int RankGroup(string group) => group switch
    {
        "Potência" => 0,
        "Temperatura" => 1,
        "FPS" => 2,
        "Carga" => 3,
        "Frequencia" => 4,
        "Memoria" => 5,
        _ => 9
    };

    private static int RankMetric(string name)
    {
        var low = Fold(name);
        if (low.Contains("potencia total do sistema") || low.Contains("system total power")) return 0;
        if (low.Contains("consumo de energia total da cpu") || low.Contains("cpu package power")) return 1;
        if (low.Contains("gpu consumo de energia") || low.Contains("gpu total power")) return 2;
        if (low.Contains("cpu package") && low.Contains("temper")) return 3;
        if (low.Contains("temperatura gpu") || low.Contains("gpu temperature")) return 4;
        if (low.Contains("ponto quente") || low.Contains("hotspot")) return 5;
        if (low.Contains("fps") || low.Contains("quadros")) return 6;
        return 20;
    }
}
