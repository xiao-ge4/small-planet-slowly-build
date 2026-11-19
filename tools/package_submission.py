#!/usr/bin/env python3
"""Allowlist the portable submission; never copy local saves, keys or Unity caches."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "unity/DawnGarden"
TREE_ROOTS = ("backend", "contracts", "vendor", "docs/submission/seed", "web/submission",
              "unity/DawnGarden/Assets", "unity/DawnGarden/Packages", "unity/DawnGarden/ProjectSettings")
SINGLE_FILES = ("README.md", ".env.example", ".gitignore", "requirements-submission.txt",
                "server.py", "home_server.py", "tools/unity", "tools/blender",
                "tools/init_submission_data.py", "tools/start_submission.py",
                "tools/package_submission.py", "tools/check_submission.py",
                "scripts/verify_lux3d.py", "scripts/prepare_home_asset_blender.py",
                "scripts/build_garden_detail_kit.py", "scripts/home_creation_pipeline.py",
                "tests/test_guardian_land_csharp.py", "tests/csharp/GardenGuardianLandChecks.cs",
                "docs/submission/BOUNDARIES.md", "web/submission/index.html", "web/submission/play.html",
                "web/submission/assets/cover.jpg")
SKIP_PARTS = {"__pycache__", "node_modules", ".git", ".venv", ".tooling", "Library", "Logs", "Temp",
              "obj", "Obj", "UserSettings", "MemoryCaptures", "Recordings", "_Recovery",
              "Samples~", "Documentation~", "Tests", "Tests~", ".idea", ".vs"}
SKIP_SUFFIXES = {".pyc", ".pyo", ".sqlite3", ".sqlite", ".db", ".log", ".tmp", ".bak", ".pem", ".key"}
ARCHIVE_FOLDER = "SmallPlanetSubmission"


def rejected(path):
    p = Path(path)
    return (bool(set(p.parts) & SKIP_PARTS) or p.name in {".DS_Store", "Thumbs.db", "_Recovery.meta", "Temp.meta"}
            or p.suffix.lower() in SKIP_SUFFIXES or (p.name.startswith(".env") and p.name != ".env.example")
            or p.name.endswith(("-wal", "-shm")))


def known_credentials():
    values = set()
    local = ROOT / ".env.local"
    if local.is_file():
        for line in local.read_text().splitlines():
            if "=" not in line or line.lstrip().startswith("#"):
                continue
            name, value = line.split("=", 1)
            if any(word in name.upper() for word in ("KEY", "TOKEN", "SECRET", "PASSWORD")):
                value = value.strip().strip("\"'")
                if len(value) >= 8:
                    values.add(value.encode())
    for name, value in os.environ.items():
        if any(word in name.upper() for word in ("API_KEY", "ACCESS_TOKEN", "SECRET_KEY")) and len(value) >= 8:
            values.add(value.encode())
    return values


def scan_secrets(path, credentials):
    # Check exact known credentials in every file, including binary Unity/WebGL assets.
    overlap = max([len(value) for value in credentials] + [128])
    tail = b""
    with path.open("rb") as handle:
        while chunk := handle.read(1024 * 1024):
            data = tail + chunk
            if any(value in data for value in credentials):
                raise ValueError("检测到本地凭据，拒绝打包：" + str(path.relative_to(ROOT)))
            if re.search(rb"\bsk-[A-Za-z0-9_-]{24,}", data):
                raise ValueError("检测到疑似 API key，拒绝打包：" + str(path.relative_to(ROOT)))
            tail = data[-overlap:]


def package_source(name, blockers):
    """Project embedded packages are authoritative, including local importer changes."""
    embedded = PROJECT / "Packages" / name
    if embedded.exists():
        if not (embedded / "package.json").is_file():
            blockers.append("embedded package missing package.json: " + name)
            return None
        return embedded
    matches = list((PROJECT / "Library/PackageCache").glob(name + "@*"))
    if len(matches) != 1 or not (matches[0] / "package.json").is_file():
        blockers.append("missing pinned package: " + name)
        return None
    return matches[0]


def collect(web_root, include_webgl=True):
    entries = {}
    blockers = []

    def add(source, target):
        if rejected(target):
            return
        if source.is_symlink():
            raise ValueError("不打包符号链接：" + target)
        entries[target] = source

    for name in SINGLE_FILES:
        if (ROOT / name).is_file():
            add(ROOT / name, name)
        else:
            blockers.append("missing: " + name)
    for source in sorted(ROOT.iterdir()):
        if source.is_file() and source.suffix in {".html", ".js", ".css"}:
            add(source, source.name)
    for name in TREE_ROOTS:
        directory = ROOT / name
        if not directory.is_dir():
            blockers.append("missing: " + name)
            continue
        for current, dirs, files in os.walk(directory):
            dirs[:] = sorted(d for d in dirs if d not in SKIP_PARTS)
            for filename in sorted(files):
                path = Path(current) / filename
                add(path, path.relative_to(ROOT).as_posix())

    # Embed exact pinned sources needed by catalog dependency checks; no Library is shipped.
    seed = json.loads((ROOT / "docs/submission/seed/catalog.json").read_text())
    packages = sorted({Path(dep["path"]).parts[1] for asset in seed["assets"]
                       for dep in asset["dependencies"] if dep["path"].startswith("Packages/")})
    for name in packages:
        destination = "unity/DawnGarden/Packages/" + name
        package = package_source(name, blockers)
        if package is None:
            continue
        for current, dirs, files in os.walk(package):
            dirs[:] = sorted(d for d in dirs if d not in SKIP_PARTS)
            for filename in sorted(files):
                path = Path(current) / filename
                add(path, destination + "/" + path.relative_to(package).as_posix())

    generated = {}
    for edition in ("game-v1", "game-v5"):
        manifest = ROOT / "artifacts" / edition / "build-manifest.json"
        if manifest.is_file():
            raw = json.loads(manifest.read_text())
            clean = {key: raw[key] for key in ("buildId", "createdAt", "success", "assets", "scenes") if key in raw}
            generated["artifacts/" + edition + "/build-manifest.json"] = (json.dumps(clean, ensure_ascii=False, indent=2) + "\n").encode()
        else:
            blockers.append("missing " + edition + " build manifest")
    library_input = ROOT / "artifacts/game-v1/library-input.json"
    if library_input.is_file():
        add(library_input, "artifacts/game-v1/library-input.json")
    # Legacy editor re-preparation reads these exact paths; keep only the clean reusable kit.
    kit = json.loads((ROOT / "docs/submission/seed/garden-kit.json").read_text())
    generated["artifacts/garden-detail-kit/v1/manifest.json"] = (json.dumps(kit, ensure_ascii=False, indent=2) + "\n").encode()
    for asset in kit["assets"]:
        for field in ("model", "preview"):
            source = ROOT / "docs/submission/seed/garden-kit" / asset["id"] / asset[field]
            add(source, "artifacts/garden-detail-kit/v1/" + asset["id"] + "/" + asset[field])
    if include_webgl:
        build_manifest = web_root.parent / "webgl-build-manifest.json"
        if build_manifest.is_file():
            clean = json.loads(build_manifest.read_text())
            clean["appPath"] = "artifacts/submission/WebGL"
            if not clean.get("success"):
                blockers.append("WebGL build manifest does not report success")
            runtime = generated.get("artifacts/game-v1/build-manifest.json")
            if runtime and clean.get("sourceBuildId") != json.loads(runtime)["buildId"]:
                blockers.append("WebGL sourceBuildId differs from runtime buildId")
            generated["artifacts/submission/webgl-build-manifest.json"] = (json.dumps(clean, ensure_ascii=False, indent=2) + "\n").encode()
        else:
            blockers.append("missing WebGL build manifest")
        if not (web_root / "index.html").is_file():
            blockers.append("missing WebGL index.html")
        else:
            for pattern in ("*.loader.js", "*.data", "*.framework.js", "*.wasm"):
                matches = list((web_root / "Build").glob(pattern))
                if len(matches) != 1 or not matches[0].is_file() or matches[0].stat().st_size == 0:
                    blockers.append("missing or ambiguous WebGL Build/" + pattern)
            for current, dirs, files in os.walk(web_root):
                dirs[:] = sorted(d for d in dirs if d not in SKIP_PARTS)
                for filename in sorted(files):
                    source = Path(current) / filename
                    add(source, "artifacts/submission/WebGL/" + source.relative_to(web_root).as_posix())
    return entries, generated, blockers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/submission/SmallPlanetSubmission.zip")
    parser.add_argument("--webgl-dir", type=Path, default=ROOT / "artifacts/submission/WebGL")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--without-webgl", action="store_true", help="source-only inventory, for pre-build review")
    parser.add_argument("--manifest", type=Path, help="write the relative-path inventory as JSON")
    args = parser.parse_args()
    try:
        entries, generated, blockers = collect(args.webgl_dir.resolve(), not args.without_webgl)
        credentials = known_credentials()
        files = []
        for name, source in sorted(entries.items()):
            scan_secrets(source, credentials)
            files.append({"path": name, "bytes": source.stat().st_size,
                          "sha256": hashlib.sha256(source.read_bytes()).hexdigest()})
        for name, data in sorted(generated.items()):
            if any(value in data for value in credentials):
                raise ValueError("生成文件包含本地凭据，拒绝打包。")
            files.append({"path": name, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()})
        report = {"schemaVersion": 1, "sourceOnly": args.without_webgl, "files": sorted(files, key=lambda v: v["path"]),
                  "fileCount": len(files), "totalBytes": sum(f["bytes"] for f in files),
                  "blockers": blockers, "secretsChecked": True,
                  "excluded": [".env.local and credentials", "all user databases/saves/chats", "Unity caches and recovery",
                               "test/recording outputs and historical builds", "machine tooling and virtualenv"]}
        if args.manifest:
            args.manifest.parent.mkdir(parents=True, exist_ok=True)
            args.manifest.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
        if not args.dry_run:
            if blockers:
                raise ValueError("提交包尚不完整：" + "; ".join(blockers))
            args.output.parent.mkdir(parents=True, exist_ok=True)
            with zipfile.ZipFile(args.output, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
                for name, source in sorted(entries.items()):
                    archive.write(source, ARCHIVE_FOLDER + "/" + name)
                for name, data in sorted(generated.items()):
                    archive.writestr(ARCHIVE_FOLDER + "/" + name, data)
                archive.writestr(ARCHIVE_FOLDER + "/submission-manifest.json", json.dumps(report, ensure_ascii=False, indent=2) + "\n")
        print(json.dumps({"dryRun": args.dry_run, "fileCount": report["fileCount"], "totalBytes": report["totalBytes"],
                          "blockers": blockers, "secretsChecked": True,
                          "zip": str(args.output) if not args.dry_run else None}, ensure_ascii=False))
    except (OSError, ValueError) as exc:
        print(str(exc), file=sys.stderr)
        raise SystemExit(1)


if __name__ == "__main__":
    main()
