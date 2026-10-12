"""Preview a Unity Web export using the same compression headers as Vercel."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class UnityHandler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        if path.endswith(".gz"):
            path = path[:-3]
        if path.endswith(".wasm"):
            return "application/wasm"
        if path.endswith(".data"):
            return "application/octet-stream"
        if path.endswith(".js"):
            return "application/javascript"
        return super().guess_type(path)

    def end_headers(self):
        if self.path.split("?", 1)[0].endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--directory", default="Builds/WebGL")
    parser.add_argument("--port", type=int, default=8015)
    args = parser.parse_args()
    root = Path(args.directory).resolve()
    if not (root / "index.html").is_file():
        parser.error(f"No Web build in {root}")
    server = ThreadingHTTPServer(("127.0.0.1", args.port), partial(UnityHandler, directory=str(root)))
    print(f"MOJI Fighter X: http://127.0.0.1:{args.port}", flush=True)
    server.serve_forever()
