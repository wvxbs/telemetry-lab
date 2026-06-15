// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel Ferreira
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

const string resourceName = "payload.zip";
const string appExe = "TelemetryLab.WinUI.exe";

try
{
    using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException("Telemetry Lab payload was not embedded in this executable.");

    var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Telemetry Lab", "WinUI");
    var appDir = Path.Combine(root, "app");
    Directory.CreateDirectory(root);

    var hash = Sha256(payload);
    payload.Position = 0;
    var marker = Path.Combine(root, "payload.sha256");
    if (!File.Exists(Path.Combine(appDir, appExe)) || !File.Exists(marker) || File.ReadAllText(marker) != hash)
    {
        var staging = Path.Combine(root, "staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        ZipFile.ExtractToDirectory(payload, staging, overwriteFiles: true);
        if (Directory.Exists(appDir))
        {
            Directory.Delete(appDir, recursive: true);
        }
        Directory.Move(staging, appDir);
        File.WriteAllText(marker, hash, Encoding.ASCII);
    }

    Process.Start(new ProcessStartInfo
    {
        FileName = Path.Combine(appDir, appExe),
        WorkingDirectory = appDir,
        UseShellExecute = true
    });
}
catch (Exception ex)
{
    var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Telemetry Lab");
    Directory.CreateDirectory(logDir);
    var logPath = Path.Combine(logDir, "launcher-error.log");
    File.WriteAllText(logPath, ex.ToString(), Encoding.UTF8);
    MessageBox(IntPtr.Zero, $"Telemetry Lab could not start.\n\n{ex.Message}\n\nDetails: {logPath}", "Telemetry Lab", 0x10);
}

static string Sha256(Stream stream)
{
    using var sha = SHA256.Create();
    return Convert.ToHexString(sha.ComputeHash(stream));
}

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
