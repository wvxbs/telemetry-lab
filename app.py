from __future__ import annotations

import csv
import re
from io import StringIO
from pathlib import Path

import pandas as pd
import streamlit as st


CONTAINER_DEFAULT_DIR = Path("/data/cinebench-2026")

DEFAULT_DIR = Path(
    "/mnt/c/Users/gabri/OneDrive/Documents/tools/Desmerdificar o windows/"
    "relatorio de sensores/cinebench 2026"
)

ALT_DEFAULT_DIR = Path(
    "/mnt/c/Users/gabri/OneDrive/Documents/tools/Desmerdíficar o windows/"
    "relatório de sensores/cinebench 2026"
)


SCORES = {
    "G completo - GPU": 18089,
    "G completo - CPU multi": 3727,
    "G completo - CPU single core": 563,
    "G completo - CPU single thread": 420,
    "G completo - MP ratio": 8.86,
    "G multi anterior": 3683,
    "Balanceado multi": 3557,
}


INDEX = {
    "cpu_total_pct": 70,
    "cpu_package_temp_c": 126,
    "cpu_package_w": 176,
    "ia_cores_w": 177,
    "gt_cores_w": 178,
    "system_total_w": 179,
    "system_agent_w": 180,
    "rest_chip_w": 181,
    "p_core_clock": list(range(23, 29)),
    "e_core_clock": list(range(29, 33)),
    "p_core_effective": list(range(36, 48)),
    "e_core_effective": list(range(48, 52)),
    "gpu_temp_c": 350,
    "gpu_hotspot_c": 351,
    "gpu_thermal_limit_c": 352,
    "gpu_power_w": 357,
    "gpu_clock_mhz": 366,
    "gpu_mem_clock_mhz": 367,
    "gpu_effective_clock_mhz": 369,
    "gpu_core_load_pct": 371,
    "gpu_mem_ctrl_load_pct": 372,
    "gpu_bus_load_pct": 374,
    "gpu_mem_use_pct": 375,
    "gpu_perf_limiter": 381,
    "gpu_perf_power": 382,
    "gpu_perf_thermal": 383,
    "gpu_perf_reliability_voltage": 384,
    "gpu_perf_utilization": 386,
    "gpu_mem_available_mb": 388,
    "gpu_mem_allocated_mb": 389,
    "gpu_mem_dedicated_d3d_mb": 390,
    "gpu_mem_dynamic_d3d_mb": 391,
    "pcie_gts": 392,
    "battery_voltage_v": 393,
    "battery_remaining_wh": 394,
    "battery_charge_pct": 395,
    "battery_wear_pct": 396,
    "disk1_temp_c": 308,
    "disk2_temp_c": 309,
    "disk3_temp_c": 310,
    "disk1_life_pct": 311,
    "disk1_spare_pct": 312,
    "disk1_failure": 313,
    "disk1_warning": 314,
    "disk_read_activity_pct": 329,
    "disk_write_activity_pct": 330,
    "disk_total_activity_pct": 331,
    "disk_read_mbs": 332,
    "disk_write_mbs": 333,
    "virtual_mem_load_pct": 4,
    "physical_mem_used_mb": 5,
    "physical_mem_available_mb": 6,
    "physical_mem_load_pct": 7,
    "pagefile_use_pct": 8,
    "ram_clock_mhz": 244,
}


def dedupe_columns(columns: list[str]) -> list[str]:
    seen: dict[str, int] = {}
    result: list[str] = []
    for i, name in enumerate(columns):
        if i == 0:
            name = name.lstrip("\ufeff").removeprefix("ï»¿")
        if name in seen:
            seen[name] += 1
            result.append(f"{name}#{seen[name]}")
        else:
            seen[name] = 1
            result.append(name)
    return result


@st.cache_data(show_spinner=False)
def load_csv(path: str) -> pd.DataFrame:
    last_error: Exception | None = None
    for encoding in ("utf-8-sig", "cp1252", "latin1"):
        try:
            with open(path, "r", encoding=encoding) as f:
                first_line = f.readline().rstrip("\n\r")
            header = next(csv.reader(StringIO(first_line)), None)
            if not header:
                raise ValueError("Cabecalho vazio.")
            columns = dedupe_columns(header)
            return pd.read_csv(path, names=columns, skiprows=1, encoding=encoding)
        except UnicodeDecodeError as exc:
            last_error = exc
    raise RuntimeError(f"Nao foi possivel ler o CSV com os encodings conhecidos: {last_error}")


def col(df: pd.DataFrame, index: int) -> pd.Series:
    if index >= len(df.columns):
        return pd.Series(dtype="float64")
    return pd.to_numeric(df.iloc[:, index].astype(str).str.replace(",", ".", regex=False), errors="coerce")


def avg_cols(df: pd.DataFrame, indexes: list[int]) -> pd.Series:
    parts = [col(df, i) for i in indexes if i < len(df.columns)]
    if not parts:
        return pd.Series(index=df.index, dtype="float64")
    return pd.concat(parts, axis=1).mean(axis=1)


def yes_count(df: pd.DataFrame, index: int) -> int:
    if index >= len(df.columns):
        return 0
    values = df.iloc[:, index].astype(str).str.strip().str.lower()
    return int(values.isin(["sim", "yes"]).sum())


def stats(series: pd.Series) -> dict[str, float]:
    clean = series.dropna()
    if clean.empty:
        return {"min": float("nan"), "avg": float("nan"), "max": float("nan")}
    return {"min": clean.min(), "avg": clean.mean(), "max": clean.max()}


def metric_row(items: list[tuple[str, float | int | str, str | None]]) -> None:
    cols = st.columns(len(items))
    for c, (label, value, suffix) in zip(cols, items):
        if isinstance(value, float):
            text = f"{value:,.1f}".replace(",", "X").replace(".", ",").replace("X", ".")
        else:
            text = str(value)
        if suffix:
            text = f"{text} {suffix}"
        c.metric(label, text)


def make_time(df: pd.DataFrame) -> pd.Series:
    raw = df.iloc[:, 0].astype(str) + " " + df.iloc[:, 1].astype(str)
    parsed = pd.to_datetime(raw, format="%d.%m.%Y %H:%M:%S.%f", errors="coerce")
    if parsed.isna().all():
        parsed = pd.Series(pd.RangeIndex(len(df)), index=df.index)
    return parsed


def build_series(df: pd.DataFrame) -> pd.DataFrame:
    out = pd.DataFrame(index=df.index)
    out["time"] = make_time(df)
    out["P-core clock avg MHz"] = avg_cols(df, INDEX["p_core_clock"])
    out["E-core clock avg MHz"] = avg_cols(df, INDEX["e_core_clock"])
    out["P-core effective avg MHz"] = avg_cols(df, INDEX["p_core_effective"])
    out["E-core effective avg MHz"] = avg_cols(df, INDEX["e_core_effective"])
    out["CPU package W"] = col(df, INDEX["cpu_package_w"])
    out["System total W"] = col(df, INDEX["system_total_w"])
    out["CPU package C"] = col(df, INDEX["cpu_package_temp_c"])
    out["GPU W"] = col(df, INDEX["gpu_power_w"])
    out["GPU temp C"] = col(df, INDEX["gpu_temp_c"])
    out["GPU hotspot C"] = col(df, INDEX["gpu_hotspot_c"])
    out["GPU core load %"] = col(df, INDEX["gpu_core_load_pct"])
    out["GPU memory use %"] = col(df, INDEX["gpu_mem_use_pct"])
    out["Physical memory load %"] = col(df, INDEX["physical_mem_load_pct"])
    out["Disk total activity %"] = col(df, INDEX["disk_total_activity_pct"])
    return out


def show_stats_table(title: str, data: dict[str, pd.Series]) -> None:
    rows = []
    for name, series in data.items():
        s = stats(series)
        rows.append({"Metric": name, "Min": s["min"], "Avg": s["avg"], "Max": s["max"]})
    st.subheader(title)
    st.dataframe(pd.DataFrame(rows), use_container_width=True, hide_index=True)


def main() -> None:
    st.set_page_config(page_title="Cinebench / HWiNFO stats", layout="wide")
    st.title("Cinebench 2026 + HWiNFO")
    st.caption("Resumo de potencia, temperatura, clocks e dispositivos.")

    if CONTAINER_DEFAULT_DIR.exists():
        default_dir = CONTAINER_DEFAULT_DIR
    else:
        default_dir = ALT_DEFAULT_DIR if ALT_DEFAULT_DIR.exists() else DEFAULT_DIR
    with st.sidebar:
        st.header("Entrada")
        csv_dir = Path(st.text_input("Pasta dos CSVs", str(default_dir)))
        files = sorted(csv_dir.glob("*.CSV")) + sorted(csv_dir.glob("*.csv")) if csv_dir.exists() else []
        names = [f.name for f in files]
        default_index = next((i for i, n in enumerate(names) if n.endswith("-G.CSV")), 0)
        selected_name = st.selectbox("Relatorio", names, index=default_index if names else 0)
        selected = csv_dir / selected_name if selected_name else None

        st.header("Scores")
        scores = {
            "GPU": st.number_input("GPU", value=int(SCORES["G completo - GPU"])),
            "CPU multi": st.number_input("CPU multi", value=int(SCORES["G completo - CPU multi"])),
            "CPU single core": st.number_input("CPU single core", value=int(SCORES["G completo - CPU single core"])),
            "CPU single thread": st.number_input("CPU single thread", value=int(SCORES["G completo - CPU single thread"])),
            "MP ratio": st.number_input("MP ratio", value=float(SCORES["G completo - MP ratio"])),
        }

    if not selected or not selected.exists():
        st.warning("Selecione um CSV valido.")
        return

    df = load_csv(str(selected))
    ts = build_series(df)

    st.write(f"Arquivo: `{selected}`")
    metric_row(
        [
            ("Amostras", len(df), None),
            ("GPU score", scores["GPU"], "pts"),
            ("CPU multi", scores["CPU multi"], "pts"),
            ("Single core", scores["CPU single core"], "pts"),
            ("Single thread", scores["CPU single thread"], "pts"),
            ("MP ratio", scores["MP ratio"], "x"),
        ]
    )

    st.divider()

    cpu_load = ts["CPU package W"] >= 70
    gpu_load = ts["GPU core load %"] >= 90
    active = cpu_load | gpu_load

    sys_all = stats(ts["System total W"])
    sys_cpu = stats(ts.loc[cpu_load, "System total W"])
    sys_gpu = stats(ts.loc[gpu_load, "System total W"])
    sys_active = stats(ts.loc[active, "System total W"])

    st.subheader("Potencia total do sistema")
    metric_row(
        [
            ("Geral media", sys_all["avg"], "W"),
            ("Geral pico", sys_all["max"], "W"),
            ("CPU forte media", sys_cpu["avg"], "W"),
            ("CPU forte pico", sys_cpu["max"], "W"),
            ("GPU forte media", sys_gpu["avg"], "W"),
            ("Ativo pico", sys_active["max"], "W"),
        ]
    )

    show_stats_table(
        "CPU",
        {
            "CPU package W": ts["CPU package W"],
            "CPU package C": ts["CPU package C"],
            "P-core clock avg MHz": ts["P-core clock avg MHz"],
            "E-core clock avg MHz": ts["E-core clock avg MHz"],
            "P-core effective avg MHz": ts["P-core effective avg MHz"],
            "E-core effective avg MHz": ts["E-core effective avg MHz"],
        },
    )

    show_stats_table(
        "CPU forte (CPU package >= 70 W)",
        {
            "CPU package W": ts.loc[cpu_load, "CPU package W"],
            "CPU package C": ts.loc[cpu_load, "CPU package C"],
            "P-core clock avg MHz": ts.loc[cpu_load, "P-core clock avg MHz"],
            "E-core clock avg MHz": ts.loc[cpu_load, "E-core clock avg MHz"],
            "P-core effective avg MHz": ts.loc[cpu_load, "P-core effective avg MHz"],
            "E-core effective avg MHz": ts.loc[cpu_load, "E-core effective avg MHz"],
        },
    )

    show_stats_table(
        "RTX GPU",
        {
            "GPU W": ts["GPU W"],
            "GPU temp C": ts["GPU temp C"],
            "GPU hotspot C": ts["GPU hotspot C"],
            "GPU core load %": ts["GPU core load %"],
            "GPU memory use %": ts["GPU memory use %"],
            "GPU memory allocated MB": col(df, INDEX["gpu_mem_allocated_mb"]),
            "GPU clock MHz": col(df, INDEX["gpu_clock_mhz"]),
            "GPU effective clock MHz": col(df, INDEX["gpu_effective_clock_mhz"]),
        },
    )

    show_stats_table(
        "Memoria / Disco / Bateria",
        {
            "RAM load %": ts["Physical memory load %"],
            "RAM used MB": col(df, INDEX["physical_mem_used_mb"]),
            "RAM available MB": col(df, INDEX["physical_mem_available_mb"]),
            "Pagefile use %": col(df, INDEX["pagefile_use_pct"]),
            "Disk total activity %": ts["Disk total activity %"],
            "Disk 1 temp C": col(df, INDEX["disk1_temp_c"]),
            "Disk 2 temp C": col(df, INDEX["disk2_temp_c"]),
            "Disk 3 temp C": col(df, INDEX["disk3_temp_c"]),
            "Battery charge %": col(df, INDEX["battery_charge_pct"]),
            "Battery wear %": col(df, INDEX["battery_wear_pct"]),
        },
    )

    st.subheader("Limitadores")
    limiter_rows = [
        ("GPU perf limiter avg", yes_count(df, INDEX["gpu_perf_limiter"])),
        ("GPU limit power", yes_count(df, INDEX["gpu_perf_power"])),
        ("GPU limit thermal", yes_count(df, INDEX["gpu_perf_thermal"])),
        ("GPU limit reliability voltage", yes_count(df, INDEX["gpu_perf_reliability_voltage"])),
        ("GPU limit utilization", yes_count(df, INDEX["gpu_perf_utilization"])),
        ("Disk failure", yes_count(df, INDEX["disk1_failure"])),
        ("Disk warning", yes_count(df, INDEX["disk1_warning"])),
    ]
    st.dataframe(pd.DataFrame(limiter_rows, columns=["Evento", "Amostras"]), use_container_width=True, hide_index=True)

    st.divider()
    st.subheader("Graficos no tempo")
    chart_data = ts.set_index("time")
    tab1, tab2, tab3, tab4 = st.tabs(["Clocks", "Potencia", "Temperatura", "GPU/RAM/Disco"])
    with tab1:
        st.line_chart(
            chart_data[
                [
                    "P-core clock avg MHz",
                    "E-core clock avg MHz",
                    "P-core effective avg MHz",
                    "E-core effective avg MHz",
                ]
            ],
            height=360,
        )
    with tab2:
        st.line_chart(chart_data[["System total W", "CPU package W", "GPU W"]], height=360)
    with tab3:
        st.line_chart(chart_data[["CPU package C", "GPU temp C", "GPU hotspot C"]], height=360)
    with tab4:
        st.line_chart(chart_data[["GPU core load %", "GPU memory use %", "Physical memory load %", "Disk total activity %"]], height=360)

    st.subheader("Serie temporal extraida")
    st.dataframe(ts, use_container_width=True)
    st.download_button(
        "Baixar serie temporal limpa",
        ts.to_csv(index=False).encode("utf-8"),
        file_name="cinebench_hwinfo_series.csv",
        mime="text/csv",
    )


if __name__ == "__main__":
    main()
