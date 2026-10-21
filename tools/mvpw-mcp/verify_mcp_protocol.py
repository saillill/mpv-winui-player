"""Drive the MCP server over stdio exactly as a client would: JSON-RPC lines in,
JSON-RPC lines out. Proves the protocol layer works, not just the tool functions.

Run:  python tools/mvpw-mcp/verify_mcp_protocol.py
"""
import json
import subprocess
import sys
import os

SERVER = os.path.join(os.path.dirname(os.path.abspath(__file__)), "mcpw_server.py")


def main():
    proc = subprocess.Popen(
        [sys.executable, SERVER],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        text=True, encoding="utf-8", bufsize=1,
    )

    def rpc(msg):
        proc.stdin.write(json.dumps(msg) + "\n")
        proc.stdin.flush()
        line = proc.stdout.readline()
        if not line:
            err = proc.stderr.read()
            raise SystemExit("server closed the stream; stderr:\n" + err)
        return json.loads(line)

    failures = []

    def check(label, cond, detail=""):
        print(("  PASS  " if cond else "  FAIL  ") + label + ("" if cond else "  " + detail))
        if not cond:
            failures.append(label)

    print("== MCP protocol ==")

    r = rpc({"jsonrpc": "2.0", "id": 1, "method": "initialize",
             "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                        "clientInfo": {"name": "verify", "version": "0"}}})
    check("initialize returns serverInfo",
          r.get("result", {}).get("serverInfo", {}).get("name") == "mvpw-mcp", json.dumps(r))

    proc.stdin.write(json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}) + "\n")
    proc.stdin.flush()

    r = rpc({"jsonrpc": "2.0", "id": 2, "method": "tools/list"})
    tools = {t["name"] for t in r.get("result", {}).get("tools", [])}
    required = {"launch_app", "get_ui_snapshot", "invoke_control",
                "assert_element_state", "screenshot_region",
                "read_playback_state", "trigger_screenshot"}
    check("tools/list exposes the seven required tools",
          required <= tools, "missing " + str(sorted(required - tools)))
    print("        tools: " + ", ".join(sorted(tools)))

    # A tool call that must fail cleanly (no app running) and report isError.
    r = rpc({"jsonrpc": "2.0", "id": 3, "method": "tools/call",
             "params": {"name": "read_playback_state", "arguments": {}}})
    res = r.get("result", {})
    check("tool call with no app returns isError (not a crash)",
          res.get("isError") is True, json.dumps(r)[:300])

    # An actual round trip: launch, snapshot, assert, close.
    r = rpc({"jsonrpc": "2.0", "id": 4, "method": "tools/call",
             "params": {"name": "launch_app", "arguments": {"wait_seconds": 25}}})
    res = r.get("result", {})
    ok_launch = res.get("isError") is False
    check("launch_app via MCP", ok_launch, json.dumps(r)[:300])

    if ok_launch:
        r = rpc({"jsonrpc": "2.0", "id": 5, "method": "tools/call",
                 "params": {"name": "assert_element_state",
                            "arguments": {"automation_id": "ShellNav_music",
                                          "exists": True, "enabled": True}}})
        body = r.get("result", {}).get("content", [{}])[0].get("text", "{}")
        passed = json.loads(body).get("passed") is True
        check("assert_element_state(ShellNav_music) via MCP", passed, body[:300])

        r = rpc({"jsonrpc": "2.0", "id": 6, "method": "tools/call",
                 "params": {"name": "invoke_control",
                            "arguments": {"automation_id": "ShellNav_music",
                                          "action": "select"}}})
        check("invoke_control(ShellNav_music, select) via MCP",
              r.get("result", {}).get("isError") is False, json.dumps(r)[:300])

        r = rpc({"jsonrpc": "2.0", "id": 7, "method": "tools/call",
                 "params": {"name": "get_ui_snapshot",
                            "arguments": {"max_depth": 5}}})
        body = r.get("result", {}).get("content", [{}])[0].get("text", "{}")
        snap = json.loads(body)
        check("get_ui_snapshot via MCP returns elements with ids",
              snap.get("count", 0) > 0 and any(e["automationId"] for e in snap["elements"]),
              "count=%s" % snap.get("count"))

        r = rpc({"jsonrpc": "2.0", "id": 8, "method": "tools/call",
                 "params": {"name": "close_app", "arguments": {}}})
        check("close_app via MCP", r.get("result", {}).get("isError") is False,
              json.dumps(r)[:200])

    r = rpc({"jsonrpc": "2.0", "id": 9, "method": "no/such/method"})
    check("unknown method returns a JSON-RPC error",
          r.get("error", {}).get("code") == -32601, json.dumps(r)[:200])

    proc.stdin.close()
    proc.wait(timeout=20)

    print("\n%s (%d failure(s))" % ("OK" if not failures else "FAILED", len(failures)))
    return 0 if not failures else 1


if __name__ == "__main__":
    sys.exit(main())
