# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

import altair as alt
import pandas as pd
import streamlit as st

from telemetry_lab.models import Report


def chart_source(report: Report, columns: list[str]) -> pd.DataFrame:
    data = report.numeric[columns].copy()
    data.insert(0, "time", report.time.values)
    return data


def render_chart(report: Report, chart_type: str, x_axis: str, y_axis: list[str], height: int = 360) -> None:
    if not y_axis:
        st.info("Selecione ao menos uma m\u00e9trica.")
        return
    data = chart_source(report, y_axis)
    if x_axis != "time" and x_axis in report.numeric.columns:
        data[x_axis] = report.numeric[x_axis]
    if chart_type == "Tabela":
        st.dataframe(data, width="stretch")
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
        st.altair_chart(chart, width="stretch")
        return
    long = data.melt(id_vars=[x_axis], value_vars=y_axis, var_name="Metric", value_name="Value").dropna()
    base = alt.Chart(long).encode(
        x=alt.X(f"{x_axis}:T" if x_axis == "time" and pd.api.types.is_datetime64_any_dtype(long[x_axis]) else f"{x_axis}:Q"),
        y=alt.Y("Value:Q"),
        color="Metric:N",
        tooltip=[x_axis, "Metric", alt.Tooltip("Value:Q", format=".2f")],
    )
    if chart_type == "\u00c1rea":
        chart = base.mark_area(opacity=0.45)
    elif chart_type == "Dispers\u00e3o":
        chart = base.mark_circle(size=36, opacity=0.65)
    elif chart_type == "Barras":
        chart = base.mark_bar(opacity=0.75)
    else:
        chart = base.mark_line()
    st.altair_chart(chart.properties(height=height).interactive(), width="stretch")
