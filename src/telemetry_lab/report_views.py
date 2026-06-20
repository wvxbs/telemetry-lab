# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

import altair as alt
import pandas as pd
import streamlit as st

from telemetry_lab.analysis import stats_frame
from telemetry_lab.metrics import (
    battery_metrics,
    component_metrics,
    cpu_cluster_frame,
    curated_gaming_metrics,
    curated_power_metrics,
    curated_temperature_metrics,
    estimated_system_power,
    fps_metrics,
    glossary_frame,
    is_cpu_load_metric,
    is_cpu_metric,
    display_metric_value,
    memory_technology,
    metric_group,
    metric_label,
    metric_unit,
    storage_device_label,
    redundancy_frame,
    is_fps_metric,
    is_gpu_load_metric,
    is_gpu_metric,
    is_memory_temperature_metric,
    is_power_metric,
    is_ram_metric,
    is_temperature_metric,
    is_vram_metric,
    rank_cpu_metric,
    rank_gaming_metric,
    rank_gpu_power_metric,
    rank_gpu_temperature_metric,
    rank_vram_metric,
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
        for metric in metrics:
            data[metric] = data[metric].map(lambda value, metric=metric: display_metric_value(metric, value))
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
            clean = report.numeric[metric].dropna().map(lambda value, metric=metric: display_metric_value(metric, value))
            if clean.empty:
                continue
            rows.append(
                {
                    "Report": label,
                    "Metric": metric,
                    "Avg": clean.mean(),
                    "1%": clean.quantile(0.01),
                    "0.1%": clean.quantile(0.001),
                    "P95": clean.quantile(0.95),
                    "Max": clean.max(),
                    "Last": clean.iloc[-1],
                    "Samples": int(clean.count()),
                }
            )
    return pd.DataFrame(rows)


def render_metric_chart(data: pd.DataFrame, height: int = 380) -> None:
    if data.empty:
        st.info("Nenhuma metrica compativel foi detectada.")
        return
    x_type = "time:T" if pd.api.types.is_datetime64_any_dtype(data["time"]) else "time:Q"
    units = sorted({metric_unit(metric) for metric in data["Metric"].dropna().unique()})
    y_title = "Valor" if len(units) != 1 or units[0] == "-" else f"Valor ({units[0]})"
    chart = (
        alt.Chart(data)
        .mark_line()
        .encode(
            x=alt.X(x_type, title="Tempo" if x_type == "time:T" else "Amostra"),
            y=alt.Y("Value:Q", title=y_title),
            color="Metric:N",
            strokeDash="Report:N",
            tooltip=["Report", "Metric", "time", alt.Tooltip("Value:Q", format=".2f")],
        )
        .properties(height=height)
        .interactive()
    )
    st.altair_chart(chart, width="stretch")


def _clean_stats(series: pd.Series) -> dict[str, float | int]:
    clean = series.dropna()
    if clean.empty:
        return {"samples": 0}
    return {
        "avg": float(clean.mean()),
        "p1": float(clean.quantile(0.01)),
        "p01": float(clean.quantile(0.001)),
        "p95": float(clean.quantile(0.95)),
        "min": float(clean.min()),
        "max": float(clean.max()),
        "last": float(clean.iloc[-1]),
        "samples": int(clean.count()),
    }


def _format_value(value: float | int | None, unit: str) -> str:
    if value is None:
        return "-"
    formatted = f"{float(value):,.0f}" if abs(float(value)) >= 100 else f"{float(value):,.1f}"
    formatted = formatted.replace(",", "X").replace(".", ",").replace("X", ".")
    return formatted if unit == "-" else f"{formatted} {unit}"


def _best_metric(report: Report, predicate, ranker=None) -> str | None:
    ranker = ranker or (lambda name: 0)
    candidates = [col for col in report.numeric.columns if predicate(col)]
    candidates = sorted(candidates, key=lambda col: (ranker(col), col.casefold()))
    return candidates[0] if candidates else None


def _highlight_value_key(report: Report, metric: str) -> str:
    if report.live_reload:
        return "last"
    if is_temperature_metric(metric):
        return "max"
    return "avg"


def _comparison_for_card(report: Report, metric: str, stats: dict[str, float | int], unit: str) -> str | None:
    if not stats.get("samples", 0):
        return None
    if report.live_reload:
        return f"Média: {_format_value(stats.get('avg'), unit)}"
    if is_temperature_metric(metric):
        return f"Média: {_format_value(stats.get('avg'), unit)}"
    if is_fps_metric(metric):
        return f"1%: {_format_value(stats.get('p1'), unit)}"
    return f"Max: {_format_value(stats.get('max'), unit)}"


def _highlight_card(label: str, report: Report, metric: str | None, value_key: str | None = None) -> None:
    if not metric or metric not in report.numeric.columns:
        st.metric(label, "-")
        return
    stats = _clean_stats(report.numeric[metric])
    unit = metric_unit(metric)
    selected_key = value_key or _highlight_value_key(report, metric)
    st.metric(label, _format_value(stats.get(selected_key), unit), delta=_comparison_for_card(report, metric, stats, unit), help=metric)


def _filtered_fps_series(series: pd.Series, min_fps: float, max_fps: float) -> pd.Series:
    clean = series.dropna()
    return clean[(clean >= min_fps) & (clean <= max_fps)]


def render_gaming_view(reports: list[Report]) -> None:
    if not reports:
        st.info("Carregue ao menos um relatorio.")
        return
    left, right = st.columns(2)
    min_fps = left.number_input("FPS minimo valido", min_value=0.0, value=30.0, step=5.0, key="gaming_min_fps")
    max_fps = right.number_input("FPS maximo valido", min_value=1.0, value=1000.0, step=10.0, key="gaming_max_fps")

    for idx, report in enumerate(reports, start=1):
        if len(reports) > 1:
            st.markdown(f"### {report_label(report, f'R{idx}')}")
        fps = _best_metric(report, is_fps_metric)
        gpu_power = _best_metric(report, lambda col: is_gpu_metric(col) and is_power_metric(col), rank_gpu_power_metric)
        cpu_power = _best_metric(report, lambda col: is_cpu_metric(col) and is_power_metric(col), rank_cpu_metric)
        gpu_temp = _best_metric(report, lambda col: is_gpu_metric(col) and is_temperature_metric(col), rank_gpu_temperature_metric)
        cpu_temp = _best_metric(report, lambda col: is_cpu_metric(col) and is_temperature_metric(col), rank_cpu_metric)
        gpu_load = _best_metric(report, is_gpu_load_metric, rank_gaming_metric)
        cpu_load = _best_metric(report, is_cpu_load_metric, rank_gaming_metric)
        vram = _best_metric(report, is_vram_metric, rank_vram_metric)
        ram = _best_metric(report, is_ram_metric, rank_gaming_metric)
        memory_temp = _best_metric(report, is_memory_temperature_metric, rank_gaming_metric)

        cards = st.columns(4)
        if fps and fps in report.numeric.columns:
            filtered = _filtered_fps_series(report.numeric[fps], min_fps, max_fps)
            fps_stats = _clean_stats(filtered)
            if report.live_reload:
                cards[0].metric("FPS atual", _format_value(_clean_stats(report.numeric[fps]).get("last"), "FPS"), help=fps)
                cards[1].metric("FPS medio", _format_value(fps_stats.get("avg"), "FPS"), delta=f"Último: {_format_value(fps_stats.get('last'), 'FPS')}", help=f"{fps} filtrado")
            else:
                cards[0].metric("FPS medio", _format_value(fps_stats.get("avg"), "FPS"), delta=f"1%: {_format_value(fps_stats.get('p1'), 'FPS')}", help=f"{fps} filtrado")
                cards[1].metric("FPS máximo", _format_value(fps_stats.get("max"), "FPS"), delta=f"Média: {_format_value(fps_stats.get('avg'), 'FPS')}", help=f"{fps} filtrado")
            cards[2].metric("1% low", _format_value(fps_stats.get("p1"), "FPS"), help=f"{fps} filtrado")
            cards[3].metric("0.1% low", _format_value(fps_stats.get("p01"), "FPS"), help=f"{fps} filtrado")
        else:
            fps_labels = ["FPS atual", "FPS medio", "1% low", "0.1% low"] if any(item.live_reload for item in reports) else ["FPS medio", "FPS máximo", "1% low", "0.1% low"]
            for col, label in zip(cards, fps_labels):
                col.metric(label, "-")

        cards = st.columns(6)
        with cards[0]:
            _highlight_card("Potencia GPU", report, gpu_power)
        with cards[1]:
            _highlight_card("Potencia CPU", report, cpu_power)
        with cards[2]:
            _highlight_card("Temp. GPU", report, gpu_temp)
        with cards[3]:
            _highlight_card("Temp. CPU", report, cpu_temp)
        with cards[4]:
            _highlight_card("Uso GPU", report, gpu_load)
        with cards[5]:
            _highlight_card("Uso CPU", report, cpu_load)

        cards = st.columns(4)
        with cards[0]:
            _highlight_card("VRAM dedicada/alocada", report, vram)
        with cards[1]:
            _highlight_card("RAM", report, ram)
        with cards[2]:
            _highlight_card("Temp. memoria", report, memory_temp)
        with cards[3]:
            samples = len(report.df)
            st.metric("Amostras", f"{samples:,}".replace(",", "."))

        metrics = curated_gaming_metrics(list(report.numeric.columns), include_extra=True)
        visible = metric_summary([report], {report.source: metrics})
        if not visible.empty:
            st.dataframe(visible, width="stretch", hide_index=True)
        chart_metrics = [metric for metric in [fps, gpu_power, cpu_power, gpu_temp, cpu_temp, gpu_load, cpu_load, vram, ram] if metric]
        chart_metrics.extend([metric for metric in metrics if metric not in chart_metrics][: max(0, 8 - len(chart_metrics))])
        render_metric_chart(long_metric_frame([report], {report.source: chart_metrics}), height=320)

        if fps and fps in report.numeric.columns:
            key_cols = [col for col in metrics if col != fps and col in report.numeric.columns][:32]
            base = report.numeric[[fps] + key_cols].copy()
            base = base[(base[fps] >= min_fps) & (base[fps] <= max_fps)]
            corr = base.corr(numeric_only=True)[fps].drop(labels=[fps], errors="ignore").dropna()
            if not corr.empty:
                st.markdown("#### Correlacao com FPS")
                corr_df = corr.abs().sort_values(ascending=False).head(12).rename("Abs correlation").reset_index()
                corr_df = corr_df.rename(columns={"index": "Metric"})
                st.dataframe(corr_df, width="stretch", hide_index=True)


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


def render_component_view(reports: list[Report], component: str, title: str) -> None:
    if not reports:
        st.info("Carregue ao menos um relatorio.")
        return
    include_extra = st.checkbox("Mostrar sensores extras", value=True, key=f"{component}_extra_sensors")
    metrics_by_report = {
        report.source: component_metrics(list(report.numeric.columns), component, include_extra=include_extra) for report in reports
    }
    if component == "cpu":
        for report in reports:
            clusters = cpu_cluster_frame(report.numeric)
            if not clusters.empty:
                st.markdown("#### Clusters de CPU")
                st.dataframe(clusters, width="stretch", hide_index=True)
    elif component == "memory":
        st.caption(f"Tecnologia inferida: {memory_technology(list(reports[0].numeric.columns))}. Clocks de memória são exibidos como MT/s efetivos.")
    elif component == "storage":
        devices = sorted({storage_device_label(metric) for report in reports for metric in component_metrics(list(report.numeric.columns), component, include_extra=True)})
        st.caption(f"Dispositivos/grupos detectados: {', '.join(devices) if devices else '-'}")
    else:
        st.caption("Sensores priorizados para leitura rapida aparecem primeiro; a tabela preserva os demais dados relevantes do HWiNFO.")
    for idx, report in enumerate(reports, start=1):
        label = report_label(report, f"R{idx}")
        metrics = metrics_by_report.get(report.source, [])
        if len(reports) > 1:
            st.markdown(f"### {label}")
        cards = st.columns(4)
        for col, metric in zip(cards, metrics[:4]):
            with col:
                _highlight_card(metric, report, metric)
    summary = metric_summary(reports, metrics_by_report)
    if summary.empty:
        st.info(f"Nenhum sensor de {title.lower()} foi detectado nesses relatorios.")
        return
    st.dataframe(summary, width="stretch", hide_index=True)
    chart_metrics = {source: metrics[:8] for source, metrics in metrics_by_report.items()}
    render_metric_chart(long_metric_frame(reports, chart_metrics), height=340)


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
