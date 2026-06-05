# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

import altair as alt
import pandas as pd
import streamlit as st

from telemetry_lab.analysis import stats_frame
from telemetry_lab.metrics import (
    battery_metrics,
    curated_power_metrics,
    curated_temperature_metrics,
    estimated_system_power,
    fps_metrics,
    glossary_frame,
    metric_group,
    metric_label,
    redundancy_frame,
)
from telemetry_lab.models import Report


def report_label(report: Report, fallback: str) -> str:
    title = str(report.context.get("title", "")).strip()
    return title or fallback


def long_metric_frame(reports: list[Report], metrics_by_report: dict[str, list[str]]) -> pd.DataFrame:
    frames = []
    for idx, report in enumerate(reports, start=1):
        label = report_label(report, f"R{idx}")
        metrics = [metric for metric in metrics_by_report.get(report.source, []) if metric in report.numeric.columns]
        if not metrics:
            continue
        data = report.numeric[metrics].copy()
        data.insert(0, "time", report.time.values)
        data.insert(1, "Report", label)
        frames.append(data.melt(id_vars=["time", "Report"], var_name="Metric", value_name="Value").dropna())
    if not frames:
        return pd.DataFrame(columns=["time", "Report", "Metric", "Value"])
    return pd.concat(frames, ignore_index=True)


def metric_summary(reports: list[Report], metrics_by_report: dict[str, list[str]]) -> pd.DataFrame:
    rows = []
    for idx, report in enumerate(reports, start=1):
        label = report_label(report, f"R{idx}")
        for metric in metrics_by_report.get(report.source, []):
            if metric not in report.numeric.columns:
                continue
            clean = report.numeric[metric].dropna()
            if clean.empty:
                continue
            rows.append(
                {
                    "Report": label,
                    "Metric": metric,
                    "Avg": clean.mean(),
                    "P95": clean.quantile(0.95),
                    "Max": clean.max(),
                    "Samples": int(clean.count()),
                }
            )
    return pd.DataFrame(rows)


def render_metric_chart(data: pd.DataFrame, height: int = 380) -> None:
    if data.empty:
        st.info("Nenhuma metrica compativel foi detectada.")
        return
    x_type = "time:T" if pd.api.types.is_datetime64_any_dtype(data["time"]) else "time:Q"
    chart = (
        alt.Chart(data)
        .mark_line()
        .encode(
            x=alt.X(x_type, title=""),
            y=alt.Y("Value:Q", title=""),
            color="Metric:N",
            strokeDash="Report:N",
            tooltip=["Report", "Metric", "time", alt.Tooltip("Value:Q", format=".2f")],
        )
        .properties(height=height)
        .interactive()
    )
    st.altair_chart(chart, width="stretch")


def render_power_view(reports: list[Report]) -> None:
    if not reports:
        st.info("Carregue ao menos um relatorio.")
        return
    include_extra = st.checkbox("Mostrar sensores extras de potencia", value=False, key="power_extra_sensors")
    metrics_by_report: dict[str, list[str]] = {}
    for report in reports:
        estimated = estimated_system_power(report.numeric)
        if estimated.notna().sum() > 0 and "System estimated W" not in report.numeric.columns:
            report.numeric["System estimated W"] = estimated
        cols = curated_power_metrics(list(report.numeric.columns), include_extra=include_extra)
        metrics_by_report[report.source] = cols

    data = long_metric_frame(reports, metrics_by_report)
    st.dataframe(metric_summary(reports, metrics_by_report), width="stretch", hide_index=True)
    render_metric_chart(data)

    battery_by_report = {report.source: battery_metrics(list(report.numeric.columns)) for report in reports}
    battery_data = long_metric_frame(reports, battery_by_report)
    if not battery_data.empty:
        st.subheader("Bateria")
        render_metric_chart(battery_data, height=260)
    else:
        st.caption("Nenhum sensor de bateria/descarga foi detectado nesses relatorios.")


def render_temperature_view(reports: list[Report]) -> None:
    if not reports:
        st.info("Carregue ao menos um relatorio.")
        return
    include_extra = st.checkbox("Mostrar sensores extras de temperatura", value=False, key="temperature_extra_sensors")
    metrics_by_report = {
        report.source: curated_temperature_metrics(list(report.numeric.columns), include_extra=include_extra) for report in reports
    }
    data = long_metric_frame(reports, metrics_by_report)
    st.dataframe(metric_summary(reports, metrics_by_report), width="stretch", hide_index=True)
    render_metric_chart(data)


def fps_stats(series: pd.Series, min_fps: float, max_fps: float) -> dict[str, float | int]:
    clean = series.dropna()
    clean = clean[(clean >= min_fps) & (clean <= max_fps)]
    if clean.empty:
        return {"Samples": 0}
    return {
        "Avg": clean.mean(),
        "1% low": clean.quantile(0.01),
        "0.1% low": clean.quantile(0.001),
        "Min": clean.min(),
        "Max": clean.max(),
        "Samples": int(clean.count()),
    }


def render_fps_view(reports: list[Report]) -> None:
    if not reports:
        st.info("Carregue ao menos um relatorio.")
        return
    left, right = st.columns(2)
    min_fps = left.number_input("FPS minimo valido", min_value=0.0, value=30.0, step=5.0)
    max_fps = right.number_input("FPS maximo valido", min_value=1.0, value=1000.0, step=10.0)

    metrics_by_report = {report.source: fps_metrics(list(report.numeric.columns)) for report in reports}
    rows = []
    for idx, report in enumerate(reports, start=1):
        label = report_label(report, f"R{idx}")
        for metric in metrics_by_report[report.source]:
            stats = fps_stats(report.numeric[metric], min_fps, max_fps)
            if stats.get("Samples", 0):
                rows.append({"Report": label, "Metric": metric, **stats})
    stats = pd.DataFrame(rows)
    if stats.empty:
        st.info("Nenhuma metrica de FPS foi detectada. O HWiNFO nem sempre registra FPS sem fonte externa como RTSS/PresentMon.")
        return
    st.dataframe(stats, width="stretch", hide_index=True)

    data = long_metric_frame(reports, metrics_by_report)
    data = data[(data["Value"] >= min_fps) & (data["Value"] <= max_fps)]
    render_metric_chart(data)

    st.subheader("Correlacao")
    for idx, report in enumerate(reports, start=1):
        fps_cols = metrics_by_report[report.source]
        if not fps_cols:
            continue
        key_cols = [
            col
            for col in report.numeric.columns
            if metric_group(col) in {"Potencia", "Temperatura", "CPU", "GPU", "Memoria"} and col not in fps_cols
        ][:24]
        if not key_cols:
            continue
        fps_col = st.selectbox(f"FPS base {report_label(report, f'R{idx}')}", fps_cols, format_func=metric_label, key=f"fps_corr_{idx}")
        base = report.numeric[[fps_col] + key_cols].copy()
        base = base[(base[fps_col] >= min_fps) & (base[fps_col] <= max_fps)]
        corr = base.corr(numeric_only=True)[fps_col].drop(labels=[fps_col], errors="ignore").dropna()
        if not corr.empty:
            corr_df = corr.abs().sort_values(ascending=False).head(10).rename("Abs correlation").reset_index()
            corr_df = corr_df.rename(columns={"index": "Metric"})
            st.dataframe(corr_df, width="stretch", hide_index=True)


def render_glossary_view(report: Report | None) -> None:
    if not report:
        st.info("Carregue um relatorio para ver o glossario das colunas.")
        return
    numeric_glossary = glossary_frame(list(report.numeric.columns))
    redundancy = redundancy_frame(list(report.numeric.columns))
    raw_only = [col for col in report.df.columns if col not in set(report.numeric.columns)]
    raw_glossary = glossary_frame(raw_only)
    if not redundancy.empty:
        st.subheader("Possiveis redundancias")
        st.dataframe(redundancy, width="stretch", hide_index=True)
    st.subheader("Metricas numericas")
    st.dataframe(numeric_glossary, width="stretch", hide_index=True)
    with st.expander("Colunas brutas do HWiNFO"):
        st.dataframe(raw_glossary, width="stretch", hide_index=True)
