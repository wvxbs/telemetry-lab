# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

from dataclasses import dataclass
import re

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
    if "limit" in low or "limite" in low or "pl1" in low or "pl2" in low:
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
    if any(term in low for term in ("gpu", "video", "graphics", "igpu")):
        return False
    return any(term in low for term in ("cpu", "processador", "core", "package", "p-core", "e-core", "uncore"))


def is_gpu_metric(name: str) -> bool:
    low = ascii_fold(name)
    return "gpu" in low or "video" in low or "graphics" in low


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


def is_voltage_metric(name: str) -> bool:
    low = ascii_fold(name)
    return "[v]" in low or "voltage" in low or "tensao" in low or "vdd" in low


def is_unavailable_memory_metric(name: str) -> bool:
    low = ascii_fold(name)
    return "disponivel" in low or "available" in low or "free" in low


def is_vram_metric(name: str) -> bool:
    low = ascii_fold(name)
    if is_voltage_metric(name) or is_unavailable_memory_metric(name):
        return False
    if any(term in low for term in ("clock", "relogio", "frequencia", "mhz", "ghz", "controlador", "controller")):
        return False
    return (
        "vram" in low
        or "memoria gpu" in low
        or "gpu memory" in low
        or "memoria dedicada gpu" in low
        or "dedicated gpu memory" in low
        or ("memoria dedicada" in low and is_gpu_metric(name))
    )


def is_ram_metric(name: str) -> bool:
    low = ascii_fold(name)
    if is_unavailable_memory_metric(name):
        return False
    has_ram_term = low == "ram" or low.startswith("ram ") or " ram " in low or "[ram]" in low
    return not is_vram_metric(name) and (
        has_ram_term
        or any(term in low for term in ("memoria fisica", "physical memory", "memory load", "carga da memoria"))
    )


def is_memory_temperature_metric(name: str) -> bool:
    low = ascii_fold(name)
    return is_temperature_metric(name) and any(
        term in low for term in ("memoria", "memory", "vram", "junction", "spd hub")
    )


def is_gpu_load_metric(name: str) -> bool:
    low = ascii_fold(name)
    return is_gpu_metric(name) and any(term in low for term in ("%", "load", "carga", "uso", "utilization"))


def is_cpu_load_metric(name: str) -> bool:
    low = ascii_fold(name)
    return is_cpu_metric(name) and any(term in low for term in ("%", "load", "carga", "uso", "utilization"))


def metric_component(name: str) -> str:
    if is_gpu_metric(name):
        return "GPU"
    if is_cpu_metric(name):
        return "CPU"
    if is_system_metric(name):
        return "Sistema"
    if is_battery_metric(name):
        return "Bateria"
    low = ascii_fold(name)
    if any(term in low for term in ("disk", "ssd", "drive", "nvme")):
        return "Disco"
    if any(term in low for term in ("memory", "memoria", "ram", "vram")):
        return "Memoria"
    return "Outro"


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


def is_memory_clock_metric(name: str) -> bool:
    low = ascii_fold(name)
    return "gpu" not in low and ("relogio da memoria" in low or "memory clock" in low) and "mhz" in low


def display_metric_value(name: str, value: float, temperature_unit: str = "C") -> float:
    if temperature_unit == "F" and is_temperature_metric(name):
        return value * 9 / 5 + 32
    if is_memory_clock_metric(name):
        return value * 2
    return value


def memory_technology(columns: list[str]) -> str:
    text = " ".join(ascii_fold(col) for col in columns)
    if "ddr5" in text or "spd hub" in text or "pmic" in text or "vddq" in text:
        return "DDR5"
    if "ddr4" in text:
        return "DDR4"
    if "ddr3" in text:
        return "DDR3"
    if "gear mode" in text:
        return "DDR4/DDR5"
    return "DDR"


def storage_device_label(name: str) -> str:
    low = ascii_fold(name)
    match = re.search(r"(?:disco|disk|drive|ssd|nvme)\s*(\d+)", low)
    if match and int(match.group(1)) > 0:
        return f"Disco/SSD {int(match.group(1))}"
    suffix = re.search(r"#(\d+)$", low)
    if suffix and int(suffix.group(1)) > 1:
        return f"Disco/SSD {int(suffix.group(1))}"
    return "Disco/SSD 1"


def is_cpu_core_clock(name: str) -> bool:
    low = ascii_fold(name)
    if "mhz" not in low or not ("relogio" in low or "clock" in low):
        return False
    if any(term in low for term in ("efetivo", "effective", "avg", "barramento", "bus", "ring", "llc", "gpu", "memoria", "memory")):
        return False
    return any(term in low for term in ("p-core", "e-core", "lp-core", "core ")) or re.search(r"\bcore\s*\d+", low) is not None


def cpu_cluster_name(name: str) -> str:
    low = ascii_fold(name)
    if "lp-core" in low or "lp e-core" in low:
        return "LP-core"
    if "p-core" in low:
        return "P-core"
    if "e-core" in low:
        return "E-core"
    if "zen 5c" in low:
        return "Zen 5c"
    if "zen 5" in low:
        return "Zen 5"
    return "Core"


def cpu_cluster_frame(numeric: pd.DataFrame) -> pd.DataFrame:
    rows = []
    for cluster, cols in sorted(
        ((cluster, [col for col in numeric.columns if is_cpu_core_clock(col) and cpu_cluster_name(col) == cluster]) for cluster in {cpu_cluster_name(col) for col in numeric.columns if is_cpu_core_clock(col)}),
        key=lambda item: {"P-core": 0, "Core": 1, "E-core": 2, "LP-core": 3, "Zen 5": 4, "Zen 5c": 5}.get(item[0], 10),
    ):
        data = numeric[cols].dropna(how="all")
        if data.empty:
            continue
        rows.append({
            "Cluster": cluster,
            "Avg MHz": data.mean(axis=1).mean(),
            "Max MHz": data.max(axis=1).max(),
            "Last MHz": data.tail(1).mean(axis=1).iloc[0],
            "Cores": len(cols),
        })
    return pd.DataFrame(rows)


def metric_unit(name: str, temperature_unit: str = "C") -> str:
    low = ascii_fold(name)
    if is_temperature_metric(name):
        return "°F" if temperature_unit == "F" or low.endswith(" f") or "[f]" in low else "°C"
    if is_fps_metric(name):
        return "FPS"
    if is_power_metric(name):
        return "W"
    if "%" in name or any(term in low for term in ("load", "carga", "uso", "utilization")):
        return "%"
    if is_memory_clock_metric(name):
        return "MT/s"
    if "mhz" in low:
        return "MHz"
    if "ghz" in low:
        return "GHz"
    if "rpm" in low:
        return "RPM"
    if "[gb]" in low or low.endswith(" gb"):
        return "GB"
    if "[mb]" in low or low.endswith(" mb"):
        return "MB"
    bracket_start = name.rfind("[")
    bracket_end = name.rfind("]")
    if bracket_start >= 0 and bracket_end > bracket_start:
        unit = name[bracket_start + 1 : bracket_end].strip()
        return unit or "-"
    return "-"


def redundancy_frame(columns: list[str]) -> pd.DataFrame:
    groups: dict[tuple[str, str], list[str]] = {}
    for col in columns:
        group = metric_group(col)
        if group not in {"Potencia", "Temperatura", "FPS", "Bateria"}:
            continue
        key = (metric_component(col), group)
        groups.setdefault(key, []).append(col)
    rows = []
    for (component, group), members in groups.items():
        if len(members) < 2:
            continue
        rows.append(
            {
                "Component": component,
                "Category": group,
                "Possible duplicates": ", ".join(members),
                "Note": "Revise antes de comparar: HWiNFO pode expor sensor fisico, sensor agregado e metrica canonica do Telemetry Lab para a mesma familia.",
            }
        )
    return pd.DataFrame(rows)


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


def rank_gpu_power_metric(name: str) -> int:
    low = ascii_fold(name)
    if low.startswith("gpu consumo de energia") or low.startswith("gpu power") or "gpu total power" in low:
        return 0
    if "total board power" in low or "tbp" in low or "tgp" in low:
        return 1
    if "8-pin" in low or "entrada de energia gpu" in low:
        return 24
    if "linhas gpu" in low:
        return 25
    if "fonte pp" in low:
        return 20
    if "nvvdd" in low or "restante do chip" in low or "system agent" in low:
        return 30
    return 10


def rank_cpu_metric(name: str) -> int:
    low = ascii_fold(name)
    if "consumo de energia total da cpu" in low or "cpu package power" in low:
        return 0
    if "cpu inteira" in low or "cpu package temperature" in low:
        return 1
    if "nucleo maximo" in low or "core max" in low:
        return 2
    if "uso total da cpu" in low or "utilizacao total da cpu" in low or "cpu total load" in low:
        return 3
    if "relogios nucleo" in low or "core clocks" in low or "p-core clock avg" in low:
        return 4
    if "p-core" in low and ("relogio" in low or "clock" in low):
        return 5
    if "e-core" in low and ("relogio" in low or "clock" in low):
        return 6
    if "vid" in low or "[v]" in low or "voltage" in low or "tensao" in low:
        return 7
    if is_power_metric(name):
        return 8
    if is_temperature_metric(name):
        return 9
    return 40


def rank_gpu_temperature_metric(name: str) -> int:
    low = ascii_fold(name)
    if ("temperatura gpu" in low or "gpu temperature" in low) and not any(term in low for term in ("ponto quente", "hotspot", "hot spot")):
        return 0
    if any(term in low for term in ("ponto quente", "hotspot", "hot spot")):
        return 1
    if any(term in low for term in ("memory", "memoria", "junction")):
        return 2
    return 10


def rank_vram_metric(name: str) -> int:
    low = ascii_fold(name)
    if is_unavailable_memory_metric(name):
        return 100
    if "memoria dedicada gpu d3d" in low or "dedicated gpu d3d" in low:
        return 0
    if "memoria gpu alocada" in low or "gpu memory allocated" in low or "allocated gpu memory" in low:
        return 1
    if "memoria dedicada gpu" in low or "dedicated gpu memory" in low:
        return 2
    if ("vram" in low or "gpu memory" in low) and ("[mb]" in low or "[gb]" in low):
        return 3
    if "uso de memoria gpu" in low or "gpu memory usage" in low or "vram usage" in low:
        return 20
    if "memoria dinamica gpu" in low or "dynamic gpu memory" in low:
        return 40
    return 50


def rank_gaming_metric(name: str) -> int:
    low = ascii_fold(name)
    if is_fps_metric(name):
        return 0
    if is_gpu_metric(name) and is_power_metric(name):
        return 1 + rank_gpu_power_metric(name)
    if is_cpu_metric(name) and is_power_metric(name):
        return 10 + rank_cpu_metric(name)
    if is_gpu_metric(name) and is_temperature_metric(name):
        return 20 + rank_gpu_temperature_metric(name)
    if is_cpu_metric(name) and is_temperature_metric(name):
        return 24 + rank_cpu_metric(name)
    if is_vram_metric(name):
        return 30 + rank_vram_metric(name)
    if is_ram_metric(name):
        return 31
    if is_gpu_load_metric(name):
        return 40
    if is_cpu_load_metric(name):
        return 41
    if is_memory_temperature_metric(name):
        return 50
    return 90


def power_metrics(columns: list[str]) -> list[str]:
    return ranked_metrics(
        columns,
        is_power_metric,
        (
            "system total power w",
            "potencia total do sistema",
            "cpu package power w",
            "consumo de energia total da cpu",
            "gpu total power w",
            "gpu consumo de energia",
            "battery discharge",
            "charge rate",
        ),
    )


def temperature_metrics(columns: list[str]) -> list[str]:
    return ranked_metrics(
        columns,
        is_temperature_metric,
        (
            "cpu package temperature c",
            "cpu package",
            "gpu temperature c",
            "temperatura gpu",
            "gpu hotspot temperature c",
            "ponto quente da gpu",
            "ssd",
            "disk",
        ),
    )


def fps_metrics(columns: list[str]) -> list[str]:
    return ranked_metrics(columns, is_fps_metric, ("fps", "framerate", "frame rate"))


def battery_metrics(columns: list[str]) -> list[str]:
    return ranked_metrics(columns, is_battery_metric, ("discharge", "charge rate", "battery"))


def _existing(columns: list[str], preferred: tuple[str, ...]) -> list[str]:
    available = {ascii_fold(col): col for col in columns}
    result = []
    for wanted in preferred:
        found = available.get(ascii_fold(wanted))
        if found and found not in result:
            result.append(found)
    return result


def drop_redundant_canonical(columns: list[str]) -> list[str]:
    folded = [ascii_fold(col) for col in columns]

    def has_raw(*needles: str) -> bool:
        return any(all(needle in col for needle in needles) for col in folded)

    redundant = set()
    if has_raw("gpu consumo de energia"):
        redundant.add("gpu total power w")
    if has_raw("consumo de energia total da cpu"):
        redundant.add("cpu package power w")
    if has_raw("temperatura gpu"):
        redundant.add("gpu temperature c")
    if has_raw("temperatura de ponto quente da gpu"):
        redundant.add("gpu hotspot temperature c")
    if has_raw("cpu inteira"):
        redundant.add("cpu package temperature c")
    if has_raw("carga da memoria fisica"):
        redundant.add("physical memory load %")
    if has_raw("carga do nucleo da gpu"):
        redundant.add("gpu core load %")

    result = []
    for col in columns:
        if ascii_fold(col) in redundant:
            continue
        if col not in result:
            result.append(col)
    return result


def curated_power_metrics(columns: list[str], include_extra: bool = False) -> list[str]:
    curated = _existing(
        columns,
        (
            "System total power W",
            "CPU package power W",
            "GPU total power W",
            "System estimated W",
        ),
    )
    if include_extra:
        curated.extend([col for col in power_metrics(columns) if col not in curated][:8])
    return drop_redundant_canonical(curated)


def curated_gaming_metrics(columns: list[str], include_extra: bool = False) -> list[str]:
    primary = [
        col
        for col in columns
        if (
            is_fps_metric(col)
            or is_gpu_load_metric(col)
            or is_cpu_load_metric(col)
            or is_vram_metric(col)
            or is_ram_metric(col)
            or is_memory_temperature_metric(col)
            or (is_gpu_metric(col) and is_temperature_metric(col))
            or (is_cpu_metric(col) and is_temperature_metric(col))
            or (is_gpu_metric(col) and is_power_metric(col))
            or (is_cpu_metric(col) and is_power_metric(col))
        )
        and not is_voltage_metric(col)
    ]
    ranked = sorted(primary, key=lambda col: (rank_gaming_metric(col), ascii_fold(col)))
    ranked = drop_redundant_canonical(ranked)
    if include_extra:
        return ranked
    return ranked[:24]


def curated_temperature_metrics(columns: list[str], include_extra: bool = False) -> list[str]:
    curated = _existing(
        columns,
        (
            "CPU package temperature C",
            "CPU package temperature F",
            "GPU temperature C",
            "GPU temperature F",
            "GPU hotspot temperature C",
            "GPU hotspot temperature F",
        ),
    )
    if include_extra:
        curated.extend([col for col in temperature_metrics(columns) if col not in curated][:10])
    return drop_redundant_canonical(curated)


def is_cpu_detail_metric(name: str) -> bool:
    low = ascii_fold(name)
    cpu_voltage = ("vid" in low or "[v]" in low or "voltage" in low or "tensao" in low) and not is_gpu_metric(name)
    return is_cpu_metric(name) or "ia cores" in low or "gt cores" in low or "tjmax" in low or cpu_voltage


def is_gpu_detail_metric(name: str) -> bool:
    low = ascii_fold(name)
    return is_gpu_metric(name) or "nvvdd" in low or "fbvdd" in low or "8-pin" in low


def is_storage_metric(name: str) -> bool:
    low = ascii_fold(name)
    return any(term in low for term in ("disk", "disco", "drive", "ssd", "nvme", "s.m.a.r.t", "smart"))


def is_memory_detail_metric(name: str) -> bool:
    low = ascii_fold(name)
    if is_gpu_metric(name) or is_storage_metric(name):
        return False
    has_ram_term = low == "ram" or low.startswith("ram ") or " ram " in low or "[ram]" in low
    return any(term in low for term in ("memoria", "memory", "spd hub", "relogio da memoria")) or has_ram_term


def rank_gpu_metric(name: str) -> int:
    low = ascii_fold(name)
    if is_power_metric(name):
        return rank_gpu_power_metric(name)
    if is_temperature_metric(name):
        return 10 + rank_gpu_temperature_metric(name)
    if is_vram_metric(name):
        return 20 + rank_vram_metric(name)
    if is_gpu_load_metric(name):
        return 30
    if any(term in low for term in ("clock", "relogio", "mhz")):
        return 40
    if is_voltage_metric(name):
        return 50
    if "busy" in low or "ms" in low:
        return 60
    return 80


def rank_memory_metric(name: str) -> int:
    low = ascii_fold(name)
    if "carga da memoria fisica" in low or "physical memory load" in low:
        return 0
    if "memoria fisica utilizada" in low or "physical memory used" in low:
        return 1
    if "spd hub" in low and is_temperature_metric(name):
        return 2
    if "relogio da memoria" in low or "memory clock" in low:
        return 0
    if "memoria virtual" in low:
        return 10
    return 40


def rank_storage_metric(name: str) -> int:
    low = ascii_fold(name)
    if is_temperature_metric(name):
        return 0
    if "vida restante" in low or "remaining life" in low:
        return 1
    if "reserva" in low or "spare" in low:
        return 2
    if "atividade" in low or "activity" in low:
        return 3
    if "falha" in low or "failure" in low:
        return 4
    if "aviso" in low or "warning" in low:
        return 5
    return 40


def component_metrics(columns: list[str], component: str, include_extra: bool = True) -> list[str]:
    component = component.casefold()
    if component == "cpu":
        predicate, ranker = is_cpu_detail_metric, rank_cpu_metric
    elif component == "gpu":
        predicate, ranker = is_gpu_detail_metric, rank_gpu_metric
    elif component in {"memory", "memoria", "ram"}:
        predicate, ranker = is_memory_detail_metric, rank_memory_metric
    elif component in {"storage", "armazenamento", "disk"}:
        predicate, ranker = is_storage_metric, rank_storage_metric
    else:
        return []
    ranked = sorted([col for col in columns if predicate(col)], key=lambda col: (ranker(col), ascii_fold(col)))
    ranked = drop_redundant_canonical(ranked)
    return ranked if include_extra else ranked[:24]


def estimated_system_power(numeric: pd.DataFrame) -> pd.Series:
    cols = list(numeric.columns)
    cpu = curated_power_metrics(cols, include_extra=True)
    cpu = [col for col in cpu if is_cpu_metric(col) and is_power_metric(col)]
    gpu = curated_power_metrics(cols, include_extra=True)
    gpu = [col for col in gpu if is_gpu_metric(col) and is_power_metric(col)]
    parts = []
    if cpu:
        parts.append(numeric[cpu[0]])
    if gpu:
        parts.append(numeric[gpu[0]])
    if not parts:
        explicit = [col for col in cols if is_system_metric(col) and is_power_metric(col)]
        if explicit:
            return numeric[explicit[0]]
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
