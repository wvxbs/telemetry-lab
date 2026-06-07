# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

from pathlib import Path
from typing import Callable

import pandas as pd

from telemetry_lab.analysis import make_report
from telemetry_lab.csv_io import load_csv_path, load_uploaded_csv
from telemetry_lab.models import Report
from telemetry_lab.units import display_numeric_frame

Translate = Callable[[str], str]
CacheReader = Callable[[str, int, int, int], pd.DataFrame]


def build_report(
    source: str,
    df: pd.DataFrame,
    translate: Translate,
    mtime_ns: int | None = None,
    size: int | None = None,
) -> Report:
    return make_report(source, df, mtime_ns=mtime_ns, size=size, translate=translate)


def report_for_temperature_unit(report: Report, temperature_unit: str) -> Report:
    return Report(
        source=report.source,
        df=report.df,
        time=report.time,
        numeric=display_numeric_frame(report.numeric, temperature_unit),
        context=report.context,
        mtime_ns=report.mtime_ns,
        size=report.size,
    )


def csv_files_in_path(path: str | Path) -> list[Path]:
    p = Path(path).expanduser()
    if p.is_dir():
        return sorted(p.rglob("*.csv")) + sorted(p.rglob("*.CSV"))
    return [p] if p.exists() else []


def build_uploaded_report(name: str, data: bytes, translate: Translate) -> Report:
    return build_report(name, load_uploaded_csv(name, data), translate, size=len(data))


def build_path_report(
    path: str | Path,
    live_reload: bool,
    cache_reader: CacheReader,
    reload_token: int,
    translate: Translate,
) -> Report:
    source = str(Path(path).expanduser())
    df, mtime_ns, size = load_csv_path(source, live_reload, cache_reader, reload_token)
    return build_report(source, df, translate, mtime_ns, size)


def build_path_reports(
    raw_paths: list[str],
    live_reload: bool,
    cache_reader: CacheReader,
    reload_token: int,
    translate: Translate,
) -> list[Report]:
    reports: list[Report] = []
    for raw_path in raw_paths:
        for file in csv_files_in_path(raw_path):
            reports.append(build_path_report(file, live_reload, cache_reader, reload_token, translate))
    return reports
