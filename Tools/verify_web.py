"""Check a local/deployed Unity export, including gzip integrity and Wasm MIME."""
import argparse
import gzip
import hashlib
import json
import re
import urllib.request
from pathlib import Path


def verify(directory, base_url):
    root = Path(directory).resolve()
    html = (root / "index.html").read_text(encoding="utf-8")
    assert "{{{" not in html, "Unexpanded Unity template"
    paths = set(re.findall(r"Build/[A-Za-z0-9_.-]+\.(?:loader\.js|data\.gz|framework\.js\.gz|wasm\.gz)", html))
    assert len(paths) == 4, f"Expected four build resources, got {paths}"
    paths.update({"index.html", "build-info.json"})
    paths.update(str(path.relative_to(root)).replace("\\", "/") for path in (root / "Licenses").rglob("OFL.txt"))
    assert len(paths) == 11, "Missing bundled font license notices"
    unicode_license = root / "Licenses/Unicode/LICENSE.txt"
    if unicode_license.exists():
        paths.add("Licenses/Unicode/LICENSE.txt")
    checked = []
    for relative in sorted(paths):
        expected = (root / relative).read_bytes()
        if relative.endswith(".gz"):
            decoded = gzip.decompress(expected)
            if relative.endswith(".wasm.gz"):
                assert decoded[:8] == b"\x00asm\x01\x00\x00\x00", "Invalid WebAssembly"
        url = base_url.rstrip("/") + "/" + relative
        request = urllib.request.Request(url, headers={"Accept-Encoding": "gzip"})
        with urllib.request.urlopen(request, timeout=120) as response:
            assert response.status == 200, (relative, response.status)
            received = response.read()
            if not relative.endswith(".gz") and response.headers.get("Content-Encoding") == "gzip":
                received = gzip.decompress(received)
            assert received == expected, f"Deployed content mismatch: {relative}"
            if relative.endswith(".gz"):
                assert response.headers.get("Content-Encoding") == "gzip", f"Missing gzip header: {relative}"
            if relative.endswith(".wasm.gz"):
                assert response.headers.get_content_type() == "application/wasm", "Wrong Wasm MIME type"
            if relative.endswith(".js.gz"):
                assert response.headers.get_content_type() == "application/javascript", "Wrong JS MIME type"
            checked.append({"file": relative, "bytes": len(expected), "sha256": hashlib.sha256(received).hexdigest()})
    return {"url": base_url, "verified": checked}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("url")
    parser.add_argument("--directory", default="Builds/WebGL")
    parser.add_argument("--report")
    args = parser.parse_args()
    report = verify(args.directory, args.url)
    if args.report:
        Path(args.report).write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Verified {len(report['verified'])} files: gzip integrity, content hashes, Wasm/JS MIME and font notices.")
