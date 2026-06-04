# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

import csv
from io import StringIO
from pathlib import Path

import pandas as pd

from telemetry_lab.text_utils import repair_mojibake


def dedupe_columns(columns: list[str]) -> list[str]:
    seen: dict[str, int] = {}
    result: list[str] = []
    for i, name in enumerate(columns):
        if i == 0:
            name = name.lstrip("\ufeff").removeprefix("Ã¯Â»Â¿")
        name = repair_mojibake(name)
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


def parse_hwinfo_csv_bytes(data: bytes) -> pd.DataFrame:
    columns, encoding = decode_csv_bytes(data[:262_144])
    text = data.decode(encoding, errors="replace")
    reader = csv.reader(StringIO(text))
    next(reader, None)
    width = len(columns)
    rows: list[list[str]] = []
    repaired_short = 0
    repaired_long = 0
    repaired_lines: list[int] = []
    for line_number, row in enumerate(reader, start=2):
        if not row:
            continue
        if len(row) < width:
            repaired_short += 1
            repaired_lines.append(line_number)
            row = row + [""] * (width - len(row))
        elif len(row) > width:
            repaired_long += 1
            repaired_lines.append(line_number)
            row = row[: width - 1] + [",".join(row[width - 1 :])]
        rows.append(row)
    df = pd.DataFrame(rows, columns=columns)
    df.attrs["csv_repaired_short_rows"] = repaired_short
    df.attrs["csv_repaired_long_rows"] = repaired_long
    df.attrs["csv_repaired_lines"] = repaired_lines[:10]
    df.attrs["csv_encoding"] = encoding
    return df


def load_csv_path(path: str, live_reload: bool, cache_reader) -> tuple[pd.DataFrame, int, int]:
    p = Path(path).expanduser()
    stat = p.stat()
    if live_reload:
        df = parse_hwinfo_csv_bytes(p.read_bytes())
    else:
        df = cache_reader(str(p), stat.st_mtime_ns, stat.st_size)
    return df, stat.st_mtime_ns, stat.st_size


def load_uploaded_csv(name: str, data: bytes) -> pd.DataFrame:
    del name
    return parse_hwinfo_csv_bytes(data)
