# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

import json
import math
from datetime import datetime
from pathlib import Path
from typing import Any

import altair as alt
import pandas as pd
import streamlit as st
import streamlit.components.v1 as components

from telemetry_lab import APP_VERSION
from telemetry_lab.analysis import make_report, stats_frame, yes_count
from telemetry_lab.benchmark_records import benchmark_payload
from telemetry_lab.charts import render_chart
from telemetry_lab.config import INDEX, default_report_path
from telemetry_lab.csv_io import load_csv_path, load_uploaded_csv, parse_hwinfo_csv_bytes
from telemetry_lab.i18n import translate
from telemetry_lab.metrics import fps_metrics, metric_label, metric_options_for_query, power_metrics, temperature_metrics
from telemetry_lab.models import Report
from telemetry_lab.report_views import render_fps_view, render_glossary_view, render_power_view, render_temperature_view
from telemetry_lab.text_utils import category_for_metric, pretty_token, repair_mojibake, slugify
from telemetry_lab.units import display_numeric_frame, normalize_temperature_unit


def tr(key: str) -> str:
    lang = st.session_state.get("lang", "pt")
    return translate(lang, key)


def selected_temperature_unit() -> str:
    return normalize_temperature_unit(st.session_state.get("temperature_unit", "C"))


def display_report(report: Report) -> Report:
    return Report(
        source=report.source,
        df=report.df,
        time=report.time,
        numeric=display_numeric_frame(report.numeric, selected_temperature_unit()),
        context=report.context,
        mtime_ns=report.mtime_ns,
        size=report.size,
    )


@st.cache_data(show_spinner=False)
def load_csv_path_cached(path: str, mtime_ns: int, size: int) -> pd.DataFrame:
    del mtime_ns, size
    return parse_hwinfo_csv_bytes(Path(path).read_bytes())


def make_ui_report(source: str, df: pd.DataFrame, mtime_ns: int | None = None, size: int | None = None) -> Report:
    return make_report(source, df, mtime_ns=mtime_ns, size=size, translate=tr)


def load_report_widget(prefix: str, default_path: str = "") -> Report | None:
    upload = st.file_uploader(tr("upload_csv"), type=["csv", "CSV"], key=f"{prefix}_upload")
    with st.expander(tr("optional_path"), expanded=bool(default_path)):
        path = st.text_input(tr("csv_path"), value=default_path, help=tr("path_help"), key=f"{prefix}_path")
        live = st.checkbox(tr("live_reload"), value=False, key=f"{prefix}_live")
        if live:
            refresh = st.number_input(tr("refresh_seconds"), min_value=2, max_value=120, value=10, key=f"{prefix}_refresh")
            components.html(f"<meta http-equiv='refresh' content='{int(refresh)}'>", height=0)
    try:
        if upload is not None:
            data = upload.getvalue()
            return make_ui_report(upload.name, load_uploaded_csv(upload.name, data), size=len(data))
        if path.strip():
            p = Path(path).expanduser()
            if p.is_dir():
                files = sorted(p.rglob("*.csv")) + sorted(p.rglob("*.CSV"))
                if files:
                    chosen = st.selectbox("CSV", files, format_func=lambda item: repair_mojibake(str(item)), key=f"{prefix}_csv_select")
                    df, mtime_ns, size = load_csv_path(str(chosen), live, load_csv_path_cached)
                    return make_ui_report(str(chosen), df, mtime_ns, size)
            elif p.exists():
                df, mtime_ns, size = load_csv_path(str(p), live, load_csv_path_cached)
                return make_ui_report(str(p), df, mtime_ns, size)
    except Exception as exc:
        st.error(str(exc))
    return None


def load_many_reports_widget(prefix: str, default_path: str = "") -> list[Report]:
    reports: list[Report] = []
    uploads = st.file_uploader(tr("upload_csv"), type=["csv", "CSV"], accept_multiple_files=True, key=f"{prefix}_uploads")
    if uploads:
        for upload in uploads:
            data = upload.getvalue()
            reports.append(make_ui_report(upload.name, load_uploaded_csv(upload.name, data), size=len(data)))
    with st.expander(tr("optional_path"), expanded=bool(default_path)):
        paths_text = st.text_area(tr("csv_path"), value=default_path, help=tr("path_help"), key=f"{prefix}_paths")
        live = st.checkbox(tr("live_reload"), value=False, key=f"{prefix}_live")
        if live:
            refresh = st.number_input(tr("refresh_seconds"), min_value=2, max_value=120, value=10, key=f"{prefix}_refresh")
            components.html(f"<meta http-equiv='refresh' content='{int(refresh)}'>", height=0)
        for raw_path in [line.strip() for line in paths_text.splitlines() if line.strip()]:
            try:
                p = Path(raw_path).expanduser()
                files = sorted(p.rglob("*.csv")) + sorted(p.rglob("*.CSV")) if p.is_dir() else [p]
                for file in files:
                    if file.exists():
                        df, mtime_ns, size = load_csv_path(str(file), live, load_csv_path_cached)
                        reports.append(make_ui_report(str(file), df, mtime_ns, size))
            except Exception as exc:
                st.error(f"{raw_path}: {exc}")
    return reports


def metric_row(values: list[tuple[str, Any, str | None]]) -> None:
    cols = st.columns(max(1, len(values)))
    for col, (label, value, suffix) in zip(cols, values):
        if isinstance(value, float):
            text = f"{value:,.1f}".replace(",", "X").replace(".", ",").replace("X", ".")
        else:
            text = str(value)
        col.metric(label, f"{text} {suffix}" if suffix else text)


def render_overview_group(report: Report, stats: pd.DataFrame, title: str, metrics: list[str], height: int = 260) -> None:
    metrics = [metric for metric in metrics if metric in report.numeric.columns]
    if not metrics:
        return
    st.markdown(f"#### {title}")
    visible_stats = stats[stats["Metric"].isin(metrics)]
    if not visible_stats.empty:
        st.dataframe(visible_stats, width="stretch", hide_index=True)
    render_chart(report, "Linha", "time", metrics[:6], height)


def render_report(report: Report) -> None:
    st.session_state["current_report_source"] = report.source
    st.session_state["current_report_context"] = report.context
    ctx = report.context
    st.subheader(ctx["title"])
    st.caption(repair_mojibake(report.source))
    repaired = int(report.df.attrs.get("csv_repaired_short_rows", 0)) + int(report.df.attrs.get("csv_repaired_long_rows", 0))
    if repaired:
        lines = report.df.attrs.get("csv_repaired_lines", [])
        st.warning(
            f"O CSV tinha {repaired} linha(s) com quantidade de campos diferente do cabe\u00e7alho. "
            f"Normalizei essas linhas para manter o relat\u00f3rio carreg\u00e1vel. Exemplos: {lines}"
        )
    metric_row(
        [
            (tr("samples"), len(report.df), None),
            (tr("sensors"), len(report.df.columns), None),
            (tr("category"), pretty_token(ctx["category"]), None),
            (tr("context"), pretty_token(ctx["workload"]), None),
        ]
    )
    stats = stats_frame(report.numeric)
    section = st.radio(
        "Secao do relatorio",
        [tr("overview"), tr("stats"), tr("charts"), tr("raw")],
        horizontal=True,
        label_visibility="collapsed",
        key="report_section",
    )
    if section == tr("overview"):
        render_overview_group(report, stats, tr("power"), power_metrics(list(report.numeric.columns))[:6])
        render_overview_group(report, stats, tr("temperatures"), temperature_metrics(list(report.numeric.columns))[:6])
        load_metrics = [
            col
            for col in [
                "GPU core load %",
                "CPU total %",
                "Physical memory load %",
                "Disk total activity %",
                "Carga da memória física [%]",
            ]
            if col in report.numeric.columns
        ]
        render_overview_group(report, stats, "Carga", load_metrics)
        render_overview_group(report, stats, tr("frames"), fps_metrics(list(report.numeric.columns))[:3], height=220)
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
            st.dataframe(limiter_df, width="stretch", hide_index=True)
    elif section == tr("stats"):
        categories = sorted({category_for_metric(col) for col in report.numeric.columns})
        chosen_category = st.selectbox(tr("category"), ["Todos"] + categories)
        visible = stats if chosen_category == "Todos" else stats[stats["Metric"].map(category_for_metric) == chosen_category]
        st.dataframe(visible, width="stretch", hide_index=True)
        st.download_button(
            tr("download"),
            stats.to_csv(index=False).encode("utf-8"),
            file_name=f"{slugify(ctx['title'])}-stats.csv",
            mime="text/csv",
            key="stats_download",
        )
    elif section == tr("charts"):
        groups = {
            "Pot\u00eancia": [c for c in report.numeric.columns if " W" in c or "power" in c.lower()],
            "Temperatura": [c for c in report.numeric.columns if "temp" in c.lower() or " C" in c or " F" in c],
            "Carga": [c for c in report.numeric.columns if "%" in c or "load" in c.lower()],
            "Clocks": [c for c in report.numeric.columns if "clock" in c.lower() or "mhz" in c.lower()],
            "Tudo": list(report.numeric.columns),
        }
        group = st.selectbox(tr("category"), [name for name, cols in groups.items() if cols] or ["Tudo"])
        default = groups.get(group, list(report.numeric.columns))[:5]
        query = st.text_input("Buscar metricas", key="report_metric_search")
        options = metric_options_for_query(list(report.numeric.columns), query)
        default = [col for col in default if col in options] or options[:5]
        cols = st.multiselect(tr("metric_picker"), options, default=default, format_func=metric_label)
        chart_type = st.selectbox(tr("chart_type"), ["Linha", "\u00c1rea", "Dispers\u00e3o", "Barras", "Heatmap", "Tabela"])
        render_chart(report, chart_type, "time", cols)
    elif section == tr("raw"):
        st.dataframe(report.df, width="stretch")
        st.download_button(
            tr("download"),
            report.df.to_csv(index=False).encode("utf-8"),
            file_name=f"{slugify(ctx['title'])}-raw.csv",
            mime="text/csv",
            key="raw_download",
        )


def render_browser_save_button(file_name: str, content: str, label: str, help_text: str) -> None:
    payload = json.dumps({"fileName": file_name, "content": content, "label": label, "help": help_text})
    components.html(
        f"""
        <div style="font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;">
          <button id="save-file" style="
            border: 1px solid rgba(49, 51, 63, 0.2);
            border-radius: 0.5rem;
            padding: 0.45rem 0.8rem;
            background: rgb(255, 255, 255);
            color: rgb(49, 51, 63);
            cursor: pointer;
            font-size: 0.92rem;
          "></button>
          <div id="save-status" style="margin-top: 0.35rem; color: #6b7280; font-size: 0.82rem;"></div>
        </div>
        <script>
          const data = {payload};
          const button = document.getElementById("save-file");
          const status = document.getElementById("save-status");
          button.textContent = data.label;
          status.textContent = data.help;
          button.addEventListener("click", async () => {{
            try {{
              if (!window.showDirectoryPicker) {{
                status.textContent = "File System Access API unavailable here. Use the download button.";
                return;
              }}
              const dir = await window.showDirectoryPicker({{ mode: "readwrite" }});
              const handle = await dir.getFileHandle(data.fileName, {{ create: true }});
              const writable = await handle.createWritable();
              await writable.write(data.content);
              await writable.close();
              status.textContent = `Saved: ${{data.fileName}}`;
            }} catch (error) {{
              status.textContent = error && error.name === "AbortError" ? "Canceled." : `Could not save: ${{error.message || error}}`;
            }}
          }});
        </script>
        """,
        height=92,
    )


def render_benchmarks() -> None:
    st.subheader(tr("benchmarks"))
    default_scores = pd.DataFrame(
        [
            {"Metric": "GPU", "Value": "18089", "Unit": "pts"},
            {"Metric": "CPU multi", "Value": "3727", "Unit": "pts"},
            {"Metric": "CPU single core", "Value": "563", "Unit": "pts"},
            {"Metric": "CPU single thread", "Value": "420", "Unit": "pts"},
            {"Metric": "MP ratio", "Value": "8,86", "Unit": "x"},
        ]
    )
    with st.form("benchmark_form"):
        name = st.text_input(tr("benchmark_name"), value="Cinebench 2026")
        performance_mode = st.text_input(tr("performance_mode"), value="Balanced")
        scenario = st.text_input(tr("scenario"), value=st.session_state.get("current_report_context", {}).get("title", "geral"))
        scores = st.data_editor(
            default_scores,
            num_rows="dynamic",
            width="stretch",
            column_config={"Metric": st.column_config.TextColumn(required=True), "Value": st.column_config.TextColumn()},
        )
        use_current = st.checkbox(tr("current_report"), value=bool(st.session_state.get("current_report_source")))
        submitted = st.form_submit_button(tr("save"))
    linked = None
    if use_current and st.session_state.get("current_report_source"):
        linked = {"source": st.session_state["current_report_source"], "context": st.session_state.get("current_report_context", {})}
    payload = benchmark_payload(name, performance_mode, scenario, scores, linked)
    content = json.dumps(payload, ensure_ascii=False, indent=2)
    encoded = content.encode("utf-8")
    file_name = f"{slugify(payload['benchmark'])}-{slugify(payload['scenario'])}-{datetime.now():%Y%m%d-%H%M%S}.telemetry-benchmark.json"
    if submitted:
        st.success("Registro pronto. Salve pelo navegador abaixo.")
    left, right = st.columns([1, 1])
    with left:
        st.download_button(tr("download"), encoded, file_name=file_name, mime="application/json")
    with right:
        if submitted:
            render_browser_save_button(file_name, content, tr("browser_save"), tr("browser_save_help"))
        else:
            st.caption(tr("browser_save_help"))

    st.divider()
    st.subheader(tr("loaded_files"))
    uploaded = st.file_uploader(
        "JSON",
        type=["json"],
        accept_multiple_files=True,
        key="benchmark_json_upload",
    )
    if uploaded:
        for file in uploaded:
            try:
                data = json.loads(file.getvalue().decode("utf-8"))
                with st.expander(file.name, expanded=len(uploaded) == 1):
                    st.json(data)
                    scores_df = pd.DataFrame(data.get("scores", []))
                    if not scores_df.empty:
                        st.dataframe(scores_df, width="stretch", hide_index=True)
                    linked_report = data.get("linked_report")
                    if linked_report:
                        st.caption(f"{tr('linked_report')}: {linked_report.get('source', '')}")
            except Exception as exc:
                st.error(f"{file.name}: {exc}")


def render_compare() -> None:
    left, right = st.columns(2)
    with left:
        st.markdown("### A")
        a = load_report_widget("compare_a", "")
    with right:
        st.markdown("### B")
        b = load_report_widget("compare_b", "")
    if not a or not b:
        return
    a = display_report(a)
    b = display_report(b)
    common = sorted(set(a.numeric.columns) & set(b.numeric.columns))
    if not common:
        st.warning("N\u00e3o h\u00e1 m\u00e9tricas num\u00e9ricas em comum.")
        return
    query = st.text_input("Buscar metricas", key="compare_metric_search")
    options = metric_options_for_query(common, query)
    metrics = st.multiselect(tr("metric_picker"), options, default=options[: min(5, len(options))], format_func=metric_label)
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
    st.dataframe(comp, width="stretch", hide_index=True)
    chart = (
        alt.Chart(comp)
        .mark_bar()
        .encode(x="Metric:N", y="Avg:Q", color="Report:N", column="Context:N", tooltip=list(comp.columns))
        .properties(height=320)
    )
    st.altair_chart(chart, width="stretch")


def render_custom_chart(report: Report | None) -> None:
    if not report:
        st.info(tr("no_report"))
        return
    cols = list(report.numeric.columns)
    if not cols:
        st.warning("Nenhuma m\u00e9trica num\u00e9rica foi detectada.")
        return
    chart_type = st.selectbox(tr("chart_type"), ["Linha", "\u00c1rea", "Dispers\u00e3o", "Barras", "Heatmap", "Tabela"], key="custom_type")
    x_options = ["time"] + cols
    x_axis = st.selectbox(tr("x_axis"), x_options, key="custom_x")
    query = st.text_input("Buscar metricas", key="custom_metric_search")
    options = metric_options_for_query(cols, query)
    y_axis = st.multiselect(tr("y_axis"), options, default=options[: min(3, len(options))], key="custom_y", format_func=metric_label)
    render_chart(report, chart_type, x_axis, y_axis, height=460)


def main() -> None:
    st.set_page_config(page_title="Telemetry Lab", layout="wide")
    with st.sidebar:
        lang_label = st.selectbox("Idioma / Language", ["pt", "en"], format_func=lambda item: "Portugu\u00eas" if item == "pt" else "English")
        st.session_state["lang"] = lang_label
        st.selectbox(
            tr("temperature_unit"),
            ["C", "F"],
            index=0,
            format_func=lambda item: tr("celsius") if item == "C" else tr("fahrenheit"),
            key="temperature_unit",
        )
        st.header(tr("input"))
        report = load_report_widget("main", default_report_path())
        st.caption(f"Telemetry Lab {APP_VERSION}")
    st.title(tr("page"))
    st.caption(tr("tagline"))

    view = st.radio(
        "Navegacao principal",
        [tr("report"), tr("power"), tr("temperatures"), tr("frames"), tr("compare"), tr("benchmarks"), tr("custom_chart"), tr("glossary")],
        horizontal=True,
        label_visibility="collapsed",
        key="main_view",
    )

    if view == tr("report"):
        if report:
            render_report(display_report(report))
        else:
            st.info(tr("no_report"))
    elif view == tr("power"):
        st.subheader(tr("power"))
        reports = load_many_reports_widget("power_reports", "")
        if not reports and report:
            reports = [report]
        render_power_view([display_report(item) for item in reports])
    elif view == tr("temperatures"):
        st.subheader(tr("temperatures"))
        reports = load_many_reports_widget("temp_reports", "")
        if not reports and report:
            reports = [report]
        render_temperature_view([display_report(item) for item in reports])
    elif view == tr("frames"):
        st.subheader(tr("frames"))
        reports = load_many_reports_widget("fps_reports", "")
        if not reports and report:
            reports = [report]
        render_fps_view([display_report(item) for item in reports])
    elif view == tr("compare"):
        render_compare()
    elif view == tr("benchmarks"):
        render_benchmarks()
    elif view == tr("custom_chart"):
        render_custom_chart(display_report(report) if report else None)
    elif view == tr("glossary"):
        render_glossary_view(display_report(report) if report else None)
