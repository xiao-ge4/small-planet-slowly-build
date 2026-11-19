import hashlib
import json
from pathlib import Path
import sqlite3
import threading
from uuid import uuid4

from backend.store import Problem, now


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def fingerprint(value):
    return hashlib.sha256(canonical(value).encode()).hexdigest()


class Repository:
    def __init__(self, folder):
        self.folder = Path(folder).resolve()
        (self.folder / "assets").mkdir(parents=True, exist_ok=True)
        self.lock = threading.RLock()
        self.db = sqlite3.connect(self.folder / "creation.sqlite3", check_same_thread=False)
        self.db.execute("PRAGMA journal_mode=WAL")
        self.db.execute("CREATE TABLE IF NOT EXISTS runs (id TEXT PRIMARY KEY,data TEXT NOT NULL)")
        self.db.execute("CREATE TABLE IF NOT EXISTS files (id TEXT PRIMARY KEY,run_id TEXT,data TEXT)")
        self.db.commit()

    def get(self, run_id):
        with self.lock:
            row = self.db.execute("SELECT data FROM runs WHERE id=?", (run_id,)).fetchone()
        if not row:
            raise Problem("创作任务不存在。", 404, "NOT_FOUND")
        return json.loads(row[0])

    def save(self, run):
        run["updatedAt"] = now()
        with self.lock, self.db:
            self.db.execute("INSERT OR REPLACE INTO runs VALUES (?,?)", (run["id"], canonical(run)))
        return run

    def save_with_memory(self,run,memory):
        """One commit for the accepted run and its corresponding scene memory."""
        run['updatedAt']=now()
        with self.lock,self.db:
            self.db.execute('INSERT OR IGNORE INTO memories VALUES (?,?)',(memory['id'],canonical(memory)))
            self.db.execute('INSERT OR REPLACE INTO runs VALUES (?,?)',(run['id'],canonical(run)))
        return run

    def conditional_update(self, run_id, expected, change):
        """Atomically update a run only if its top-level state still matches."""
        with self.lock:
            self.db.execute("BEGIN IMMEDIATE")
            try:
                row = self.db.execute("SELECT data FROM runs WHERE id=?", (run_id,)).fetchone()
                if not row:
                    self.db.rollback()
                    raise Problem("创作任务不存在。", 404, "NOT_FOUND")
                run = json.loads(row[0])
                if any(run.get(key) != value for key, value in expected.items()):
                    self.db.rollback()
                    return False, run
                change(run)
                run["updatedAt"] = now()
                self.db.execute("INSERT OR REPLACE INTO runs VALUES (?,?)", (run_id, canonical(run)))
                self.db.commit()
                return True, run
            except Exception:
                if self.db.in_transaction:
                    self.db.rollback()
                raise

    def update(self, run_id, change):
        with self.lock:
            run = self.get(run_id)
            change(run)
            return self.save(run)

    def all(self):
        with self.lock:
            rows = self.db.execute("SELECT data FROM runs ORDER BY rowid DESC").fetchall()
        return [json.loads(r[0]) for r in rows]

    def put(self, run_id, data, suffix, label, parent_refs=None):
        if suffix not in {"json", "png", "jpg", "webp", "glb", "prefab", "unity", "zip", "unitypackage"}:
            raise Problem("不支持的资产类型。")
        fid = uuid4().hex
        target = self.folder / "assets" / (fid + "." + suffix)
        target.write_bytes(data)
        ref = {"id": fid, "version": 1, "path": "assets/" + target.name,
               "sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data), "format": suffix,
               "label": str(label)[:160], "parentRefs": parent_refs or [],
               "url": "/api/home-creation/files/" + fid}
        with self.lock, self.db:
            self.db.execute("INSERT INTO files VALUES (?,?,?)", (fid, run_id, canonical(ref)))
        return ref

    def put_json(self, run_id, value, label, parent_refs=None):
        return self.put(run_id, canonical(value).encode(), "json", label, parent_refs)

    def file(self, file_id, run_id=None):
        with self.lock:
            row = self.db.execute("SELECT run_id,data FROM files WHERE id=?", (file_id,)).fetchone()
        if not row or (run_id is not None and row[0] != run_id):
            raise Problem("资产不存在或不属于当前任务。", 404, "FILE_NOT_FOUND")
        ref = json.loads(row[1])
        return ref

    def path(self, ref, run_id=None):
        saved = self.file(ref["id"], run_id)
        if saved["sha256"] != ref["sha256"]:
            raise Problem("资产引用已失效。", 409, "STALE_ASSET")
        path = self.folder / saved["path"]
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != saved["sha256"]:
            raise Problem("资产文件缺失或已被修改。", 409, "ASSET_INTEGRITY")
        return path

    def close(self):
        self.db.close()
