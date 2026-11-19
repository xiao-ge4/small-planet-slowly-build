import argparse
from pathlib import Path

from backend.http import serve
from backend.provider import AholoProvider, MockProvider, QwenProvider, MockImageProvider
from backend.service import Studio
from backend.store import Store


def main():
    parser = argparse.ArgumentParser(description="物件精灵本地工作台")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--data-dir", type=Path, default=Path(__file__).resolve().parent / "data")
    parser.add_argument("--provider", choices=["live", "mock"], default="live")
    args = parser.parse_args()
    provider = AholoProvider() if args.provider == "live" else MockProvider()
    image_provider = QwenProvider() if args.provider == "live" else MockImageProvider()
    studio = Studio(Store(args.data_dir), provider, image_provider, poll_interval=15 if args.provider == "live" else 0.1)
    serve(studio, args.port)


if __name__ == "__main__":
    main()
