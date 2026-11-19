#!/usr/bin/env python3
"""Restore verified authoring assets and WebGL from the public release."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import ssl
import tempfile
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = "v0.1.0"
URL = "https://github.com/xiao-ge4/small-planet-slowly-build/releases/download/v0.1.0/SmallPlanetSubmission.zip"
EXPECTED_BYTES = 1070171047
EXPECTED_SHA256 = "81cee48ff10c3981e20b6d486b7f9719fbdf6f7eb7a2743ad030d9d957d07fce"
PREFIX = "SmallPlanetSubmission/"
BUILD_PREFIX = "artifacts/submission/WebGL/"
SOURCE_PREFIX = "unity/DawnGarden/Assets/"
SOURCE_SUFFIXES = {".glb", ".png", ".wav", ".ttf", ".otf"}


def digest(path):
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, help="Use an already downloaded SmallPlanetSubmission.zip")
    args = parser.parse_args()
    output = ROOT / "artifacts/submission"
    output.mkdir(parents=True, exist_ok=True)
    target = output / "WebGL"
    if target.exists():
        raise SystemExit("WebGL 目录已存在，未覆盖。需要更换构建时请先自行移走该目录。")
    with tempfile.TemporaryDirectory(prefix=".release-build-", dir=output) as temporary:
        work = Path(temporary)
        archive = args.archive.resolve() if args.archive else work / "SmallPlanetSubmission.zip"
        if not args.archive:
            print("正在下载已验收的完整试玩包（约 1.07 GB）；不调用任何生成接口。", flush=True)
            context = ssl.create_default_context()
            try:
                import certifi
                context = ssl.create_default_context(cafile=certifi.where())
            except ImportError:
                pass
            request = urllib.request.Request(URL, headers={"User-Agent": "SmallPlanetDemo/0.1"})
            with urllib.request.urlopen(request, timeout=90, context=context) as response, archive.open("wb") as dest:
                count = 0
                reported = -1
                while block := response.read(1024 * 1024):
                    count += len(block)
                    if count > EXPECTED_BYTES:
                        raise ValueError("下载文件超过预期大小，已停止。")
                    dest.write(block)
                    progress = count * 10 // EXPECTED_BYTES
                    if progress != reported:
                        print("下载进度：%d%%" % (progress * 10), flush=True)
                        reported = progress
        if archive.stat().st_size != EXPECTED_BYTES or digest(archive) != EXPECTED_SHA256:
            raise ValueError("试玩包 SHA-256 或大小不匹配，未安装。请重新从正式 Release 下载。")
        staged = work / "verified-files"
        staged.mkdir()
        with zipfile.ZipFile(archive) as source:
            manifest = json.loads(source.read(PREFIX + "submission-manifest.json"))
            rows = [row for row in manifest["files"] if row["path"].startswith(BUILD_PREFIX)
                    or (row["path"].startswith(SOURCE_PREFIX) and Path(row["path"]).suffix.lower() in SOURCE_SUFFIXES)]
            if not rows or not any(row["path"] == BUILD_PREFIX + "index.html" for row in rows):
                raise ValueError("试玩包未包含完整的 WebGL 构建。")
            for row in rows:
                name = Path(row["path"])
                if name.is_absolute() or ".." in name.parts:
                    raise ValueError("非法构建文件路径。")
                existing = ROOT / name
                if existing.exists():
                    if existing.is_file() and existing.stat().st_size == row["bytes"] and digest(existing) == row["sha256"]:
                        continue
                    raise ValueError("已有文件与 Release 不同，未覆盖：" + row["path"])
                value = source.read(PREFIX + row["path"])
                if len(value) != row["bytes"] or hashlib.sha256(value).hexdigest() != row["sha256"]:
                    raise ValueError("构建文件校验失败：" + row["path"])
                path = staged / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(value)
            metadata = source.read(PREFIX + "artifacts/submission/webgl-build-manifest.json")
            if json.loads(metadata).get("buildId") != "3d39fac9727549be882858ac4c1a9431":
                raise ValueError("不是本次发布对应的构建。")
        if target.exists():
            raise ValueError("安装过程中出现已有 WebGL 目录，未覆盖。")
        restored_sources = 0
        for row in rows:
            if row["path"].startswith(BUILD_PREFIX):
                continue
            path = staged / row["path"]
            if path.is_file():
                destination = ROOT / row["path"]
                if destination.exists():
                    raise ValueError("资源目录在安装中发生变化，未覆盖已有文件。")
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.move(str(path), destination)
                restored_sources += 1
        shutil.move(str(staged / BUILD_PREFIX), target)
        (output / "webgl-build-manifest.json").write_bytes(metadata)
        print("WebGL %s 和 %d 项缺失源素材安装完成；已有代码、密钥和存档均未覆盖。" % (VERSION, restored_sources))
        print("运行 .venv/bin/python tools/start_submission.py 即可游玩。")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, zipfile.BadZipFile) as error:
        raise SystemExit("安装失败：%s" % error)
