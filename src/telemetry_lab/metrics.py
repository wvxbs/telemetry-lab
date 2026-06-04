# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

from dataclasses import dataclass

import pandas as pd

from telemetry_lab.text_utils import ascii_fold, pretty_token
from telemetry_lab.units import is_temperature_metric


@dataclass(frozen=True)
class MetricInfo:
    name: str
    category: str
    description: str
    aliases: tuple[str, ...] = ()


SEARCH_ALIASES = {
    "power": ("potencia", "consumo", "energia", "w", "watts"),
    "potencia": ("power", "consumo", "energia", "w", "watts"),
    "temperature": ("temperatura", "temp", "calor", "graus"),
    "temperatura": ("temperature", "temp", "heat", "degrees"),
    "memory": ("memoria", "ram", "vram"),
    "memoria": ("memory", "ram", "vram"),
    "load": ("carga", "uso", "utilizacao", "utilization"),
    "carga": ("load", "uso", "utilization"),
    "clock": ("frequencia", "freq", "mhz"),
    "frequencia": ("clock", "frequency", "mhz"),
    "battery": ("bateria", "descarga", "charge", "discharge"),
    "bateria": ("battery", "descarga", "charge", "discharge"),
    "fps": ("frames", "quadros", "framerate", "frame rate"),
    "quadros": ("fps", "frames", "framerate"),
}


def search_terms(text: str) -> set[str]:
    folded = ascii_fold(text)
    parts = {part for part in folded.replace("/", " ").replace("_", " ").replace("-", " ").split() if part}
    parts.add(folded)
    expanded = set(parts)
    for part in parts:
        expanded.update(SEARCH_ALIASES.get(part, ()))
    return expanded


def metric_label(name: str) -> str:
    info = describe_metric(name)
    aliases = ", ".join(info.aliases[:4])
    return f"{name} | {info.category}: {aliases}" if aliases else f"{name} | {info.category}"


def is_power_metric(name: str) -> bool:
    low = ascii_fold(name)
    if "limit" in low or "power limit" in low:
        return False
    return " w" in low or low.endswith("w") or "power" in low or "potencia" in low or "consumo" in low


def is_battery_metric(name: str) -> bool:
    low = ascii_fold(name)
    return any(term in low for term in ("battery", "bateria", "charge", "discharge", "wear", "remaining"))


def is_fps_metric(name: str) -> bool:
    low = ascii_fold(name)
    return any(term in low for term in ("fps", "framerate", "frame rate", "frames per second", "quadros"))


def is_cpu_metric(name: str) -> bool:
    low = ascii_fold(name)
    return "cpu" in low or "core" in low or "package" in low


def is_gpu_metric(name: str) -> bool:
    return "gpu" in ascii_fold(name)


def is_system_metric(name: str) -> bool:
    low = ascii_fold(name)
    return "system" in low or "total" in low or "entire" in low


def metric_group(name: str) -> str:
    if is_fps_metric(name):
        return "FPS"
    if is_battery_metric(name):
        return "Bateria"
    if is_temperature_metric(name):
        return "Temperatura"
    if is_power_metric(name):
        return "Potencia"
    if is_gpu_metric(name):
        return "GPU"
    if is_cpu_metric(name):
        return "CPU"
    if any(term in ascii_fold(name) for term in ("memory", "memoria", "ram", "vram")):
        return "Memoria"
    return "Outros"


def describe_metric(name: str) -> MetricInfo:
    group = metric_group(name)
    aliases = tuple(sorted(search_terms(group) | search_terms(name)))[:8]
    if group == "Potencia":
        desc = "Consumo ou potencia reportada pelo sensor. Em HWiNFO, nomes parecidos podem representar sensores fisicos diferentes ou uma metrica canonica criada pelo Telemetry Lab."
    elif group == "Temperatura":
        desc = "Temperatura reportada por componente, hotspot, pacote, nucleo ou sensor de placa."
    elif group == "FPS":
        desc = "Taxa de quadros. Use filtros para remover menus, segundo plano ou limites artificiais antes de avaliar performance em jogo."
    elif group == "Bateria":
        desc = "Estado, carga, descarga, capacidade ou estimativa relacionada a bateria."
    elif group in ("CPU", "GPU"):
        desc = f"Metrica relacionada a {group}, como carga, frequencia, memoria, limite ou utilizacao."
    else:
        desc = "Sensor numerico preservado do HWiNFO para analise livre."
    return MetricInfo(name=name, category=group, description=desc, aliases=aliases)


def glossary_frame(columns: list[str]) -> pd.DataFrame:
    rows = []
    for name in columns:
        info = describe_metric(name)
        rows.append(
            {
                "Metric": name,
                "Category": info.category,
                "Aliases": ", ".join(info.aliases),
                "Description": info.description,
            }
        )
    return pd.DataFrame(rows)


def ranked_metrics(columns: list[str], predicate, preferred: tuple[str, ...] = ()) -> list[str]:
    preferred_folded = [ascii_fold(item) for item in preferred]

    def score(name: str) -> tuple[int, str]:
        low = ascii_fold(name)
        for idx, target in enumerate(preferred_folded):
            if target and target in low:
                return (idx, low)
        return (len(preferred_folded), low)

    return sorted([col for col in columns if predicate(col)], key=score)


def power_metrics(columns: list[str]) -> list[str]:
    return ranked_metrics(
        columns,
        is_power_metric,
        ("system total w", "cpu package w", "gpu w", "battery discharge", "charge rate"),
    )


def temperature_metrics(columns: list[str]) -> list[str]:
    return ranked_metrics(
        columns,
        is_temperature_metric,
        ("cpu package", "gpu temp", "gpu hotspot", "ssd", "disk"),
    )


def fps_metrics(columns: list[str]) -> list[str]:
    return ranked_metrics(columns, is_fps_metric, ("fps", "framerate", "frame rate"))


def battery_metrics(columns: list[str]) -> list[str]:
    return ranked_metrics(columns, is_battery_metric, ("discharge", "charge rate", "battery"))


def estimated_system_power(numeric: pd.DataFrame) -> pd.Series:
    cols = list(numeric.columns)
    explicit = [col for col in cols if is_system_metric(col) and is_power_metric(col)]
    if explicit:
        return numeric[explicit[0]]
    cpu = [col for col in cols if is_cpu_metric(col) and is_power_metric(col)]
    gpu = [col for col in cols if is_gpu_metric(col) and is_power_metric(col)]
    parts = []
    if cpu:
        parts.append(numeric[cpu[0]])
    if gpu:
        parts.append(numeric[gpu[0]])
    if not parts:
        return pd.Series(index=numeric.index, dtype="float64")
    return pd.concat(parts, axis=1).sum(axis=1, min_count=1)


def metric_options_for_query(columns: list[str], query: str) -> list[str]:
    if not query.strip():
        return columns
    terms = search_terms(query)
    result = []
    for col in columns:
        haystack = search_terms(col) | search_terms(describe_metric(col).category)
        if terms & haystack or any(term in " ".join(haystack) for term in terms):
            result.append(col)
    return result
