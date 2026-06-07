# Third Party Notices

Telemetry Lab is licensed as GPL-3.0-or-later. The application also depends on third-party packages that keep their own licenses.

Direct runtime dependencies declared in `requirements.txt`:

- Streamlit: Apache-2.0
- pandas: BSD-3-Clause
- Vega-Altair / Altair: BSD-3-Clause

The Docker image is based on the official `python:3.12-slim` image and includes Python, Debian system packages, Python package dependencies, and their transitive dependencies under their respective licenses.

Python packages installed by `pip` preserve their package metadata, license classifiers, and bundled license files under `/usr/local/lib/python3.12/site-packages`. Debian package license files are normally available under `/usr/share/doc`.

For release-grade compliance, generate and publish a Software Bill of Materials (SBOM) for each image release, and keep this notice updated whenever dependencies change.
