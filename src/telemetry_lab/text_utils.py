# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

import re
import unicodedata


def slugify(value: str) -> str:
    text = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode("ascii")
    text = re.sub(r"[^a-zA-Z0-9]+", "-", text.lower()).strip("-")
    return text or "telemetry"


def pretty_token(value: str) -> str:
    value = re.sub(r"[_-]+", " ", value).strip()
    return value.title() if value else value


def category_for_metric(name: str) -> str:
    low = name.lower()
    if "gpu" in low:
        return "GPU"
    if "cpu" in low or "core" in low:
        return "CPU"
    if "mem" in low or "ram" in low:
        return "Mem\u00f3ria"
    if "disk" in low or "ssd" in low or "drive" in low:
        return "Disco"
    if "temp" in low or " c" in low:
        return "Temperatura"
    if "w" in low or "power" in low:
        return "Pot\u00eancia"
    return "Outros"
