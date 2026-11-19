"""Phase 1 probe: local images -> Lux3D task -> saved, structurally checked GLB.

Run using .venv/bin/python. Secrets are read locally and never included in logs.
Task IDs are saved immediately; resume polling instead of submitting twice.
"""

import argparse
import json
import os
from pathlib import Path
import struct
import sys
import time
from datetime import datetime, timezone
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parent.parent


def read_key():
    value = os.environ.get("AHOLO_API_KEY", "").strip()
    if not value:
        path = ROOT / ".env.local"
        if path.exists():
            for line in path.read_text().splitlines():
                if line.strip().startswith("AHOLO_API_KEY="):
                    value = line.strip().split("=", 1)[1].strip().strip("\"'")
                    break
    if not value or value in {"your_api_key_here", "填入密钥"}:
        raise ValueError("请先在 .env.local 填写 AHOLO_API_KEY 并保存。")
    return value


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(data, ensure_ascii=False, indent=2))
    os.replace(temporary, path)


def inspect_glb(path):
    """Check GLB container and confirm it contains drawable glTF meshes."""
    raw = path.read_bytes()
    if len(raw) < 20:
        raise ValueError("GLB 文件不完整。")
    magic, version, declared_size = struct.unpack_from("<4sII", raw)
    if magic != b"glTF" or version != 2 or declared_size != len(raw):
        raise ValueError("GLB 文件头或长度不合法。")
    chunk_size, chunk_kind = struct.unpack_from("<II", raw, 12)
    if chunk_kind != 0x4E4F534A or 20 + chunk_size > len(raw):
        raise ValueError("GLB 缺少合法的 JSON 数据块。")
    document = json.loads(raw[20:20 + chunk_size])
    meshes = document.get("meshes", [])
    primitives = sum(len(m.get("primitives", [])) for m in meshes)
    if not primitives:
        raise ValueError("GLB 不包含可绘制的 mesh primitives。")
    return {"bytes": len(raw), "meshes": len(meshes), "primitives": primitives,
            "materials": len(document.get("materials", [])),
            "note": "结构检查通过；仍需浏览器加载与视觉检查。"}


def save_outputs(result, folder):
    import httpx
    saved = []
    for index, output in enumerate(result.get("outputs", [])):
        url = output.get("content", "")
        parsed = urlparse(url)
        if parsed.scheme != "https":
            continue
        suffix = Path(parsed.path).suffix.lower()
        if suffix not in {".glb", ".zip", ".png", ".jpg", ".jpeg", ".webp"}:
            continue
        name = "model.glb" if suffix == ".glb" else "output-%d%s" % (index, suffix)
        target = folder / name
        temporary = target.with_suffix(target.suffix + ".part")
        with httpx.stream("GET", url, timeout=120, follow_redirects=True) as response:
            response.raise_for_status()
            with temporary.open("wb") as handle:
                for chunk in response.iter_bytes():
                    handle.write(chunk)
        temporary.replace(target)
        entry = {"path": str(target.relative_to(ROOT)), "bytes": target.stat().st_size}
        if suffix == ".glb":
            entry["glbCheck"] = inspect_glb(target)
        saved.append(entry)
        print("已保存：%s" % entry["path"], flush=True)
    if not saved:
        raise ValueError("任务成功但没有识别到可下载的输出；结果已保存，请检查 manifest。")
    return saved


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("images", nargs="*", help="1–4 张本地物件照片")
    parser.add_argument("--resume", type=Path, help="已有运行目录；只查询，不重复创建任务")
    parser.add_argument("--model", choices=["G1", "G1-Turbo"], default="G1-Turbo")
    parser.add_argument("--region", choices=["cn", "com"], default="cn", help="国内账号用 cn；国际账号用 com")
    parser.add_argument("--instruction", help="先用图片＋文字生成修改后的设计图，再另行验证图转 3D")
    parser.add_argument("--timeout", type=int, default=1800, help="轮询最长秒数；到时保留任务供恢复")
    parser.add_argument("--check-config", action="store_true")
    args = parser.parse_args()
    key = read_key()
    if args.check_config:
        print("本地密钥已配置（未输出密钥；尚未验证权限及额度）。")
        return
    from manycore.aholo_sdk_core import AholoClientConfig
    from manycore.aholo_sdk_asset import create_asset_client
    from manycore.aholo_sdk_lux3d import create_lux3d_client

    config = AholoClientConfig(api_key=key, region=args.region)
    lux3d = create_lux3d_client(config)
    if args.resume:
        folder = args.resume.resolve()
        if not folder.is_relative_to(ROOT / "validation-runs"):
            raise ValueError("恢复目录必须位于 validation-runs 下。")
        record = json.loads((folder / "manifest.json").read_text())
        if record.get("region", args.region) != args.region:
            raise ValueError("恢复查询的区域与原任务不同，请显式指定 --region。")
        if not record.get("taskId"):
            raise ValueError("此运行没有已记录的 taskId，请先核对服务端任务历史。")
        print("恢复已有任务 %s，不新建生成任务。" % record["taskId"], flush=True)
    else:
        paths = [Path(p).expanduser().resolve() for p in args.images]
        if not 1 <= len(paths) <= 4:
            raise ValueError("请提供 1–4 张照片。")
        if args.instruction and len(paths) != 1:
            raise ValueError("文字修改验证先使用一张参考图，避免不同角度相互矛盾。")
        for path in paths:
            if not path.is_file() or path.suffix.lower() not in {".jpg", ".jpeg", ".png", ".webp"}:
                raise ValueError("测试照片需要是存在的 JPG/PNG/WebP 文件。")
        folder = ROOT / "validation-runs" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
        folder.mkdir(parents=True)
        record = {"createdAt": datetime.now(timezone.utc).isoformat(),
                  "kind": "multimodal-to-image" if args.instruction else "img-to-3d",
                  "model": args.model, "region": args.region, "sourceFiles": [str(p) for p in paths],
                  "instruction": args.instruction, "status": "uploading"}
        write_json(folder / "manifest.json", record)
        asset = create_asset_client(config)
        urls = []
        for i, path in enumerate(paths):
            print("正在上传参考图 %d/%d…" % (i + 1, len(paths)), flush=True)
            urls.append(asset.upload_file(path).url)
        record["sourceUrls"] = urls
        record["status"] = "submitting"
        write_json(folder / "manifest.json", record)
        print("创建一次生成任务（发生超时时不会自动重复提交）…", flush=True)
        if args.instruction:
            task_id = lux3d.multimodal_to_image.create(img=urls[0], prompt=args.instruction)
        else:
            inputs = {"img": urls[0]} if len(urls) == 1 else {"imgs": urls}
            task_id = lux3d.img_to_3d.create(**inputs, version=args.model,
                face_count=100000, output_format=["glb"], enable_pbr=True)
        record.update(taskId=task_id, status="running")
        write_json(folder / "manifest.json", record)
        print("任务已创建：%s；记录：%s" % (task_id, folder.relative_to(ROOT)), flush=True)

    deadline = time.monotonic() + args.timeout
    previous = None
    while time.monotonic() < deadline:
        result = lux3d.tasks.retrieve(record["taskId"])
        code = result["status"]
        record.update(providerStatus=code, latestResult=result)
        write_json(folder / "manifest.json", record)
        if code != previous:
            print("任务状态：%s" % {0:"初始化",1:"生成中",3:"成功",4:"失败",6:"取消"}.get(code, str(code)), flush=True)
            previous = code
        if code == 3:
            record["files"] = save_outputs(result, folder)
            if record["kind"] == "img-to-3d" and not any(f["path"].endswith(".glb") for f in record["files"]):
                raise ValueError("图转 3D 未保存到 GLB，需核对实际输出格式。")
            record["status"] = "succeeded"
            write_json(folder / "manifest.json", record)
            print("验证结果已保存。", flush=True)
            return
        if code in {4, 6}:
            record["status"] = "failed" if code == 4 else "cancelled"
            write_json(folder / "manifest.json", record)
            raise ValueError("生成任务未成功，已保存结果供排查。")
        time.sleep(min(15, max(0, deadline - time.monotonic())))
    print("本次轮询到时，服务端任务可能仍在运行；使用 --resume 恢复查询，避免重复生成。")
    sys.exit(2)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, FileNotFoundError) as exc:
        print(str(exc), file=sys.stderr)
        sys.exit(1)
    except Exception as exc:
        # SDK/HTTP exceptions can contain signed URLs. Keep them out of chat logs.
        code = getattr(exc, "code", None)
        safe_code = str(code) if code is not None and str(code).replace("_", "").replace("-", "").isalnum() else "unknown"
        print("验证中断（%s，code=%s）。已有 taskId 时请恢复查询，不要直接重复提交。" % (type(exc).__name__, safe_code), file=sys.stderr)
        sys.exit(1)
