# Telemetry Lab

Streamlit dashboard for the HWiNFO CSV reports generated during Cinebench 2026 tests.

## Run With Docker

Build the image using the project naming convention:

```bash
cd /home/wvxbs/Documentos/tools/telemetry-lab
docker build -t wvxbs/telemetry-lab:latest .
```

Run the app detached:

```bash
docker run -d \
  --name telemetry-lab \
  --restart unless-stopped \
  -p 8501:8501 \
  -v "/mnt/c/Users/gabri/OneDrive/Documents/tools/Desmerdíficar o windows/relatório de sensores/cinebench 2026:/data/cinebench-2026:ro" \
  wvxbs/telemetry-lab:latest
```

Open the dashboard at <http://localhost:8501>.

The container mounts the default Windows report folder at:

```text
/data/cinebench-2026
```

When the mounted folder exists, Docker defaults the sidebar field `Pasta dos CSVs` to `/data/cinebench-2026`. Choose a CSV and adjust score values as needed.

## Run With Docker Compose

Start the app detached:

```bash
cd /home/wvxbs/Documentos/tools/telemetry-lab
docker compose up -d --build
```

Follow logs:

```bash
docker compose logs -f
```

Stop the app:

```bash
docker compose down
```

## Local Development

```bash
cd /home/wvxbs/Documentos/tools/telemetry-lab
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
streamlit run app.py
```

The app defaults to the mounted Windows report folder when it is available:

```text
/mnt/c/Users/gabri/OneDrive/Documents/tools/Desmerdíficar o windows/relatório de sensores/cinebench 2026
```
