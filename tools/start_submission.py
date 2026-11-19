#!/usr/bin/env python3
"""Run the submitted WebGL client and backend on one localhost origin."""
import argparse
from pathlib import Path
import socket
import subprocess
import sys
import time
import urllib.request
import webbrowser

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8791)
    parser.add_argument("--mode", choices=("mock", "live"), default="live")
    parser.add_argument("--web-root", type=Path, default=ROOT / "artifacts/submission/WebGL")
    parser.add_argument("--data-dir", type=Path)
    parser.add_argument("--no-browser", action="store_true")
    args = parser.parse_args()
    web_root = args.web_root.resolve()
    if not (web_root / "index.html").is_file():
        raise SystemExit("WebGL 构建缺失；请使用包含 artifacts/submission/WebGL/index.html 的提交包。")
    with socket.socket() as probe:
        if probe.connect_ex(("127.0.0.1", args.port)) == 0:
            raise SystemExit("指定端口已被使用，请使用 --port 选择其他端口。")
    data_dir = (args.data_dir or ROOT / ("data/submission-" + args.mode)).resolve()
    if not (data_dir / "creation.sqlite3").exists():
        subprocess.run([sys.executable, str(ROOT / "tools/init_submission_data.py"),
                        "--data-dir", str(data_dir)], cwd=ROOT, check=True)
    command = [sys.executable, str(ROOT / "home_server.py"), "--port", str(args.port),
               "--mode", args.mode, "--data-dir", str(data_dir), "--web-root", str(web_root)]
    process = subprocess.Popen(command, cwd=ROOT)
    url = "http://127.0.0.1:%d/" % args.port
    try:
        deadline = time.monotonic() + 25
        while time.monotonic() < deadline:
            if process.poll() is not None:
                raise SystemExit("本地服务启动失败，请查看上方错误。")
            try:
                with urllib.request.urlopen(url + "api/home-creation/config", timeout=1) as response:
                    if response.status == 200:
                        break
            except OSError:
                time.sleep(0.2)
        else:
            raise SystemExit("本地服务启动超时。")
        print("捡个星球慢慢造：%s · %s（Ctrl+C 关闭）" % (url, args.mode), flush=True)
        if not args.no_browser:
            webbrowser.open(url)
        process.wait()
    except KeyboardInterrupt:
        pass
    finally:
        if process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()


if __name__ == "__main__":
    main()
