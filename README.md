# Cinebench / HWiNFO stats

Streamlit dashboard for the HWiNFO CSV reports generated during the Cinebench 2026 tests.

## Run

```bash
cd /home/wvxbs/Documentos/tools/estatistica
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
streamlit run app.py
```

The app defaults to the mounted Windows report folder:

```text
/mnt/c/Users/gabri/OneDrive/Documents/tools/Desmerdíficar o windows/relatório de sensores/cinebench 2026
```

Use the sidebar to choose another CSV or adjust the score values.
