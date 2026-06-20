# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

import pandas as pd

from telemetry_lab.csv_io import parse_hwinfo_csv_bytes
from telemetry_lab.metrics import (
    component_metrics,
    curated_gaming_metrics,
    curated_power_metrics,
    curated_temperature_metrics,
    estimated_system_power,
    fps_metrics,
    is_cpu_metric,
    is_fps_metric,
    is_gpu_metric,
    is_power_metric,
    is_temperature_metric,
    is_vram_metric,
    metric_group,
    metric_unit,
    rank_cpu_metric,
    rank_gpu_power_metric,
    rank_gpu_temperature_metric,
    rank_vram_metric,
)
from telemetry_lab.report_service import build_report, report_for_temperature_unit


@dataclass(frozen=True)
class CliMetric:
    label: str
    metric: str
    group: str
    unit: str
    primary_label: str
    primary_value: float
    average: float
    minimum: float
    maximum: float
    last: float
    samples: int


@dataclass(frozen=True)
class CliReport:
    title: str
    source: str
    samples: int
    sensors: int
    live: bool
    metrics: list[CliMetric]


def load_cli_report(path: str | Path, temperature_unit: str = "C", live: bool = False, group: str = "summary") -> CliReport:
    source = Path(path).expanduser()
    df = parse_hwinfo_csv_bytes(source.read_bytes())
    report = build_report(str(source), df, lambda key: key, size=source.stat().st_size, live_reload=live)

    estimated = estimated_system_power(report.numeric)
    if estimated.notna().sum() > 0 and "System estimated W" not in report.numeric.columns:
        report.numeric["System estimated W"] = estimated

    display_report = report_for_temperature_unit(report, temperature_unit)
    metrics = build_cli_metrics(display_report.numeric, group=group, live=live, temperature_unit=temperature_unit)
    return CliReport(
        title=str(report.context.get("title") or source.stem),
        source=str(source),
        samples=len(report.df),
        sensors=len(report.df.columns),
        live=live,
        metrics=metrics,
    )


def build_cli_metrics(numeric: pd.DataFrame, group: str, live: bool, temperature_unit: str) -> list[CliMetric]:
    columns = list(numeric.columns)
    selected = _selected_columns(columns, group)
    return [_metric_row(numeric, metric, live=live, temperature_unit=temperature_unit) for metric in selected if metric in numeric]


def _selected_columns(columns: list[str], group: str) -> list[str]:
    if group == "power":
        return curated_power_metrics(columns, include_extra=True)
    if group == "temperature":
        return curated_temperature_metrics(columns, include_extra=True)
    if group == "fps":
        return fps_metrics(columns)
    if group in {"cpu", "gpu", "memory", "storage"}:
        return component_metrics(columns, group, include_extra=False)
    if group == "gaming":
        return curated_gaming_metrics(columns, include_extra=False)
    if group == "all":
        return sorted(columns, key=lambda col: (metric_group(col), col.casefold()))

    picked: list[str] = []
    picked.extend(_compact_power_metrics(columns))
    picked.extend(_best(columns, lambda col: is_cpu_metric(col) and is_temperature_metric(col), rank_cpu_metric))
    picked.extend(_best(columns, lambda col: is_gpu_metric(col) and is_temperature_metric(col), rank_gpu_temperature_metric))
    picked.extend(_best(columns, is_fps_metric))
    picked.extend(_best(columns, is_vram_metric, rank_vram_metric))
    return _dedupe(picked)


def _compact_power_metrics(columns: list[str]) -> list[str]:
    metrics = curated_power_metrics(columns, include_extra=False)
    has_direct_system = any(metric.casefold() == "system total power w" for metric in metrics)
    if has_direct_system:
        metrics = [metric for metric in metrics if metric.casefold() != "system estimated w"]
    return metrics


def _best(columns: list[str], predicate, ranker=None) -> list[str]:
    ranker = ranker or (lambda _name: 0)
    matches = [col for col in columns if predicate(col)]
    if not matches:
        return []
    return [sorted(matches, key=lambda col: (ranker(col), col.casefold()))[0]]


def _dedupe(values: Iterable[str]) -> list[str]:
    seen = set()
    result = []
    for value in values:
        if value not in seen:
            result.append(value)
            seen.add(value)
    return result


def _metric_row(numeric: pd.DataFrame, metric: str, live: bool, temperature_unit: str) -> CliMetric:
    clean = numeric[metric].dropna()
    average = float(clean.mean())
    minimum = float(clean.min())
    maximum = float(clean.max())
    last = float(clean.iloc[-1])
    primary_label = _primary_label(metric, live)
    primary_value = last if live else _static_primary_value(metric, average, maximum)
    return CliMetric(
        label=_short_label(metric),
        metric=metric,
        group=metric_group(metric),
        unit=metric_unit(metric, temperature_unit),
        primary_label=primary_label,
        primary_value=primary_value,
        average=average,
        minimum=minimum,
        maximum=maximum,
        last=last,
        samples=int(clean.count()),
    )


def _static_primary_value(metric: str, average: float, maximum: float) -> float:
    if is_temperature_metric(metric):
        return maximum
    return average


def _primary_label(metric: str, live: bool) -> str:
    if live:
        return "atual"
    if is_temperature_metric(metric):
        return "pico"
    return "media"


def _short_label(metric: str) -> str:
    if is_fps_metric(metric):
        return "FPS"
    if is_vram_metric(metric):
        return "VRAM"
    if is_gpu_metric(metric) and is_power_metric(metric):
        return "GPU power"
    if is_cpu_metric(metric) and is_power_metric(metric):
        return "CPU power"
    if is_gpu_metric(metric) and is_temperature_metric(metric):
        return "GPU temp"
    if is_cpu_metric(metric) and is_temperature_metric(metric):
        return "CPU temp"
    return metric


def format_text(report: CliReport, limit: int | None = None) -> str:
    rows = report.metrics[:limit] if limit else report.metrics
    lines = [
        f"Telemetry Lab CLI - {report.title}",
        f"Fonte: {report.source}",
        f"Amostras: {report.samples} | Sensores: {report.sensors} | modo: {'live' if report.live else 'estatico'}",
        "",
        f"{'Sinal':<18} {'Valor':>14} {'Media':>14} {'Min':>14} {'Max':>14}  Sensor",
        "-" * 96,
    ]
    for metric in rows:
        lines.append(
            f"{metric.label:<18} "
            f"{_format_number(metric.primary_value, metric.unit, metric.primary_label):>14} "
            f"{_format_number(metric.average, metric.unit):>14} "
            f"{_format_number(metric.minimum, metric.unit):>14} "
            f"{_format_number(metric.maximum, metric.unit):>14}  "
            f"{metric.metric}"
        )
    return "\n".join(lines)


def to_jsonable(report: CliReport, limit: int | None = None) -> dict:
    rows = report.metrics[:limit] if limit else report.metrics
    return {
        "title": report.title,
        "source": report.source,
        "samples": report.samples,
        "sensors": report.sensors,
        "mode": "live" if report.live else "static",
        "metrics": [
            {
                "label": metric.label,
                "metric": metric.metric,
                "group": metric.group,
                "unit": metric.unit,
                "primary_label": metric.primary_label,
                "primary_value": metric.primary_value,
                "average": metric.average,
                "minimum": metric.minimum,
                "maximum": metric.maximum,
                "last_sample": metric.last,
                "samples": metric.samples,
            }
            for metric in rows
        ],
    }


def _format_number(value: float, unit: str, label: str | None = None) -> str:
    number = f"{value:.0f}" if abs(value) >= 100 else f"{value:.1f}"
    suffix = "" if unit == "-" else f" {unit}"
    prefix = f"{label}: " if label else ""
    return f"{prefix}{number}{suffix}"
