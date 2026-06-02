from __future__ import annotations

import csv
import json
import math
import re
import unicodedata
from dataclasses import dataclass
from datetime import datetime
from io import BytesIO, StringIO
from pathlib import Path
from typing import Any

import altair as alt
import pandas as pd
import streamlit as st
import streamlit.components.v1 as components


APP_VERSION = "0.2.0"
CONTAINER_REPORT_DIR = Path("/data/reports")
CONTAINER_BENCHMARK_DIR = Path("/data/benchmarks")

DEFAULT_DIR = Path(
    "/mnt/c/Users/gabri/OneDrive/Documents/tools/Desmerdificar o windows/"
    "relatorio de sensores/cinebench 2026"
)

ALT_DEFAULT_DIR = Path(
    "/mnt/c/Users/gabri/OneDrive/Documents/tools/Desmerdíficar o windows/"
    "relatório de sensores/cinebench 2026"
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

TEXT = {
    "pt": {
        "page": "Telemetry Lab",
        "tagline": "Logs HWiNFO, benchmarks e comparações sem depender de prints.",
        "language": "Idioma",
        "report": "Relatório",
        "compare": "Comparar",
        "benchmarks": "Benchmarks",
        "custom_chart": "Gráfico customizado",
        "input": "Entrada",
        "csv_path": "Caminho do CSV no servidor/container",
        "upload_csv": "Ou envie um CSV pelo navegador",
        "live_reload": "Ler dinamicamente arquivo ainda em gravação",
        "refresh_seconds": "Atualizar a cada N segundos",
        "samples": "Amostras",
        "sensors": "Sensores",
        "context": "Contexto",
        "generic_context": "Relatório geral",
        "overview": "Visão geral",
        "stats": "Estatísticas",
        "charts": "Gráficos",
        "raw": "Dados brutos",
        "save": "Salvar",
        "download": "Baixar arquivo",
        "server_save_dir": "Pasta para salvar no servidor/container",
        "benchmark_name": "Nome do benchmark",
        "scenario": "Cenário/contexto",
        "scores": "Scores",
        "linked_report": "Relatório relacionado",
        "current_report": "Usar relatório atual",
        "chart_type": "Tipo de gráfico",
        "x_axis": "Eixo X",
        "y_axis": "Dados",
        "category": "Categoria",
        "metric_picker": "Métricas",
        "no_report": "Informe um CSV por caminho ou upload.",
        "saved": "Arquivo salvo.",
        "cannot_save": "Essa pasta não existe ou não está acessível pelo processo.",
        "loaded_files": "Arquivos registrados",
    },
    "en": {
        "page": "Telemetry Lab",
        "tagline": "HWiNFO logs, benchmark records, and comparisons without screenshots.",
        "language": "Language",
        "report": "Report",
        "compare": "Compare",
        "benchmarks": "Benchmarks",
        "custom_chart": "Custom chart",
        "input": "Input",
        "csv_path": "CSV path on server/container",
        "upload_csv": "Or upload a CSV through the browser",
        "live_reload": "Dynamically read a CSV that is still being written",
        "refresh_seconds": "Refresh every N seconds",
        "samples": "Samples",
        "sensors": "Sensors",
        "context": "Context",
        "generic_context": "General report",
        "overview": "Overview",
        "stats": "Stats",
        "charts": "Charts",
        "raw": "Raw data",
        "save": "Save",
        "download": "Download file",
        "server_save_dir": "Save folder on server/container",
        "benchmark_name": "Benchmark name",
        "scenario": "Scenario/context",
        "scores": "Scores",
        "linked_report": "Linked report",
        "current_report": "Use current report",
        "chart_type": "Chart type",
        "x_axis": "X axis",
        "y_axis": "Data",
        "category": "Category",
        "metric_picker": "Metrics",
        "no_report": "Provide a CSV path or upload.",
        "saved": "File saved.",
        "cannot_save": "This folder does not exist or is not accessible to the process.",
        "loaded_files": "Registered files",
    },
}


@dataclass
class Report:
    source: str
    df: pd.DataFrame
    time: pd.Series
    numeric: pd.DataFrame
    context: dict[str, Any]
    mtime_ns: int | None = None
    size: int | None = None


def tr(key: str) -> str:
    lang = st.session_state.get("lang", "pt")
    return TEXT[lang].get(key, key)


def slugify(value: str) -> str:
    text = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode("ascii")
    text = re.sub(r"[^a-zA-Z0-9]+", "-", text.lower()).strip("-")
    return text or "telemetry"


def pretty_token(value: str) -> str:
    value = re.sub(r"[_-]+", " ", value).strip()
    return value.title() if value else value


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


def dedupe_columns(columns: list[str]) -> list[str]:
    seen: dict[str, int] = {}
    result: list[str] = []
    for i, name in enumerate(columns):
        if i == 0:
            name = name.lstrip("\ufeff").removeprefix("ï»¿")
        name = name.strip() or f"column_{i}"
        if name in seen:
            seen[name] += 1
            result.append(f"{name}#{seen[name]}")
        else:
            seen[name] = 1
            result.append(name)
    return result


def decode_csv_bytes(data: bytes) -> tuple[list[str], str]:
    last_error: Exception | None = None
    for encoding in ("utf-8-sig", "cp1252", "latin1"):
        try:
            text = data.decode(encoding)
            first_line = text.splitlines()[0] if text.splitlines() else ""
            header = next(csv.reader(StringIO(first_line)), None)
            if not header:
                raise ValueError("Empty header")
            return dedupe_columns(header), encoding
        except UnicodeDecodeError as exc:
            last_error = exc
    raise RuntimeError(f"Could not read CSV with known encodings: {last_error}")


@st.cache_data(show_spinner=False)
def load_csv_path_cached(path: str, mtime_ns: int, size: int) -> pd.DataFrame:
    del mtime_ns, size
    data = Path(path).read_bytes()
    columns, encoding = decode_csv_bytes(data[:262_144])
    return pd.read_csv(path, names=columns, skiprows=1, encoding=encoding)


def load_csv_path(path: str, live_reload: bool) -> tuple[pd.DataFrame, int, int]:
    p = Path(path).expanduser()
    stat = p.stat()
    if live_reload:
        columns, encoding = decode_csv_bytes(p.read_bytes()[:262_144])
        df = pd.read_csv(p, names=columns, skiprows=1, encoding=encoding)
    else:
        df = load_csv_path_cached(str(p), stat.st_mtime_ns, stat.st_size)
    return df, stat.st_mtime_ns, stat.st_size


def load_uploaded_csv(name: str, data: bytes) -> pd.DataFrame:
    columns, encoding = decode_csv_bytes(data[:262_144])
    return pd.read_csv(BytesIO(data), names=columns, skiprows=1, encoding=encoding)


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


def infer_context(source: str) -> dict[str, Any]:
    p = Path(source)
    raw_parts = [part for part in p.parts if part not in ("/", "\\")]
    tokens = [slugify(part) for part in raw_parts if slugify(part)]
    hits = []
    for token in tokens:
        if token in KNOWN_CONTEXT_TERMS or any(term in token for term in KNOWN_CONTEXT_TERMS):
            hits.append(token)
    parent = slugify(p.parent.name)
    stem = slugify(p.stem)
    workload = hits[-1] if hits else parent or stem or "geral"
    category = "geral"
    joined = "/".join(tokens)
    if any(term in joined for term in ("games", "game", "valorant")):
        category = "games"
    elif any(term in joined for term in ("produtividade", "productivity")):
        category = "produtividade"
    elif any(term in joined for term in ("benchmarks", "benchmark", "cinebench", "geekbench")):
        category = "benchmarks"
    title_bits = [pretty_token(category), pretty_token(workload)]
    title = " / ".join(dict.fromkeys([bit for bit in title_bits if bit]))
    if not title:
        title = tr("generic_context")
    return {
        "title": title,
        "category": category,
        "workload": workload,
        "tags": sorted(set(hits)) or ["geral"],
        "file_name": p.name,
        "folder": str(p.parent) if str(p.parent) != "." else "",
    }


def find_columns(df: pd.DataFrame, *patterns: str) -> list[str]:
    found: list[str] = []
    for col in df.columns:
        low = col.lower()
        if all(re.search(pattern, low) for pattern in patterns):
            found.append(col)
    return found


def avg_matching(df: pd.DataFrame, *patterns: str) -> pd.Series:
    cols = find_columns(df, *patterns)
    parts = [numeric_series(df[col]) for col in cols]
    if not parts:
        return pd.Series(index=df.index, dtype="float64")
    return pd.concat(parts, axis=1).mean(axis=1)


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
        "CPU package W": col_by_index(df, INDEX["cpu_package_w"]),
        "System total W": col_by_index(df, INDEX["system_total_w"]),
        "CPU package C": col_by_index(df, INDEX["cpu_package_temp_c"]),
        "GPU W": col_by_index(df, INDEX["gpu_power_w"]),
        "GPU temp C": col_by_index(df, INDEX["gpu_temp_c"]),
        "GPU hotspot C": col_by_index(df, INDEX["gpu_hotspot_c"]),
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
        "CPU package W": avg_matching(df, "cpu", "package", "(w|power|pot.ncia)"),
        "System total W": avg_matching(df, "system", "(total|power|w)"),
        "CPU package C": avg_matching(df, "cpu", "package", "(c|temp)"),
        "GPU W": avg_matching(df, "gpu", "(power|w|pot.ncia)"),
        "GPU temp C": avg_matching(df, "gpu", "(temp|c)"),
        "GPU core load %": avg_matching(df, "gpu", "(core|load|util)"),
        "GPU memory use %": avg_matching(df, "gpu", "(memory|mem.ria)", "(use|load|util)"),
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


def make_report(source: str, df: pd.DataFrame, mtime_ns: int | None = None, size: int | None = None) -> Report:
    return Report(
        source=source,
        df=df,
        time=build_time(df),
        numeric=build_numeric(df),
        context=infer_context(source),
        mtime_ns=mtime_ns,
        size=size,
    )


def load_report_widget(prefix: str, default_path: str = "") -> Report | None:
    path = st.text_input(tr("csv_path"), value=default_path, key=f"{prefix}_path")
    upload = st.file_uploader(tr("upload_csv"), type=["csv", "CSV"], key=f"{prefix}_upload")
    live = st.checkbox(tr("live_reload"), value=False, key=f"{prefix}_live")
    if live:
        refresh = st.number_input(tr("refresh_seconds"), min_value=2, max_value=120, value=10, key=f"{prefix}_refresh")
        components.html(f"<meta http-equiv='refresh' content='{int(refresh)}'>", height=0)
    try:
        if upload is not None:
            data = upload.getvalue()
            return make_report(upload.name, load_uploaded_csv(upload.name, data), size=len(data))
        if path.strip():
            p = Path(path).expanduser()
            if p.is_dir():
                files = sorted(p.rglob("*.csv")) + sorted(p.rglob("*.CSV"))
                if files:
                    chosen = st.selectbox("CSV", files, format_func=lambda item: str(item), key=f"{prefix}_csv_select")
                    df, mtime_ns, size = load_csv_path(str(chosen), live)
                    return make_report(str(chosen), df, mtime_ns, size)
            elif p.exists():
                df, mtime_ns, size = load_csv_path(str(p), live)
                return make_report(str(p), df, mtime_ns, size)
    except Exception as exc:
        st.error(str(exc))
    return None


def metric_row(values: list[tuple[str, Any, str | None]]) -> None:
    cols = st.columns(max(1, len(values)))
    for col, (label, value, suffix) in zip(cols, values):
        if isinstance(value, float):
            text = f"{value:,.1f}".replace(",", "X").replace(".", ",").replace("X", ".")
        else:
            text = str(value)
        col.metric(label, f"{text} {suffix}" if suffix else text)


def category_for_metric(name: str) -> str:
    low = name.lower()
    if "gpu" in low:
        return "GPU"
    if "cpu" in low or "core" in low:
        return "CPU"
    if "mem" in low or "ram" in low:
        return "Memória"
    if "disk" in low or "ssd" in low or "drive" in low:
        return "Disco"
    if "temp" in low or " c" in low:
        return "Temperatura"
    if "w" in low or "power" in low:
        return "Potência"
    return "Outros"


def chart_source(report: Report, columns: list[str]) -> pd.DataFrame:
    data = report.numeric[columns].copy()
    data.insert(0, "time", report.time.values)
    return data


def render_chart(report: Report, chart_type: str, x_axis: str, y_axis: list[str], height: int = 360) -> None:
    if not y_axis:
        st.info("Selecione ao menos uma métrica.")
        return
    data = chart_source(report, y_axis)
    if x_axis != "time" and x_axis in report.numeric.columns:
        data[x_axis] = report.numeric[x_axis]
    if chart_type == "Tabela":
        st.dataframe(data, use_container_width=True)
        return
    if chart_type == "Heatmap":
        corr = report.numeric[y_axis].corr(numeric_only=True).reset_index().melt("index")
        chart = (
            alt.Chart(corr)
            .mark_rect()
            .encode(
                x=alt.X("index:N", title=""),
                y=alt.Y("variable:N", title=""),
                color=alt.Color("value:Q", scale=alt.Scale(scheme="redblue", domain=[-1, 1])),
                tooltip=["index", "variable", alt.Tooltip("value:Q", format=".3f")],
            )
            .properties(height=height)
        )
        st.altair_chart(chart, use_container_width=True)
        return
    long = data.melt(id_vars=[x_axis], value_vars=y_axis, var_name="Metric", value_name="Value").dropna()
    base = alt.Chart(long).encode(
        x=alt.X(f"{x_axis}:T" if x_axis == "time" and pd.api.types.is_datetime64_any_dtype(long[x_axis]) else f"{x_axis}:Q"),
        y=alt.Y("Value:Q"),
        color="Metric:N",
        tooltip=[x_axis, "Metric", alt.Tooltip("Value:Q", format=".2f")],
    )
    if chart_type == "Área":
        chart = base.mark_area(opacity=0.45)
    elif chart_type == "Dispersão":
        chart = base.mark_circle(size=36, opacity=0.65)
    elif chart_type == "Barras":
        chart = base.mark_bar(opacity=0.75)
    else:
        chart = base.mark_line()
    st.altair_chart(chart.properties(height=height).interactive(), use_container_width=True)


def render_report(report: Report) -> None:
    st.session_state["current_report_source"] = report.source
    st.session_state["current_report_context"] = report.context
    ctx = report.context
    st.subheader(ctx["title"])
    st.caption(f"{report.source}")
    metric_row(
        [
            (tr("samples"), len(report.df), None),
            (tr("sensors"), len(report.df.columns), None),
            (tr("category"), pretty_token(ctx["category"]), None),
            (tr("context"), pretty_token(ctx["workload"]), None),
        ]
    )
    stats = stats_frame(report.numeric)
    tab_overview, tab_stats, tab_charts, tab_raw = st.tabs([tr("overview"), tr("stats"), tr("charts"), tr("raw")])
    with tab_overview:
        preferred = [
            col
            for col in [
                "System total W",
                "CPU package W",
                "GPU W",
                "CPU package C",
                "GPU temp C",
                "GPU core load %",
                "Physical memory load %",
                "Disk total activity %",
            ]
            if col in report.numeric.columns
        ]
        if preferred:
            key_stats = stats[stats["Metric"].isin(preferred)]
            st.dataframe(key_stats, use_container_width=True, hide_index=True)
            render_chart(report, "Linha", "time", preferred[:6], 360)
        limiter_rows = [
            ("GPU perf limiter", yes_count(report.df, INDEX["gpu_perf_limiter"])),
            ("GPU limit power", yes_count(report.df, INDEX["gpu_perf_power"])),
            ("GPU limit thermal", yes_count(report.df, INDEX["gpu_perf_thermal"])),
            ("Disk failure", yes_count(report.df, INDEX["disk1_failure"])),
            ("Disk warning", yes_count(report.df, INDEX["disk1_warning"])),
        ]
        limiter_df = pd.DataFrame(limiter_rows, columns=["Evento", "Amostras"])
        limiter_df = limiter_df[limiter_df["Amostras"] > 0]
        if not limiter_df.empty:
            st.dataframe(limiter_df, use_container_width=True, hide_index=True)
    with tab_stats:
        categories = sorted({category_for_metric(col) for col in report.numeric.columns})
        chosen_category = st.selectbox(tr("category"), ["Todos"] + categories)
        visible = stats if chosen_category == "Todos" else stats[stats["Metric"].map(category_for_metric) == chosen_category]
        st.dataframe(visible, use_container_width=True, hide_index=True)
        st.download_button(
            tr("download"),
            stats.to_csv(index=False).encode("utf-8"),
            file_name=f"{slugify(ctx['title'])}-stats.csv",
            mime="text/csv",
            key="stats_download",
        )
    with tab_charts:
        groups = {
            "Potência": [c for c in report.numeric.columns if " W" in c or "power" in c.lower()],
            "Temperatura": [c for c in report.numeric.columns if "temp" in c.lower() or " C" in c],
            "Carga": [c for c in report.numeric.columns if "%" in c or "load" in c.lower()],
            "Clocks": [c for c in report.numeric.columns if "clock" in c.lower() or "mhz" in c.lower()],
            "Tudo": list(report.numeric.columns),
        }
        group = st.selectbox(tr("category"), [name for name, cols in groups.items() if cols] or ["Tudo"])
        default = groups.get(group, list(report.numeric.columns))[:5]
        cols = st.multiselect(tr("metric_picker"), list(report.numeric.columns), default=default)
        chart_type = st.selectbox(tr("chart_type"), ["Linha", "Área", "Dispersão", "Barras", "Heatmap", "Tabela"])
        render_chart(report, chart_type, "time", cols)
    with tab_raw:
        st.dataframe(report.df, use_container_width=True)
        st.download_button(
            tr("download"),
            report.df.to_csv(index=False).encode("utf-8"),
            file_name=f"{slugify(ctx['title'])}-raw.csv",
            mime="text/csv",
            key="raw_download",
        )


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


def render_benchmarks() -> None:
    st.subheader(tr("benchmarks"))
    default_scores = pd.DataFrame(
        [
            {"Metric": "GPU", "Value": 18089, "Unit": "pts"},
            {"Metric": "CPU multi", "Value": 3727, "Unit": "pts"},
            {"Metric": "CPU single core", "Value": 563, "Unit": "pts"},
            {"Metric": "CPU single thread", "Value": 420, "Unit": "pts"},
            {"Metric": "MP ratio", "Value": 8.86, "Unit": "x"},
        ]
    )
    with st.form("benchmark_form"):
        name = st.text_input(tr("benchmark_name"), value="Cinebench 2026")
        scenario = st.text_input(tr("scenario"), value=st.session_state.get("current_report_context", {}).get("title", "geral"))
        scores = st.data_editor(
            default_scores,
            num_rows="dynamic",
            use_container_width=True,
            column_config={"Metric": st.column_config.TextColumn(required=True), "Value": st.column_config.TextColumn()},
        )
        use_current = st.checkbox(tr("current_report"), value=bool(st.session_state.get("current_report_source")))
        save_dir = st.text_input(tr("server_save_dir"), value=str(CONTAINER_BENCHMARK_DIR if CONTAINER_BENCHMARK_DIR.exists() else Path.cwd()))
        submitted = st.form_submit_button(tr("save"))
    linked = None
    if use_current and st.session_state.get("current_report_source"):
        linked = {"source": st.session_state["current_report_source"], "context": st.session_state.get("current_report_context", {})}
    payload = benchmark_payload(name, scenario, scores, linked)
    encoded = json.dumps(payload, ensure_ascii=False, indent=2).encode("utf-8")
    file_name = f"{slugify(payload['benchmark'])}-{slugify(payload['scenario'])}-{datetime.now():%Y%m%d-%H%M%S}.telemetry-benchmark.json"
    if submitted:
        target = Path(save_dir).expanduser()
        if target.exists() and target.is_dir():
            out = target / file_name
            out.write_bytes(encoded)
            st.success(f"{tr('saved')} {out}")
        else:
            st.warning(tr("cannot_save"))
    st.download_button(tr("download"), encoded, file_name=file_name, mime="application/json")

    st.divider()
    st.subheader(tr("loaded_files"))
    read_dir = st.text_input("Pasta com JSONs", value=save_dir, key="read_bench_dir")
    files = []
    p = Path(read_dir).expanduser()
    if p.exists() and p.is_dir():
        files = sorted(p.glob("*.telemetry-benchmark.json")) + sorted(p.glob("*.json"))
    if files:
        chosen = st.selectbox("Arquivo", files, format_func=lambda item: item.name)
        try:
            data = json.loads(chosen.read_text(encoding="utf-8"))
            st.json(data)
            scores_df = pd.DataFrame(data.get("scores", []))
            if not scores_df.empty:
                st.dataframe(scores_df, use_container_width=True, hide_index=True)
        except Exception as exc:
            st.error(str(exc))


def render_compare() -> None:
    left, right = st.columns(2)
    with left:
        st.markdown("### A")
        a = load_report_widget("compare_a", default_report_path())
    with right:
        st.markdown("### B")
        b = load_report_widget("compare_b", "")
    if not a or not b:
        return
    common = sorted(set(a.numeric.columns) & set(b.numeric.columns))
    if not common:
        st.warning("Não há métricas numéricas em comum.")
        return
    metrics = st.multiselect(tr("metric_picker"), common, default=common[: min(5, len(common))])
    rows = []
    for label, report in [("A", a), ("B", b)]:
        for metric in metrics:
            clean = report.numeric[metric].dropna()
            rows.append(
                {
                    "Report": label,
                    "Context": report.context["title"],
                    "Metric": metric,
                    "Avg": clean.mean() if not clean.empty else math.nan,
                    "Max": clean.max() if not clean.empty else math.nan,
                    "Samples": int(clean.count()),
                }
            )
    comp = pd.DataFrame(rows)
    st.dataframe(comp, use_container_width=True, hide_index=True)
    chart = (
        alt.Chart(comp)
        .mark_bar()
        .encode(x="Metric:N", y="Avg:Q", color="Report:N", column="Context:N", tooltip=list(comp.columns))
        .properties(height=320)
    )
    st.altair_chart(chart, use_container_width=True)


def render_custom_chart(report: Report | None) -> None:
    if not report:
        st.info(tr("no_report"))
        return
    cols = list(report.numeric.columns)
    if not cols:
        st.warning("Nenhuma métrica numérica foi detectada.")
        return
    chart_type = st.selectbox(tr("chart_type"), ["Linha", "Área", "Dispersão", "Barras", "Heatmap", "Tabela"], key="custom_type")
    x_options = ["time"] + cols
    x_axis = st.selectbox(tr("x_axis"), x_options, key="custom_x")
    y_axis = st.multiselect(tr("y_axis"), cols, default=cols[: min(3, len(cols))], key="custom_y")
    render_chart(report, chart_type, x_axis, y_axis, height=460)


def main() -> None:
    st.set_page_config(page_title="Telemetry Lab", layout="wide")
    with st.sidebar:
        lang_label = st.selectbox("Idioma / Language", ["pt", "en"], format_func=lambda item: "Português" if item == "pt" else "English")
        st.session_state["lang"] = lang_label
        st.caption(f"Telemetry Lab {APP_VERSION}")
    st.title(tr("page"))
    st.caption(tr("tagline"))

    tab_report, tab_compare, tab_bench, tab_custom = st.tabs([tr("report"), tr("compare"), tr("benchmarks"), tr("custom_chart")])
    report: Report | None = None
    with tab_report:
        with st.sidebar:
            st.header(tr("input"))
            report = load_report_widget("main", default_report_path())
        if report:
            render_report(report)
        else:
            st.info(tr("no_report"))
    with tab_compare:
        render_compare()
    with tab_bench:
        render_benchmarks()
    with tab_custom:
        render_custom_chart(report)


if __name__ == "__main__":
    main()
