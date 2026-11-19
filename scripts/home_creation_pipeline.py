"""Stage 2: offline contract checks, empty run creation and source-scene replay.

No network, generation, approval creation or scene writes. Retrospective evidence
must not be mistaken for a completed live run under the new confirmation gates.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent.parent
PIPELINE = "contracts/home-creation-pipeline.v1.json"
BOUNDARY = "contracts/home-creation-boundaries.v1.json"
OUT = ROOT / "artifacts/home-creation/pipeline"


def read(path):
    return json.loads((ROOT / path).read_text())


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def reference(path, identity=None):
    p = (ROOT / path).resolve()
    if not p.is_relative_to(ROOT) or not p.is_file():
        raise ValueError("Missing or external artifact: " + str(path))
    return {"id": identity or p.relative_to(ROOT).as_posix(), "version": 1,
            "path": p.relative_to(ROOT).as_posix(), "sha256": digest(p)}


def verify_ref(ref):
    try:
        return reference(ref["path"])["sha256"] == ref["sha256"] and ref["version"] >= 1 and bool(ref["id"])
    except (KeyError, TypeError, ValueError, OSError):
        return False


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix(path.suffix + ".tmp")
    temp.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")
    temp.replace(path)


def validate_contract():
    p, b = read(PIPELINE), read(BOUNDARY)
    expected = ["brief", "concept", "breakdown", "asset_design", "assets", "layout", "validation", "finalize"]
    errors = []
    if [s["id"] for s in p["stages"]] != expected:
        errors.append("Stage order differs from documented pipeline")
    for i, stage in enumerate(p["stages"]):
        if stage["dependsOn"] != ([] if i == 0 else [expected[i-1]]):
            errors.append("Broken stage dependency: " + stage["id"])
        for field in ["inputs", "outputs", "requiredFields", "exitChecks", "onFailure", "playerGate"]:
            if not stage.get(field):
                errors.append("Missing %s on %s" % (field, stage["id"]))
    gates = {g["id"] for g in p["gates"]}
    if gates != {"concept", "production_batch", "final_layout"}:
        errors.append("Missing player confirmation gate")
    if p["boundaryContract"] != BOUNDARY or b["productChoices"]["newPlotProfile"] != "starter":
        errors.append("Confirmed terrain profile not carried into pipeline")
    if b["terrain"]["requireVisualThemeTransition"]:
        errors.append("Independent themes must not require transition")
    if p["costPolicy"]["unknownCostIsZero"] or not p["costPolicy"]["budgetRequiredBeforeBillableWork"]:
        errors.append("Invalid cost policy")
    return {"passed": not errors, "stages": len(expected), "gates": sorted(gates), "errors": errors}


def init_run(run_id, output):
    if not run_id or any(c not in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_" for c in run_id):
        raise ValueError("Use a nonempty run ID containing letters, digits, - or _")
    if output.exists():
        raise ValueError("Refusing to overwrite existing run")
    pipeline = read(PIPELINE)
    run = {"schemaVersion": 1, "runId": run_id, "mode": "draft", "pipelineRef": reference(PIPELINE),
           "boundaryRef": reference(BOUNDARY),
           "stages": [{"stage": s["id"], "status": "not_started", "inputs": [], "outputs": [], "issues": []} for s in pipeline["stages"]],
           "confirmations": [], "tasks": []}
    write(output, run)
    return output


def model_provenance(design_path, review, task):
    """Reject approved/generated outputs when their source image was changed."""
    current = digest(design_path)
    return {"reviewMatchesDesign": review.get("status") == "approved" and review.get("designSha256") == current,
            "modelMatchesDesign": task.get("sourceSha256") == current,
            "taskHasDownloadedResult": task.get("state") == "downloaded" and bool(task.get("taskId"))}


def replay_home(home, boundary_audit):
    camp = home["homeId"] == "ember-camp-starter-v1"
    folder = "artifacts/ember-camp" if camp else "artifacts/garden-assets"
    config = read(folder + "/design-config.json")
    concept = folder + "/" + ("concept-v1/design.png" if camp else config["reference"])
    prepared = read(folder + "/prepared-assets.json")
    entries = prepared["generated"] if isinstance(prepared, dict) else prepared
    # Both current manifests are dicts. Keep an explicit error rather than invent IDs.
    assets, errors = [], []
    for item in entries:
        identity = item["id"]
        base = folder + "/" + identity
        design = ROOT / base / "design.png"
        review = read(base + "/review.json")
        task = read(base + "/model-task.json")
        checks = model_provenance(design, review, task)
        files = [reference(base + "/" + name) for name in ["design.png", "review.json", "model-task.json", "front.png", "right.png", "back.png", "source.glb", "lod0.glb", "lod1.glb", "lod2.glb"]]
        if not all(checks.values()):
            errors.append("Source chain mismatch: " + identity)
        assets.append({"assetId": identity, "route": "generated_mesh", "sourceChecks": checks,
                       "modelSettings": task.get("parameters"), "providerTaskId": task.get("taskId"),
                       "files": files, "heightMetres": item.get("heightMeters")})
    configured = {x["id"] for x in config["assets"] if x["kind"] == "mesh"}
    actual = {x["assetId"] for x in assets}
    if configured != actual:
        errors.append("Prepared assets and current design configuration disagree")
    bound_home = next(h for h in boundary_audit["homes"] if h["homeId"] == home["homeId"])
    scene_path = "unity/DawnGarden/" + home["scenePath"]
    scene_ref = reference(scene_path)
    snapshot_current = scene_ref["sha256"] == home["sceneSha256"]
    boundary_current = boundary_audit["contractSha256"] == reference(BOUNDARY)["sha256"] and bound_home["sceneSha256"] == scene_ref["sha256"]
    if not snapshot_current or not boundary_current or not all(c["passed"] for c in bound_home["checks"]):
        errors.append("Stale or failed layout/boundary evidence; rerun Unity exports")
    interaction_ids = [i["stableId"] for i in home["interactions"]]
    if len(set(interaction_ids)) != len(interaction_ids):
        errors.append("Duplicate interaction IDs in source home")
    missing_paths = [i["prefabPath"] for i in home["instances"] if i["prefabPath"] and not (ROOT / "unity/DawnGarden" / i["prefabPath"]).is_file()]
    if missing_paths:
        errors.append("Missing prefab source: " + missing_paths[0])
    cost_path = folder + ("/production-costs.json" if camp else "/cost-summary.json")
    cost = read(cost_path)
    images = cost.get("imageTotalUsd") if camp else cost.get("settledImageTotal")
    approval_path = folder + "/concept-v1/user-approval.json"
    approval = reference(approval_path) if camp else None
    runtime_path = folder + ("/runtime.json" if camp else "/expansion/runtime.json")
    runtime = read(runtime_path)
    # A historical passed report is evidence, but lacks an exact scene hash and
    # must never authorize release under the new pipeline.
    runtime_summary = {"reportRef": reference(runtime_path), "recordedAt": runtime.get("utc"),
                       "passedChecks": sum(c["passed"] for c in runtime["checks"]), "totalChecks": len(runtime["checks"]),
                       "errors": runtime.get("errors"), "boundToCurrentScene": False,
                       "usableAsNewReleaseApproval": False}
    stages = [
        {"stage": "brief", "evidence": [reference("docs/unity/暖焰营地制作计划.md" if camp else "docs/unity/晨曦花园灰盒.md")], "coverage": "historical-docs", "gap": "No structured brief, plot grant or per-run budget"},
        {"stage": "concept", "evidence": [reference(concept)] + ([approval] if approval else []), "coverage": "image-and-user-comment" if approval else "imported-reference", "gap": "Historical confirmation lacks image hash binding" if approval else "No machine-readable player approval bound to this image"},
        {"stage": "breakdown", "evidence": [reference(folder + "/design-config.json")], "coverage": "generation-list-and-scene-snapshot", "gap": "Full reused/procedural/interactive route decisions reconstructed below; not approved as a new batch"},
        {"stage": "asset_design", "evidence": [r for a in assets for r in a["files"] if r["path"].endswith(("design.png", "review.json"))], "coverage": "hash-checked-agent-reviews", "gap": "Internal asset reviews are not player production-batch authorization"},
        {"stage": "assets", "evidence": [reference(folder + "/prepared-assets.json"), reference(cost_path)], "coverage": "local-models-views-lods-and-tasks", "gap": "Generic library scale/pivot/collider/socket metadata still needs later asset-library work"},
        {"stage": "layout", "evidence": [scene_ref, reference("artifacts/home-creation/pipeline/source-layouts.json")], "coverage": "saved-scene-hierarchy-transforms-prefabs-and-interactions", "gap": "Historical hierarchy IDs are not stable authored layout UUIDs; no new plot connection anchors inferred"},
        {"stage": "validation", "evidence": [reference("artifacts/home-creation/boundary-audit.json"), reference(runtime_path)], "coverage": "current-boundary-audit-plus-historical-runtime", "gap": "Need current-scene-bound full runtime, clearance, visual and save/reload verification for new releases"},
        {"stage": "finalize", "evidence": [], "coverage": "not-retroactively-approved", "gap": "No player final confirmation bound to this layout or atomic release record"}
    ]
    routes = [
        {"element": "pet, rig, movement, watering can, hinged chest", "route": "reuse", "reason": "Existing functioning interactions and animation attachment"},
        {"element": "main terrain and extension", "route": "procedural", "reason": "Game-owned geometry, collision and connection"},
        {"element": "ground appearance", "route": "generated_texture", "reason": "Editable material on authored terrain"},
        {"element": "trees, boulders, monument, seedling" + (", tent" if camp else ""), "route": "generated_mesh", "reason": "Theme-specific silhouettes; stateful seedling remains separate"},
        {"element": "sea and pond or spring", "route": "material_variant", "reason": "Reuse water shader; preserve non-walkable volumes"},
        {"element": "path stones and small decorations", "route": "procedural", "reason": "Predictable geometry with placement variation"}
    ]
    if camp:
        routes.append({"element": "shoreline sandstone variants", "route": "reuse", "reason": "Derived from existing generated boulder; later polish replaced primitive border stones", "evidenceRef": reference(folder + "/shore-kit/manifest.json")})
    return {"homeId": home["homeId"], "title": home["title"], "mode": "retrospective-replay",
            "sourceChainPassed": not errors, "errors": errors, "stages": stages, "assets": assets,
            "productionRoutes": routes, "sceneRef": scene_ref, "snapshotCurrent": snapshot_current,
            "boundaryEvidenceCurrent": boundary_current,
            "layoutEvidence": {"instanceCount": len(home["instances"]), "interactionIds": interaction_ids},
            "costs": [{"unit": "USD", "amount": images, "basis": "settled", "sourceRef": reference(cost_path)},
                      {"unit": "AHOLO_CREDITS", "amount": None, "basis": "unknown", "sourceRef": reference(cost_path)}],
            "runtimeEvidence": runtime_summary, "readyForNewPipelineRelease": False}


def replay():
    contract = validate_contract()
    if not contract["passed"]:
        raise ValueError("; ".join(contract["errors"]))
    snapshots = read("artifacts/home-creation/pipeline/source-layouts.json")
    if not snapshots["sourceScenesUnchanged"] or not snapshots["activeScenePreserved"]:
        raise ValueError("Source export did not preserve scenes")
    boundary = read("artifacts/home-creation/boundary-audit.json")
    homes = [replay_home(h, boundary) for h in snapshots["homes"]]
    if {h["homeId"] for h in homes} != {"dawn-garden-starter-v1", "ember-camp-starter-v1"}:
        raise ValueError("Expected both source homes")
    report = {"schemaVersion": 1, "utc": datetime.now(timezone.utc).isoformat(),
              "mode": "offline-retrospective-replay", "contractRef": reference(PIPELINE), "boundaryRef": reference(BOUNDARY),
              "contractChecks": contract, "passed": all(h["sourceChainPassed"] for h in homes), "homes": homes,
              "stageCoverage": "8/8 stages mapped for each home; missing live gates explicitly listed",
              "billableCalls": 0, "newGenerationTasks": 0, "sceneWrites": 0,
              "manualDecisionsToFormalize": [
                  "Choose generation/reuse/material/procedural route for every major element",
                  "Validate view consistency; camp tent v2 replaced v1 before modeling",
                  "Check visual fidelity separately from concept approval; camp shore changed from primitive stones to derived sandstone",
                  "Treat red sea/no glow as an explicit user preference, not an inferred fire-theme effect",
                  "Reserve connection and inspect actual paths; camp approach obstacles were moved during production",
                  "Do not infer final approval from an old passed runtime report or an Agent asset review"
              ]}
    write(OUT / "replay-report.json", report)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("check")
    sub.add_parser("replay")
    init = sub.add_parser("init")
    init.add_argument("--run-id", required=True)
    init.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        if args.command == "check":
            report = validate_contract(); print(json.dumps(report, ensure_ascii=False, indent=2)); return 0 if report["passed"] else 1
        if args.command == "init":
            print(init_run(args.run_id, args.output)); return 0
        report = replay()
        print(json.dumps({"passed": report["passed"], "homes": [{"title": h["title"], "assets": len(h["assets"]), "sourceChainPassed": h["sourceChainPassed"], "errors": h["errors"]} for h in report["homes"]], "report": str(OUT / "replay-report.json"), "billableCalls": 0}, ensure_ascii=False, indent=2))
        return 0 if report["passed"] else 1
    except (ValueError, KeyError, OSError, StopIteration) as e:
        print("Pipeline check failed: " + str(e), file=sys.stderr); return 1


if __name__ == "__main__":
    sys.exit(main())
