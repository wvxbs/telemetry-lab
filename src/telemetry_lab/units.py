# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

import re

import pandas as pd

TemperatureUnit = str


def normalize_temperature_unit(unit: str | None) -> TemperatureUnit:
    return "F" if unit == "F" else "C"


def is_temperature_metric(name: str) -> bool:
    low = name.lower()
    if "tempo" in low or "frame time" in low or "quadro" in low:
        return False
    if "temperatura" in low or "temperature" in low or "hotspot" in low:
        return True
    return bool(re.search(r"(\s|_|-|\()(\u00b0?c|celsius)(\)|$|\s)", low) or low.endswith(" c"))


def celsius_to_fahrenheit(series: pd.Series) -> pd.Series:
    return series * 9 / 5 + 32


def display_temperature_name(name: str, unit: str) -> str:
    unit = normalize_temperature_unit(unit)
    suffix = "F" if unit == "F" else "C"
    if re.search(r"\u00b0?[CF]$", name):
        return re.sub(r"\u00b0?[CF]$", suffix, name)
    if name.endswith(" C") or name.endswith(" F"):
        return f"{name[:-2]} {suffix}"
    return f"{name} {suffix}" if is_temperature_metric(name) else name


def display_numeric_frame(numeric: pd.DataFrame, temperature_unit: str) -> pd.DataFrame:
    temperature_unit = normalize_temperature_unit(temperature_unit)
    out = numeric.copy()
    rename: dict[str, str] = {}
    for col in numeric.columns:
        if not is_temperature_metric(col):
            continue
        if temperature_unit == "F":
            out[col] = celsius_to_fahrenheit(out[col])
        rename[col] = display_temperature_name(col, temperature_unit)
    return out.rename(columns=rename)
