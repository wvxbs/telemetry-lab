# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

from datetime import datetime
from typing import Any

import pandas as pd


def benchmark_payload(name: str, scenario: str, scores: pd.DataFrame, report: dict[str, Any] | None) -> dict[str, Any]:
    rows = []
    for row in scores.to_dict("records"):
        metric = str(row.get("Metric", "")).strip()
        value = row.get("Value", "")
        unit = str(row.get("Unit", "")).strip()
        if not metric or value in ("", None):
            continue
        rows.append({"metric": metric, "value": value, "unit": unit})
    return {
        "schema": "telemetry-lab.benchmark.v1",
        "created_at": datetime.now().isoformat(timespec="seconds"),
        "benchmark": name.strip() or "Benchmark",
        "scenario": scenario.strip() or "geral",
        "scores": rows,
        "linked_report": report,
    }
