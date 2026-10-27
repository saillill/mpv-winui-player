#!/usr/bin/env python3
"""MCP server (stdio, JSON-RPC 2.0) exposing mpvw's UI to an agent.

Protocol: Model Context Protocol 2024-11-05 over stdio. One JSON object per
line on stdin, one per line on stdout; logs go to stderr so they never corrupt
the stream.

Tools are the seven the task names:
    launch_app, get_ui_snapshot, invoke_control, assert_element_state,
    screenshot_region, read_playback_state, trigger_screenshot

Register in a client (claude_desktop_config.json / DSH mcpServers). Use an absolute
path to *this file* in your own checkout -- the deploy directory is resolved by
`paths.py` from the file's location, so no other path needs configuring:

    "mvpw": {
      "command": "python",
      "args": ["<your-checkout>/tools/mvpw-mcp/mcpw_server.py"]
    }
"""
from __future__ import annotations

import json
import sys
import traceback

import mpvw_tools as T

PROTOCOL = "2024-11-05"
SERVER = {"name": "mvpw-mcp", "version": "0.1.0"}

TOOLS = [
    {
        "name": "launch_app",
        "description": "Start mpvw.exe from the deploy directory and wait for its main window. Refuses if an instance is already running.",
        "inputSchema": {
            "type": "object",
            "properties": {"wait_seconds": {"type": "integer", "default": 20}},
        },
    },
    {
        "name": "close_app",
        "description": "Close mpvw.exe gracefully (WM_CLOSE so mpv flushes state), force-kill only if it will not answer.",
        "inputSchema": {"type": "object", "properties": {}},
    },
    {
        "name": "get_ui_snapshot",
        "description": "UI Automation tree of the running mpvw: AutomationId, control type, enabled, rectangle. Names are redacted by default because they are the user's media titles.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "max_depth": {"type": "integer", "default": 6},
                "redact_names": {"type": "boolean", "default": True},
                "include_empty_ids": {"type": "boolean", "default": False},
            },
        },
    },
    {
        "name": "invoke_control",
        "description": "Act on a control found by AutomationId: invoke | select | expand | collapse | toggle | click. Throws if the pattern is unavailable rather than silently doing nothing.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "automation_id": {"type": "string"},
                "action": {
                    "type": "string",
                    "enum": ["invoke", "select", "expand", "collapse", "toggle", "click"],
                    "default": "invoke",
                },
            },
            "required": ["automation_id"],
        },
    },
    {
        "name": "assert_element_state",
        "description": "Assert one control's state (existence, enabled, visible, name). Returns passed/problems instead of throwing, so a test can report several assertions.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "automation_id": {"type": "string"},
                "exists": {"type": "boolean", "default": True},
                "enabled": {"type": "boolean"},
                "visible": {"type": "boolean"},
                "name_equals": {"type": "string"},
                "name_contains": {"type": "string"},
            },
            "required": ["automation_id"],
        },
    },
    {
        "name": "screenshot_region",
        "description": "Capture the main window, one element (by AutomationId), or an explicit [left, top, right, bottom] rectangle to a PNG. For diagnosis and visual diff, not as the routine pass/fail path.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "path": {"type": "string"},
                "automation_id": {"type": "string"},
                "rect": {"type": "array", "items": {"type": "integer"}},
            },
            "required": ["path"],
        },
    },
    {
        "name": "read_playback_state",
        "description": "Read mpv state over \\\\.\\pipe\\mpvpipe: pause, time-pos, duration, volume, mute, speed, filename, media-title, idle-active, playlist-count. A null means mpv answered 'property unavailable'.",
        "inputSchema": {"type": "object", "properties": {}},
    },
    {
        "name": "trigger_screenshot",
        "description": "Ask mpv itself to write a screenshot (screenshot-to-file), defaulting to <deploy>/screenshots/mpvw-<timestamp>.<fmt>.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "format": {"type": "string", "default": "png"},
                "path": {"type": "string"},
            },
        },
    },
]

DISPATCH = {
    "launch_app": T.launch_app,
    "close_app": T.close_app,
    "get_ui_snapshot": T.get_ui_snapshot,
    "invoke_control": T.invoke_control,
    "assert_element_state": T.assert_element_state,
    "screenshot_region": T.screenshot_region,
    "read_playback_state": T.read_playback_state,
    "trigger_screenshot": T.trigger_screenshot,
}


def log(msg: str) -> None:
    print(f"[mvpw-mcp] {msg}", file=sys.stderr, flush=True)


def reply(msg_id, result=None, error=None):
    out = {"jsonrpc": "2.0", "id": msg_id}
    if error is not None:
        out["error"] = error
    else:
        out["result"] = result
    sys.stdout.write(json.dumps(out, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def handle(msg: dict):
    method = msg.get("method")
    msg_id = msg.get("id")

    # Notifications carry no id and take no reply.
    if msg_id is None and method and method.startswith("notifications/"):
        return

    if method == "initialize":
        reply(msg_id, {
            "protocolVersion": PROTOCOL,
            "capabilities": {"tools": {"listChanged": False}},
            "serverInfo": SERVER,
        })
        return

    if method == "tools/list":
        reply(msg_id, {"tools": TOOLS})
        return

    if method == "tools/call":
        params = msg.get("params") or {}
        name = params.get("name")
        args = params.get("arguments") or {}
        fn = DISPATCH.get(name)
        if fn is None:
            reply(msg_id, error={"code": -32601, "message": f"unknown tool {name}"})
            return
        try:
            payload = fn(**args)
            reply(msg_id, {
                "content": [{"type": "text", "text": json.dumps(payload, ensure_ascii=False, indent=1)}],
                "isError": False,
            })
        except TypeError as e:
            reply(msg_id, {
                "content": [{"type": "text", "text": f"bad arguments for {name}: {e}"}],
                "isError": True,
            })
        except Exception as e:
            log(traceback.format_exc())
            reply(msg_id, {
                "content": [{"type": "text", "text": f"{type(e).__name__}: {e}"}],
                "isError": True,
            })
        return

    if method == "ping":
        reply(msg_id, {})
        return

    if msg_id is not None:
        reply(msg_id, error={"code": -32601, "message": f"unknown method {method}"})


def main() -> None:
    log("ready on stdio")
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            msg = json.loads(line)
        except Exception as e:
            log(f"unparseable line: {e}")
            continue
        try:
            handle(msg)
        except Exception:
            log(traceback.format_exc())


if __name__ == "__main__":
    main()
