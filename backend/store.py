import copy
import hashlib
import io
import json
from pathlib import Path
import sqlite3
import threading
from datetime import datetime, timezone
from uuid import uuid4

from PIL import Image, UnidentifiedImageError


def now():
    return datetime.now(timezone.utc).isoformat()


def uid():
    return uuid4().hex


class Problem(Exception):
    def __init__(self, message, status=400, code="INVALID_REQUEST"):
        super().__init__(message)
        self.status, self.code = status, code


class Store:
    def __init__(self, folder):
        self.folder = Path(folder).resolve()
        (self.folder / "assets").mkdir(parents=True, exist_ok=True)
        self.lock = threading.RLock()
        self.db = sqlite3.connect(self.folder / "studio.sqlite3", check_same_thread=False)
        self.db.execute("PRAGMA journal_mode=WAL")
        self.db.execute("CREATE TABLE IF NOT EXISTS projects (id TEXT PRIMARY KEY, data TEXT NOT NULL)")
        self.db.execute("CREATE TABLE IF NOT EXISTS files (id TEXT PRIMARY KEY, data TEXT NOT NULL)")
        self.db.execute("CREATE TABLE IF NOT EXISTS prompt_settings (revision INTEGER PRIMARY KEY, data TEXT NOT NULL)")
        self.db.commit()

    def get(self, project_id):
        with self.lock:
            row = self.db.execute("SELECT data FROM projects WHERE id=?", (project_id,)).fetchone()
        if not row:
            raise Problem("项目不存在。", 404, "NOT_FOUND")
        return json.loads(row[0])

    def save(self, project):
        project["updatedAt"] = now()
        with self.lock, self.db:
            self.db.execute("INSERT OR REPLACE INTO projects VALUES (?, ?)",
                            (project["id"], json.dumps(project, ensure_ascii=False)))
        return project

    def update(self, project_id, fn):
        with self.lock:
            project = self.get(project_id)
            fn(project)
            return self.save(project)

    def all(self):
        with self.lock:
            rows = self.db.execute("SELECT data FROM projects").fetchall()
        return sorted([json.loads(r[0]) for r in rows], key=lambda p: p["updatedAt"], reverse=True)

    def create(self, title="未命名物件"):
        return self.save({"schemaVersion": 2, "id": uid(), "title": title[:80], "status": "draft",
            "sourceImages": [], "activeImageIds": [], "tasks": [], "versions": [],
            "inputInstruction": "", "activeVersionId": None, "activeAnthroVersionId": None,
            "confirmedVersionId": None, "confirmedAnthroVersionId": None, "createdAt": now(), "updatedAt": now(), "_requests": {}})

    def file(self, file_id):
        with self.lock:
            row = self.db.execute("SELECT data FROM files WHERE id=?", (file_id,)).fetchone()
        if not row:
            raise Problem("文件不存在。", 404, "NOT_FOUND")
        return json.loads(row[0])

    def file_path(self, file_id):
        return self.folder / "assets" / self.file(file_id)["storageKey"]

    def save_file(self, data, suffix, file_name=None):
        file_id = uid()
        name = file_id + "." + suffix
        target = self.folder / "assets" / name
        target.write_bytes(data)
        entry = {"id": file_id, "format": suffix, "fileName": (file_name or name)[:180],
                 "sizeBytes": len(data), "storageKey": name,
                 "url": "/files/" + file_id, "sha256": hashlib.sha256(data).hexdigest()}
        with self.lock, self.db:
            self.db.execute("INSERT INTO files VALUES (?,?)", (file_id, json.dumps(entry)))
        return entry

    def save_image(self, data, file_name="image", origin="upload"):
        if not data or len(data) > 10 * 1024 * 1024:
            raise Problem("每张图片需小于 10 MB。", code="INVALID_IMAGE")
        try:
            with Image.open(io.BytesIO(data)) as image:
                width, height = image.size
                fmt = image.format
                if width * height > 40_000_000 or min(width, height) < 32:
                    raise Problem("图片尺寸应至少为 32×32，且不超过 4000 万像素。")
                image.verify()
        except (UnidentifiedImageError, OSError, Image.DecompressionBombError):
            raise Problem("图片无法读取，请使用 JPG、PNG 或 WebP。", code="INVALID_IMAGE")
        formats = {"JPEG": ("jpg", "image/jpeg"), "PNG": ("png", "image/png"), "WEBP": ("webp", "image/webp")}
        if fmt not in formats:
            raise Problem("仅支持 JPG、PNG 或 WebP 图片。", code="INVALID_IMAGE")
        suffix, mime = formats[fmt]
        file = self.save_file(data, suffix, Path(file_name).name)
        return {**file, "origin": origin, "role": "reference", "order": 0,
                "mimeType": mime, "width": width, "height": height, "createdAt": now()}


def public(value):
    """Remove internal keys, provider URLs and exception details from API responses."""
    if isinstance(value, dict):
        return {k: public(v) for k, v in value.items()
                if not k.startswith("_") and k not in {"providerUrl", "providerUrlExpiresAt"}}
    if isinstance(value, list):
        return [public(v) for v in value]
    return copy.deepcopy(value)
