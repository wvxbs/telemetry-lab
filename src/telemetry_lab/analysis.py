# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

import re
from typing import Callable

import pandas as pd

from telemetry_lab.config import INDEX
from telemetry_lab.context import infer_context
from telemetry_lab.models import Report
from telemetry_lab.text_utils import ascii_fold


def numeric_series(series: pd.Series) -> pd.Series:
    cleaned = series.astype(str).str.replace(",", ".", regex=False)
    cleaned = cleaned.str.replace(r"[^0-9eE+\-.]", "", regex=True)
    return pd.to_numeric(cleaned, errors="coerce")


def build_time(df: pd.DataFrame) -> pd.Series:
    if len(df.columns) >= 2:
        raw = df.iloc[:, 0].astype(str) + " " + df.iloc[:, 1].astype(str)
        parsed = pd.to_datetime(raw, format="%d.%m.%Y %H:%M:%S.%f", errors="coerce")
        if parsed.notna().sum() >= max(1, len(df) // 3):
            return parsed
    for col in df.columns[:5]:
        parsed = pd.to_datetime(df[col], errors="coerce")
        if parsed.notna().sum() >= max(1, len(df) // 3):
            return parsed
    return pd.Series(pd.RangeIndex(len(df)), index=df.index, name="sample")


def find_columns(df: pd.DataFrame, *patterns: str) -> list[str]:
    found: list[str] = []
    for col in df.columns:
        low = ascii_fold(col)
        if all(re.search(pattern, low) for pattern in patterns):
            found.append(col)
    return found


def avg_matching(df: pd.DataFrame, *patterns: str) -> pd.Series:
    cols = find_columns(df, *patterns)
    parts = [numeric_series(df[col]) for col in cols]
    if not parts:
        return pd.Series(index=df.index, dtype="float64")
    return pd.concat(parts, axis=1).mean(axis=1)


def best_matching(
    df: pd.DataFrame,
    priorities: tuple[str, ...],
    include: tuple[str, ...] = (),
    exclude: tuple[str, ...] = (),
) -> pd.Series:
    candidates = []
    for col in df.columns:
        low = ascii_fold(col)
        if include and not all(re.search(pattern, low) for pattern in include):
            continue
        if any(re.search(pattern, low) for pattern in exclude):
            continue
        series = numeric_series(df[col])
        if series.notna().sum() > 0:
            candidates.append((col, low, series))
    if not candidates:
        return pd.Series(index=df.index, dtype="float64")
    for priority in priorities:
        wanted = ascii_fold(priority)
        for _col, low, series in candidates:
            if wanted in low:
                return series
    return candidates[0][2]


def col_by_index(df: pd.DataFrame, index: int) -> pd.Series:
    if index >= len(df.columns):
        return pd.Series(index=df.index, dtype="float64")
    return numeric_series(df.iloc[:, index])


def avg_by_indexes(df: pd.DataFrame, indexes: list[int]) -> pd.Series:
    parts = [col_by_index(df, i) for i in indexes if i < len(df.columns)]
    if not parts:
        return pd.Series(index=df.index, dtype="float64")
    return pd.concat(parts, axis=1).mean(axis=1)


def build_numeric(df: pd.DataFrame) -> pd.DataFrame:
    out = pd.DataFrame(index=df.index)
    fallback = {
        "CPU total load %": col_by_index(df, INDEX["cpu_total_pct"]),
        "CPU package power W": col_by_index(df, INDEX["cpu_package_w"]),
        "System total power W": col_by_index(df, INDEX["system_total_w"]),
        "CPU package temperature C": col_by_index(df, INDEX["cpu_package_temp_c"]),
        "GPU total power W": col_by_index(df, INDEX["gpu_power_w"]),
        "GPU temperature C": col_by_index(df, INDEX["gpu_temp_c"]),
        "GPU hotspot temperature C": col_by_index(df, INDEX["gpu_hotspot_c"]),
        "GPU core load %": col_by_index(df, INDEX["gpu_core_load_pct"]),
        "GPU memory use %": col_by_index(df, INDEX["gpu_mem_use_pct"]),
        "Physical memory load %": col_by_index(df, INDEX["physical_mem_load_pct"]),
        "Disk total activity %": col_by_index(df, INDEX["disk_total_activity_pct"]),
        "P-core clock avg MHz": avg_by_indexes(df, INDEX["p_core_clock"]),
        "E-core clock avg MHz": avg_by_indexes(df, INDEX["e_core_clock"]),
        "P-core effective avg MHz": avg_by_indexes(df, INDEX["p_core_effective"]),
        "E-core effective avg MHz": avg_by_indexes(df, INDEX["e_core_effective"]),
    }
    detected = {
        "CPU total load %": avg_matching(df, "cpu", "(total|load|uso|util|carga)", "(%|util)"),
        "CPU package power W": best_matching(
            df,
            ("consumo de energia total da cpu", "cpu package power", "cpu package w", "package power"),
            include=("cpu|package", "w|power|potencia|energia"),
            exclude=("ia cores|gt cores|system agent|restante|limit|limite|pl1|pl2"),
        ),
        "System total power W": best_matching(
            df,
            ("potencia total do sistema", "system total power", "total system power", "system total w"),
            include=("sistema|system", "total|power|potencia|w"),
            exclude=("limit|limite|pl1|pl2|agent"),
        ),
        "CPU package temperature C": best_matching(
            df,
            ("cpu package", "cpu inteira", "cpu whole", "temperaturas centrais"),
            include=("cpu|core|centrais", r"\[c\]|temp|temperatura"),
            exclude=("gt cores|igpu|vr vcc"),
        ),
        "GPU total power W": best_matching(
            df,
            ("gpu consumo de energia [w]", "gpu power [w]", "gpu total power", "total gpu power"),
            include=("gpu", "w|power|potencia|energia"),
            exclude=("linhas|fonte pp|core \\(|fbvdd|saida|tensao|voltage|limit|limite|pl1|pl2"),
        ),
        "GPU temperature C": best_matching(
            df,
            ("temperatura gpu", "gpu temperature", "gpu temp"),
            include=("gpu", r"\[c\]|temp|temperatura"),
            exclude=("ponto quente|hotspot|limite|limit"),
        ),
        "GPU hotspot temperature C": best_matching(
            df,
            ("temperatura de ponto quente da gpu", "gpu hotspot", "hot spot"),
            include=("gpu", "hotspot|ponto quente|hot spot"),
            exclude=("limite|limit"),
        ),
        "GPU core load %": best_matching(
            df,
            ("carga do nucleo da gpu", "gpu core load", "gpu load"),
            include=("gpu", "load|carga|uso|utilizacao", "%"),
            exclude=("d3d|video|memoria|memory|bus|barramento|vr"),
        ),
        "GPU memory use %": best_matching(
            df,
            ("uso de memoria gpu", "gpu memory use", "gpu memory load"),
            include=("gpu", "memoria|memory", "%"),
            exclude=("d3d|disponivel|available|alocada|allocated|dedicada|dynamic"),
        ),
        "Physical memory load %": avg_matching(df, "(physical|f.sica)", "(memory|mem.ria)", "(load|uso|util)"),
        "Disk total activity %": avg_matching(df, "(disk|drive|ssd)", "(activity|atividade|load)"),
        "P-core clock avg MHz": avg_matching(df, "p-core", "clock"),
        "E-core clock avg MHz": avg_matching(df, "e-core", "clock"),
    }
    columns: dict[str, pd.Series] = {}
    for name, fallback_series in fallback.items():
        detected_series = detected.get(name)
        series = detected_series if detected_series is not None and detected_series.notna().sum() > 0 else fallback_series
        if series.notna().sum() > 0:
            columns[name] = series
    for col in df.columns:
        series = numeric_series(df[col])
        if series.notna().sum() >= max(3, len(df) // 10) and col not in columns:
            columns[col] = series
    if not columns:
        return out
    return pd.DataFrame(columns, index=df.index).dropna(axis=1, how="all")


def stats_frame(numeric: pd.DataFrame) -> pd.DataFrame:
    rows = []
    for col in numeric.columns:
        clean = numeric[col].dropna()
        if clean.empty:
            continue
        rows.append(
            {
                "Metric": col,
                "Min": clean.min(),
                "Avg": clean.mean(),
                "P95": clean.quantile(0.95),
                "Max": clean.max(),
                "Samples": int(clean.count()),
            }
        )
    return pd.DataFrame(rows)


def yes_count(df: pd.DataFrame, index: int) -> int:
    if index >= len(df.columns):
        return 0
    values = df.iloc[:, index].astype(str).str.strip().str.lower()
    return int(values.isin(["sim", "yes", "true", "1"]).sum())


def make_report(
    source: str,
    df: pd.DataFrame,
    mtime_ns: int | None = None,
    size: int | None = None,
    translate: Callable[[str], str] | None = None,
) -> Report:
    return Report(
        source=source,
        df=df,
        time=build_time(df),
        numeric=build_numeric(df),
        context=infer_context(source, translate=translate),
        mtime_ns=mtime_ns,
        size=size,
    )
