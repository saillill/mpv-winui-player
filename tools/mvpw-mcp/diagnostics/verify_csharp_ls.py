"""Does csharp-ls actually serve this project? Speak LSP to it and find out.

Installing a plugin is not the same as proving it works. `csharp-lsp` is a thin
wrapper: it ships no server, only a README saying to `dotnet tool install --global
csharp-ls`. So the real question is whether csharp-ls 0.28.0 can load
`mpv-winui.slnx` (an unusual file name -- .NET's newer XML solution format) and answer
a real query about this project's code.

This does the minimum real conversation:

  1. spawn `csharp-ls`
  2. send `initialize` with the project root as rootUri
  3. wait for the response, check it declares documentSymbol/definition capabilities
  4. send `initialized` (the notification LSP requires next)
  5. open one real .cs file from this repo with `textDocument/didOpen`
  6. ask for `textDocument/documentSymbol` and print what comes back

A server that starts but cannot index the project answers nothing at step 6, which is
the failure mode worth catching.

Run:  python _work/tooling-20261027/verify_csharp_ls.py
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
import threading
import time

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
TOOL = os.path.join(os.environ["USERPROFILE"], ".dotnet", "tools", "csharp-ls.exe")
TARGET = os.path.join(REPO, "mpv-winui", "mpv-winui", "Modules", "Settings", "MpvSettings.cs")


class Lsp:
    def __init__(self, exe: str, cwd: str):
        self.p = subprocess.Popen(
            [exe], cwd=cwd,
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        self.buf = b""
        self.next_id = 1
        self.errors = []
        threading.Thread(target=self._drain_err, daemon=True).start()

    def _drain_err(self):
        for line in self.p.stderr:
            self.errors.append(line.decode("utf-8", "replace").rstrip())

    def _read_message(self, timeout: float):
        """LSP frames are `Content-Length: N\\r\\n\\r\\n<N bytes>`."""
        deadline = time.time() + timeout
        while time.time() < deadline:
            if b"\r\n\r\n" in self.buf:
                head, rest = self.buf.split(b"\r\n\r\n", 1)
                length = None
                for line in head.split(b"\r\n"):
                    if line.lower().startswith(b"content-length:"):
                        length = int(line.split(b":")[1].strip())
                if length is not None and len(rest) >= length:
                    body, self.buf = rest[:length], rest[length:]
                    return json.loads(body.decode("utf-8"))
            chunk = self.p.stdout.read(1)
            if not chunk:
                return None
            self.buf += chunk
        return None

    def send(self, payload: dict):
        body = json.dumps(payload).encode("utf-8")
        self.p.stdin.write(b"Content-Length: %d\r\n\r\n" % len(body) + body)
        self.p.stdin.flush()

    def request(self, method: str, params: dict, timeout: float = 60.0):
        rid = self.next_id
        self.next_id += 1
        self.send({"jsonrpc": "2.0", "id": rid, "method": method, "params": params})
        deadline = time.time() + timeout
        while time.time() < deadline:
            msg = self._read_message(2.0)
            if msg is None:
                continue
            # Skip server->client requests/notifications; we only want our reply.
            if msg.get("id") == rid:
                return msg
        return None

    def notify(self, method: str, params: dict):
        self.send({"jsonrpc": "2.0", "method": method, "params": params})

    def close(self):
        try:
            self.notify("exit", {})
        except Exception:
            pass
        try:
            self.p.kill()
        except Exception:
            pass


def main() -> int:
    if not os.path.exists(TOOL):
        print(f"FAIL: csharp-ls not found at {TOOL}")
        return 2
    if not os.path.exists(TARGET):
        print(f"FAIL: target file not found: {TARGET}")
        return 2

    print(f"server : {TOOL}")
    print(f"root   : {REPO}")
    print(f"target : {os.path.relpath(TARGET, REPO)}\n")

    lsp = Lsp(TOOL, REPO)
    failures = []
    try:
        init = lsp.request("initialize", {
            "processId": os.getpid(),
            "rootUri": "file:///" + REPO.replace("\\", "/"),
            "capabilities": {
                "textDocument": {
                    "documentSymbol": {"hierarchicalDocumentSymbolSupport": True},
                    "definition": {"linkSupport": True},
                }
            },
            "workspaceFolders": [{
                "uri": "file:///" + REPO.replace("\\", "/"),
                "name": "mpv-clean",
            }],
        }, timeout=120.0)

        if not init:
            print("FAIL: no response to initialize()")
            failures.append("initialize timed out")
        elif "error" in init:
            print(f"FAIL: initialize returned an error: {init['error']}")
            failures.append("initialize error")
        else:
            caps = init.get("result", {}).get("capabilities", {})
            print(f"PASS  initialize answered by {init.get('result', {}).get('serverInfo', {}).get('name', '?')}")
            for cap in ("documentSymbolProvider", "definitionProvider",
                        "hoverProvider", "referencesProvider"):
                if caps.get(cap):
                    print(f"PASS  capability: {cap}")
                else:
                    print(f"      capability absent: {cap}")

            lsp.notify("initialized", {})

            text = open(TARGET, encoding="utf-8", errors="replace").read()
            lsp.notify("textDocument/didOpen", {
                "textDocument": {
                    "uri": "file:///" + TARGET.replace("\\", "/"),
                    "languageId": "csharp",
                    "version": 1,
                    "text": text,
                }
            })
            time.sleep(3.0)   # let it parse; no progress token to wait on

            sym = lsp.request("textDocument/documentSymbol", {
                "textDocument": {"uri": "file:///" + TARGET.replace("\\", "/")}
            }, timeout=90.0)

            if not sym or "result" not in sym:
                print("FAIL: documentSymbol returned nothing")
                failures.append("documentSymbol empty")
            else:
                result = sym["result"] or []
                print(f"\nPASS  documentSymbol returned {len(result)} symbol(s) from "
                      f"{os.path.basename(TARGET)}")
                names = []
                for s in result[:12]:
                    n = s.get("name", "?")
                    kind = s.get("kind", "?")
                    names.append(n)
                    print(f"        [{kind}] {n}")
                if not result:
                    failures.append("documentSymbol was an empty list")

        if lsp.errors:
            print(f"\nserver stderr ({len(lsp.errors)} line(s)), first 5:")
            for line in lsp.errors[:5]:
                print("   ", line[:160])
    finally:
        lsp.close()

    print("\n" + "=" * 56)
    if failures:
        print(f"RESULT: FAIL -- {failures}")
        return 1
    print("RESULT: PASS -- csharp-ls indexes this project and answers queries")
    return 0


if __name__ == "__main__":
    sys.exit(main())
