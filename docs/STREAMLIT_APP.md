# Telemetry Lab Streamlit App

This document explains how to run, use, deploy, and develop the Streamlit/Docker version of Telemetry Lab.

## Why Use The Streamlit App

Use the Streamlit app when you want:

- a browser-based dashboard at `localhost:8501`;
- simple CSV upload through the browser;
- browser-managed downloads for benchmark records;
- optional Docker deployment;
- an interface that works from another device on the same network when the host exposes the port;
- a clear separation between the Streamlit frontend and reusable telemetry logic under `src/telemetry_lab`.

Use the native Windows app branch when you specifically want a Windows 11-style local shell, native file pickers, and a desktop-app feel.

## Quick Start With Docker Compose

Build and start the app:

```bash
docker compose up -d --build
```

Open:

```text
http://localhost:8501
```

Stop it:

```bash
docker compose down
```

This is the easiest repeatable way to run the web dashboard.

## Quick Start With Docker Run

Build the image:

```bash
docker build -t wvxbs/telemetry-lab:latest .
```

Run detached:

```bash
docker run -d \
  --name telemetry-lab \
  --restart unless-stopped \
  -p 8501:8501 \
  wvxbs/telemetry-lab:latest
```

Open:

```text
http://localhost:8501
```

Stop and remove:

```bash
docker stop telemetry-lab
docker rm telemetry-lab
```

## Access From Another Device

Expose the container on the host, then open the dashboard from another device using the host machine IP:

```text
http://<host-ip>:8501
```

On Windows, the host IP is usually visible with:

```powershell
ipconfig
```

Make sure the firewall allows inbound access to port `8501`.

## Normal File Workflow

The browser is the normal file interface.

Use browser controls to:

- upload HWiNFO CSV files;
- download benchmark JSON records;
- upload existing benchmark JSON records;
- in Chrome/Edge, use the directory picker to save benchmark records directly to a chosen folder.

This keeps the container hands-off. The user chooses files in the browser, and the app reads the uploaded content.

## Typed Paths And Live Reload

Typed paths are advanced.

Use them when the Streamlit process can directly see the path:

- local Python development;
- Docker bind mounts;
- a path inside the container.

Live reload needs a readable path because the app must poll the CSV file while HWiNFO is still writing it.

Docker example with a mounted reports folder:

```bash
docker run -d \
  --name telemetry-lab \
  --restart unless-stopped \
  -p 8501:8501 \
  -e TELEMETRY_LAB_REPORT_DIR=/reports \
  -v "/path/to/your/hwinfo-reports:/reports:ro" \
  wvxbs/telemetry-lab:latest
```

Then use a path under:

```text
/reports
```

The browser upload flow does not require this mount.

## Useful File Naming

File naming is optional. The app should load any valid HWiNFO CSV.

For richer inferred context, use meaningful folders and structured names:

```text
Benchmark History/<machine>/Games/<game>/<performance-mode>-<fps>cap-<dd-mm-yyyy>-<hhmm>.CSV
```

Example:

```text
Benchmark History/Dell G15/Games/Cyberpunk 2077/gmode-120cap-05-06-2026-0311.CSV
```

The app can infer:

- machine;
- workload category;
- game/app name;
- performance mode;
- FPS cap;
- date/time.

These are only hints. They are not required to load a report.

## Main Views

The Streamlit dashboard includes:

- report overview;
- gaming-focused dashboard;
- power analysis;
- temperature analysis;
- frame/FPS analysis;
- custom charts;
- comparison between reports;
- benchmark records;
- glossary and sensor descriptions.

The custom chart flow is intentionally broad: it lets you pick metrics and chart types, while the focused views try to choose useful sensor groups automatically.

## Gaming View

The gaming view is the default high-signal surface for game logs.

It highlights:

- current FPS;
- average FPS after optional FPS filtering;
- 1% low and 0.1% low FPS;
- real GPU power, preferring the GPU energy/power sensor instead of connector, rail, or misleading fallback fields;
- GPU and CPU temperatures;
- GPU and CPU utilization;
- dedicated or allocated VRAM usage;
- RAM usage;
- memory-related temperatures when HWiNFO exposes them.

Use the FPS min/max filters to ignore menus, background caps, loading screens, or artificial frame limiters before judging gameplay stability.

The detailed table under the cards keeps the broader statistics available, including average, min, 1%, 0.1%, P95, max, last value, and sample count. The chart below the table keeps the focused metrics visible over time, and the FPS correlation table helps identify which system metrics moved together with frame-rate instability.

## Benchmark Records

Benchmark records are saved as:

```text
*.telemetry-benchmark.json
```

They can store:

- benchmark name;
- scenario/context;
- score rows;
- score units;
- optional telemetry CSV link/context.

Use the benchmark workflow when you want to compare runs beyond a single CSV session.

## Local Development

Create a virtual environment:

```bash
python -m venv .venv
```

Activate it on Linux/macOS:

```bash
source .venv/bin/activate
```

Activate it on Windows PowerShell:

```powershell
.\.venv\Scripts\Activate.ps1
```

Install dependencies:

```bash
pip install -r requirements.txt
```

Run:

```bash
streamlit run app.py
```

When running locally, typed paths are normal paths visible to your user.

## Architecture

The Streamlit app is the frontend layer.

It owns:

- widgets;
- navigation;
- session state;
- rendering;
- browser upload/download flows.

Reusable logic lives under:

```text
src/telemetry_lab
```

Important responsibilities:

- CSV loading and decoding;
- report building;
- unit conversion;
- sensor grouping and glossary logic;
- benchmark record models;
- chart data preparation;
- optional path browsing for mounted/local folders.

The goal is to keep Streamlit as a frontend consuming reusable telemetry services, not as the only place where the application logic exists.

## Docker Image Publishing

The Docker publishing workflow builds and pushes:

```text
wvxbs/telemetry-lab
```

The image name is intentionally fixed.

Configure Docker Hub credentials in GitHub Actions using repository/environment secrets expected by the workflow.

## Troubleshooting

If browser upload works but typed paths do not:

- the typed path is not visible to the app process;
- in Docker, mount the host folder into the container;
- use browser upload for normal files.

If live reload does not update:

- confirm the path points to the growing CSV;
- confirm HWiNFO is still writing;
- use manual reload to confirm the file is readable;
- avoid browser upload for live reload because uploaded files are snapshots.

If the dashboard cannot open from another device:

- confirm the container is running;
- confirm port `8501` is mapped;
- use the host IP, not `localhost`, from the other device;
- check firewall rules.

If CSV text has broken accents:

- use a fresh build with the encoding repair changes;
- confirm the CSV is exported by HWiNFO as UTF-8 or a compatible legacy encoding;
- report the exact column name if mojibake still appears.
