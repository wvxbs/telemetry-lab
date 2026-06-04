# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

from dataclasses import dataclass
from typing import TYPE_CHECKING, Any

if TYPE_CHECKING:
    import pandas as pd


@dataclass
class Report:
    source: str
    df: "pd.DataFrame"
    time: "pd.Series"
    numeric: "pd.DataFrame"
    context: dict[str, Any]
    mtime_ns: int | None = None
    size: int | None = None
