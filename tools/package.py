"""Package the compiled user mod without distributing the game or script loaders."""
from pathlib import Path
import hashlib
import zipfile

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "src/KickChaos/bin/Release/net48"
entries = {
    "Para-copiar-al-juego/scripts/KickChaos.net.dll": BUILD / "KickChaos.net.dll",
    "Para-copiar-al-juego/System.Numerics.Vectors.dll": BUILD / "System.Numerics.Vectors.dll",
    "LEEME.md": ROOT / "docs/INSTALACION.md",
    "CAMBIOS.md": ROOT / "README.md",
    "Perfil-recomendado/config.ini": ROOT / "config/recommended/config.ini",
    "Recuperacion-del-RAR/config.ini": ROOT / "config/original/config.ini",
    "Recuperacion-del-RAR/camaras.ini": ROOT / "config/original/camaras.ini",
    "Licencias/System.Numerics.Vectors-LICENSE.txt": ROOT / "third-party/System.Numerics.Vectors-LICENSE.txt",
    "Licencias/System.Numerics.Vectors-NOTICES.txt": ROOT / "third-party/System.Numerics.Vectors-NOTICES.txt",
}
for directory in ("src", "tools", "tests", "docs", "config", "lib", "third-party"):
    for source in (ROOT / directory).rglob("*"):
        if source.is_file() and not any(part in ("bin", "obj", "__pycache__") for part in source.relative_to(ROOT).parts):
            if source.suffix.lower() not in (".dll", ".asi", ".pyc"):
                entries["Codigo-fuente/" + source.relative_to(ROOT).as_posix()] = source
entries["Codigo-fuente/README.md"] = ROOT / "README.md"
entries["Codigo-fuente/.gitignore"] = ROOT / ".gitignore"
for filename, path in entries.items():
    if not path.is_file():
        raise SystemExit(f"Falta {path}: compilar antes de empaquetar")
output = ROOT / "dist/KickChaos-IV-1.6.3.zip"
output.parent.mkdir(exist_ok=True)
with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED) as archive:
    hashes = []
    for filename, path in entries.items():
        data = path.read_bytes()
        archive.writestr(filename, data)
        hashes.append(f"{hashlib.sha256(data).hexdigest()}  {filename}")
    archive.writestr("SHA256SUMS.txt", "\n".join(hashes) + "\n")
with zipfile.ZipFile(output) as archive:
    if archive.testzip() is not None:
        raise SystemExit("Error al verificar el ZIP")
print(output)
