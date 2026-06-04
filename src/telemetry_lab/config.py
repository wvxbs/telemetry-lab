# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

from pathlib import Path

CONTAINER_REPORT_DIR = Path("/data/reports")

DEFAULT_DIR = Path(
    "/mnt/c/Users/gabri/OneDrive/Documents/tools/Desmerdificar o windows/"
    "relatorio de sensores/cinebench 2026"
)

ALT_DEFAULT_DIR = Path(
    "/mnt/c/Users/gabri/OneDrive/Documents/tools/Desmerd\u00edficar o windows/"
    "relat\u00f3rio de sensores/cinebench 2026"
)

KNOWN_CONTEXT_TERMS = {
    "benchmarks",
    "benchmark",
    "games",
    "game",
    "valorant",
    "cinebench",
    "geekbench",
    "produtividade",
    "productivity",
    "geral",
    "general",
    "cpu",
    "gpu",
}

INDEX = {
    "cpu_total_pct": 70,
    "cpu_package_temp_c": 126,
    "cpu_package_w": 176,
    "ia_cores_w": 177,
    "gt_cores_w": 178,
    "system_total_w": 179,
    "p_core_clock": list(range(23, 29)),
    "e_core_clock": list(range(29, 33)),
    "p_core_effective": list(range(36, 48)),
    "e_core_effective": list(range(48, 52)),
    "gpu_temp_c": 350,
    "gpu_hotspot_c": 351,
    "gpu_power_w": 357,
    "gpu_clock_mhz": 366,
    "gpu_effective_clock_mhz": 369,
    "gpu_core_load_pct": 371,
    "gpu_mem_use_pct": 375,
    "gpu_perf_limiter": 381,
    "gpu_perf_power": 382,
    "gpu_perf_thermal": 383,
    "gpu_perf_reliability_voltage": 384,
    "gpu_perf_utilization": 386,
    "gpu_mem_allocated_mb": 389,
    "disk1_temp_c": 308,
    "disk2_temp_c": 309,
    "disk3_temp_c": 310,
    "disk1_failure": 313,
    "disk1_warning": 314,
    "disk_total_activity_pct": 331,
    "physical_mem_used_mb": 5,
    "physical_mem_available_mb": 6,
    "physical_mem_load_pct": 7,
    "pagefile_use_pct": 8,
}


def default_report_path() -> str:
    if CONTAINER_REPORT_DIR.exists():
        csvs = sorted(CONTAINER_REPORT_DIR.rglob("*.csv")) + sorted(CONTAINER_REPORT_DIR.rglob("*.CSV"))
        if csvs:
            return str(csvs[0])
        return str(CONTAINER_REPORT_DIR)
    if ALT_DEFAULT_DIR.exists():
        csvs = sorted(ALT_DEFAULT_DIR.glob("*.CSV")) + sorted(ALT_DEFAULT_DIR.glob("*.csv"))
        return str(csvs[0] if csvs else ALT_DEFAULT_DIR)
    return str(DEFAULT_DIR)
