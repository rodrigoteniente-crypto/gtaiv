"""Build a reviewable release; never bundle loaders, the game, logs or live settings."""
from pathlib import Path
import hashlib
import zipfile

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "stream/KickChaos/bin/Release/net48"
PANEL = ROOT / "panel/bin/Release/net48"
entries = {
    "Para-copiar-al-juego/scripts/KickChaos.net.dll": BUILD / "KickChaos.net.dll",
    "Para-copiar-al-juego/System.Numerics.Vectors.dll": BUILD / "System.Numerics.Vectors.dll",
    "Para-copiar-al-juego/KickChaos.Panel.exe": PANEL / "KickChaos.Panel.exe",
    "Para-copiar-al-juego/KickChaos.Panel.exe.config": PANEL / "KickChaos.Panel.exe.config",
    "Perfil-stream/config.ini": ROOT / "stream/config/config.ini",
    "Respaldo-1.9.0/config.ini": ROOT / "stream/original/config.ini",
    "LEEME.md": ROOT / "docs/STREAM-INSTALACION.md",
    "VALIDACION.md": ROOT / "docs/STREAM-VALIDACION.md",
    "PANEL.md": ROOT / "panel/LEEME.md",
    "CAMBIOS.md": ROOT / "README.md",
    "Licencias/System.Numerics.Vectors-LICENSE.txt": ROOT / "third-party/System.Numerics.Vectors-LICENSE.txt",
    "Licencias/System.Numerics.Vectors-NOTICES.txt": ROOT / "third-party/System.Numerics.Vectors-NOTICES.txt",
}
for directory in ("stream", "panel", "third-party"):
    for source in (ROOT / directory).rglob("*"):
        relative = source.relative_to(ROOT)
        if not source.is_file() or any(part in ("bin", "obj", "__pycache__") for part in relative.parts):
            continue
        if source.suffix.lower() not in (".dll", ".asi", ".pyc"):
            entries["Codigo-fuente/" + relative.as_posix()] = source
for name in ("README.md", ".gitignore", "docs/STREAM-INSTALACION.md", "docs/STREAM-VALIDACION.md",
             "tools/build-stream.sh", "tools/test-stream.sh", "tools/package-stream.py"):
    entries["Codigo-fuente/" + name] = ROOT / name
for name, path in entries.items():
    if not path.is_file():
        raise SystemExit(f"Falta {path}; compilar antes de empaquetar")
output = ROOT / "dist/KickChaos-IV-1.9.2-camera.zip"
output.parent.mkdir(exist_ok=True)
with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED) as archive:
    hashes = []
    for name, path in sorted(entries.items()):
        data = path.read_bytes()
        info = zipfile.ZipInfo(name, (2026, 10, 6, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        archive.writestr(info, data)
        hashes.append(f"{hashlib.sha256(data).hexdigest()}  {name}")
    archive.writestr("SHA256SUMS.txt", "\n".join(hashes) + "\n")
with zipfile.ZipFile(output) as archive:
    if archive.testzip() is not None:
        raise SystemExit("ZIP corrupto")
    for line in archive.read("SHA256SUMS.txt").decode().splitlines():
        expected, name = line.split("  ", 1)
        if hashlib.sha256(archive.read(name)).hexdigest() != expected:
            raise SystemExit(f"Checksum incorrecto: {name}")
    if any(name.endswith("camaras.ini") and name.startswith("Para-copiar") for name in archive.namelist()):
        raise SystemExit("El paquete no debe reemplazar camaras guardadas")
print(f"{output} ({output.stat().st_size} bytes)")
print("SHA256 " + hashlib.sha256(output.read_bytes()).hexdigest())
