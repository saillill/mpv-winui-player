#!/usr/bin/env python3
"""Read a Windows minidump: exception record, module map, crash-thread stack.

Why this exists
---------------
When this app crashes there is no dialog and no log line. The only record is a
minidump under %LOCALAPPDATA%\\CrashDumps. Setting up a debugger for one crash
is slower than reading the three things that identify it:

  * the exception code -- 0xC000027B is STATUS_STOWED_EXCEPTION, which is how
    WinUI reports an exception raised inside a layout/render callback. It is
    NOT a managed crash, and it is NOT the CLR reporting an unhandled throw;
  * the faulting address, resolved to module+offset;
  * the faulting thread's stack, resolved to module+offset.

That combination has been enough to tell a repeat of a known crash from a new
one, and to see which subsystem (XAML core, Direct2D, combase) was live.

Usage
-----
    python tools/read-dump.py path/to/app.exe.NNNN.dmp [more.dmp ...]

Prints, per dump: exception code/flags/address, the exception parameters, the
crash thread's RIP/RSP, and the first frames of its stack that land inside a
known module. Frames in ntdll/KERNELBASE/ucrtbase are the reporting machinery
rather than the cause, so they are listed but not highlighted.

Notes on the format (learned the hard way)
------------------------------------------
* MINIDUMP_EXCEPTION_STREAM is 8 bytes of header (thread id + alignment) then a
  152-byte MINIDUMP_EXCEPTION, then the thread-context descriptor. Reading the
  exception at the stream start yields garbage -- the +8 matters.
* MINIDUMP_MODULE is 108 bytes and the name is at +20 as an RVA to a
  MINIDUMP_STRING (a 4-byte byte-length followed by UTF-16). Guessing the name
  offset silently produces empty names and every frame resolves to "?".
* The stowed-exception payload a 0xC000027B points at frequently sits in a
  region the dump did not capture, so the nested error text is often simply not
  present. The stack is the reliable part; do not over-invest in the payload.
"""

import struct
import sys
from pathlib import Path

REPORTING_MODULES = ("ntdll", "KERNELBASE", "ucrtbase", "combase", "RPCRT4")

STOWED_EXCEPTION = 0xC000027B


def load(path):
    data = Path(path).read_bytes()
    if data[:4] != b"MDMP":
        raise ValueError(f"{path}: not a minidump")

    stream_count = struct.unpack_from("<I", data, 8)[0]
    dir_rva = struct.unpack_from("<I", data, 12)[0]

    streams = {}
    for i in range(stream_count):
        off = dir_rva + i * 12
        stype, size, rva = struct.unpack_from("<III", data, off)
        streams.setdefault(stype, []).append((size, rva))

    modules = read_modules(data, streams)
    return data, streams, modules


def read_modules(data, streams):
    modules = []
    for size, rva in streams.get(4, []):
        count = struct.unpack_from("<I", data, rva)[0]
        for i in range(count):
            entry = rva + 4 + i * 108
            base, image_size = struct.unpack_from("<QI", data, entry)
            name_rva = struct.unpack_from("<I", data, entry + 20)[0]
            name = ""
            if name_rva and name_rva + 4 < len(data):
                chars = struct.unpack_from("<I", data, name_rva)[0]
                raw = data[name_rva + 4: name_rva + 4 + chars]
                try:
                    name = raw.decode("utf-16-le")
                except UnicodeDecodeError:
                    name = ""
            if name:
                modules.append((base, image_size, name))
    return modules


def resolve(modules, addr):
    for base, size, name in modules:
        if base <= addr < base + size:
            return f"{Path(name).name}+0x{addr - base:X}"
    return None


def read_exception(data, streams):
    if 6 not in streams:
        return None
    _, rva = streams[6][0]
    thread_id = struct.unpack_from("<I", data, rva)[0]
    exc = rva + 8
    code, flags = struct.unpack_from("<II", data, exc)
    address = struct.unpack_from("<Q", data, exc + 16)[0]
    count = struct.unpack_from("<I", data, exc + 24)[0]
    params = struct.unpack_from("<15Q", data, exc + 32)
    return thread_id, code, flags, address, count, params


def memory_ranges(data, streams):
    ranges = []
    for _, rva in streams.get(5, []):
        count = struct.unpack_from("<I", data, rva)[0]
        for i in range(count):
            entry = rva + 4 + i * 16
            start = struct.unpack_from("<Q", data, entry)[0]
            size = struct.unpack_from("<I", data, entry + 8)[0]
            file_rva = struct.unpack_from("<I", data, entry + 12)[0]
            ranges.append((start, size, file_rva))
    ranges.sort()
    return ranges


def read_virtual(data, ranges, addr, length):
    for start, size, file_rva in ranges:
        if start <= addr < start + size:
            take = min(length, start + size - addr)
            at = file_rva + (addr - start)
            return data[at: at + take]
    return b""


def thread_context(data, streams, thread_id):
    for _, rva in streams.get(3, []):
        count = struct.unpack_from("<I", data, rva)[0]
        for i in range(count):
            entry = rva + 4 + i * 48
            (tid, _suspend, _prio_c, _prio, _teb,
             _stack_start, _stack_size, _stack_rva,
             _ctx_size, ctx_rva) = struct.unpack_from("<IIIIQQIIII", data, entry)
            if tid == thread_id:
                return ctx_rva
    return None


def report(path):
    data, streams, modules = load(path)
    print(f"===== {Path(path).name} =====")

    exc = read_exception(data, streams)
    if exc is None:
        print("  no exception stream")
        return

    thread_id, code, flags, address, count, params = exc
    print(f"  exception   : 0x{code:08X}"
          + ("  (STATUS_STOWED_EXCEPTION: WinUI escalated an exception raised"
             " inside a layout/render callback)" if code == STOWED_EXCEPTION else ""))
    print(f"  flags       : 0x{flags:08X}")
    print(f"  address     : 0x{address:016X}  {resolve(modules, address) or '?'}")
    print(f"  thread      : {thread_id}")
    for i in range(min(count, 4)):
        param = params[i]
        where = resolve(modules, param) if param > 0x10000 else ""
        print(f"  param[{i}]    : 0x{param:016X}  {where}")

    ctx_rva = thread_context(data, streams, thread_id)
    if ctx_rva is None:
        print("  no context for the crash thread")
        return

    rip = struct.unpack_from("<Q", data, ctx_rva + 0xF8)[0]
    rsp = struct.unpack_from("<Q", data, ctx_rva + 0x98)[0]
    rbp = struct.unpack_from("<Q", data, ctx_rva + 0xA0)[0]
    print(f"  RIP         : 0x{rip:016X}  {resolve(modules, rip) or '?'}")
    print(f"  RSP         : 0x{rsp:016X}")
    print(f"  RBP         : 0x{rbp:016X}")

    ranges = memory_ranges(data, streams)
    print("  stack (module frames, nearest first; reporting machinery tagged):")
    # A stack scan, not a real unwind. Without .pdata access the return
    # addresses cannot be walked properly, but every frame leaves its return
    # address somewhere in [RSP, RBP), and reading the whole window picks them
    # up. Scanning only a few words past RSP misses everything except the
    # reporting frames, which is what the first version of this did.
    addr = rsp
    limit = max(rbp, rsp) + 0x600
    seen = set()
    while addr < limit:
        raw = read_virtual(data, ranges, addr, 8)
        if len(raw) == 8:
            value = struct.unpack("<Q", raw)[0]
            where = resolve(modules, value)
            if where and where not in seen:
                seen.add(where)
                tag = ("   <- reporting machinery"
                       if where.split(".")[0] in REPORTING_MODULES else "")
                print(f"      0x{value:016X}  {where}{tag}")
                if len(seen) >= 22:
                    break
        addr += 8


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    for path in sys.argv[1:]:
        report(path)
        print()
    return 0


if __name__ == "__main__":
    sys.exit(main())
