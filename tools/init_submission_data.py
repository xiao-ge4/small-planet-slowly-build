#!/usr/bin/env python3
"""Create a fresh asset library from submission fixtures, without paid calls."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))


def initialize(folder):
    from backend.home_creation.catalog import AssetCatalog
    from backend.home_creation.storage import Repository, canonical, fingerprint
    from backend.store import now

    folder = Path(folder).resolve()
    if (folder / "creation.sqlite3").exists():
        raise SystemExit("目标已有数据库；请选择全新目录，以免改写存档。")
    seed = ROOT / "docs/submission/seed"
    manifest = json.loads((seed / "catalog.json").read_text())
    for asset in manifest["assets"]:
        asset["previewPath"] = str(ROOT / asset["previewPath"])
    repo = Repository(folder)
    try:
        catalog = AssetCatalog(repo, ROOT)
        assets = catalog.import_manifest(manifest)
        recipes = json.loads((seed / "garden-kit.json").read_text())
        for raw in recipes["assets"]:
            aid = raw["id"]
            model_path = seed / "garden-kit" / aid / raw["model"]
            model = repo.put("_submission_library", model_path.read_bytes(), "glb", raw["name"])
            preview = repo.put("_submission_library", (seed / "garden-kit" / aid / raw["preview"]).read_bytes(),
                               "png", raw["name"] + "预览", [model])
            data = {k: raw[k] for k in ("id", "name", "size", "center")}
            data.update(version=1, prefabPath="runtime://" + aid, hasCollider=False, hasLod=False,
                        placeable=True, decorationOnly=True, materials=["圆润叶片", "哑光植物材质"],
                        dependencies=[], interactionKind=None, pivot="bottom-centred",
                        tags=["程序组合", "晨露", "花园", "植物", "地被"], runtimeModelRef=model,
                        previewRef=preview, permissions=["reuse", "transform", "material-copy"],
                        source={"kind": "procedural-kit", "recipeId": aid, "recipeVersion": 1,
                                "sourceSha256": hashlib.sha256(model_path.read_bytes()).hexdigest(),
                                "script": "scripts/build_garden_detail_kit.py"})
            data["contentHash"] = fingerprint({k: v for k, v in data.items() if k not in {"previewRef", "version"}})
            data["indexedAt"] = now()
            with repo.db:
                repo.db.execute("INSERT INTO catalog VALUES (?,?,?)", (aid, 1, canonical(data)))
            assets.append(data)
        print(json.dumps({"initialized": True, "assets": len(assets), "paidCalls": 0}, ensure_ascii=False))
    finally:
        repo.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--data-dir", type=Path, default=ROOT / "data/submission-mock")
    args = parser.parse_args()
    initialize(args.data_dir)


if __name__ == "__main__":
    main()
