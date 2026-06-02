# Telemetry Lab

Streamlit dashboard for HWiNFO CSV logs, benchmark score records, report comparison, and custom charts.

The app is in Portuguese by default and includes an English switch in the sidebar.

## What It Does

- Opens HWiNFO CSV files from a server/container path or from browser upload.
- Infers report context from file and folder names, such as `benchmarks/games/valorant/report.csv`.
- Falls back to a general report context when the path does not carry useful metadata.
- Keeps rich known metrics for the original Cinebench 2026 logs while also exposing generic numeric sensors from any HWiNFO CSV.
- Registers benchmark scores with arbitrary benchmark names, metric names, values, and units.
- Saves benchmark records as JSON and can read those files back.
- Links a benchmark record to the currently loaded telemetry report.
- Compares two telemetry CSV reports.
- Provides standard charts plus a custom chart generator with line, area, scatter, bar, heatmap, and table modes.
- Offers opt-in live reload for CSV files that are still being written.

## Docker

Build the image using the project naming convention:

```bash
cd /home/wvxbs/Documentos/tools/telemetry-lab
docker build -t wvxbs/telemetry-lab:latest .
```

Run detached with Docker Compose:

```bash
cd /home/wvxbs/Documentos/tools/telemetry-lab
mkdir -p /mnt/c/Users/gabri/OneDrive/Documents/tools/telemetry-lab-data/benchmarks
docker compose up -d --build
```

Open the dashboard at <http://localhost:8501>.

The Compose file mounts:

```text
/mnt/c/Users/gabri/OneDrive/Documents/tools -> /data/reports (read-only)
/mnt/c/Users/gabri/OneDrive/Documents/tools/telemetry-lab-data/benchmarks -> /data/benchmarks (read-write)
```

Use `/data/reports` when selecting CSV files from inside the container. Benchmark JSON files can be saved to `/data/benchmarks`.

## Detached Docker Run

Without Compose:

```bash
mkdir -p /mnt/c/Users/gabri/OneDrive/Documents/tools/telemetry-lab-data/benchmarks

docker run -d \
  --name telemetry-lab \
  --restart unless-stopped \
  -p 8501:8501 \
  -v "/mnt/c/Users/gabri/OneDrive/Documents/tools:/data/reports:ro" \
  -v "/mnt/c/Users/gabri/OneDrive/Documents/tools/telemetry-lab-data/benchmarks:/data/benchmarks:rw" \
  wvxbs/telemetry-lab:latest
```

If you need the container to read or save files in another host folder, mount that folder explicitly with another `-v` entry. Browser uploads and download buttons work without extra mounts, but server-side saving only works in folders the container can access.

## Local Development

```bash
cd /home/wvxbs/Documentos/tools/telemetry-lab
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
streamlit run app.py
```

When running directly on the machine, server-side save paths are normal local paths available to your user.

## Benchmark Records

Benchmark records are saved as `*.telemetry-benchmark.json` files. Each file stores:

- benchmark name;
- scenario/context;
- arbitrary score rows with metric, value, and unit;
- optional link to the loaded HWiNFO CSV report and its inferred context.

Use the Benchmarks tab to create, download, save, read, and inspect those records.
