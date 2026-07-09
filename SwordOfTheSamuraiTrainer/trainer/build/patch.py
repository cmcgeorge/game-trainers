#!/usr/bin/env python3
"""Safety-checked byte patcher for the SOTS trainer.
Patch spec is a JSON list of {off, orig, new, note}. 'orig'/'new' are hex byte strings.
Every 'orig' is verified against the file before any write; mismatch aborts with no changes.
Usage:
  patch.py apply  <in.exe> <out.exe> <patches.json>
  patch.py verify <exe> <patches.json>          # check orig bytes match (no write)
  patch.py show   <exe> <off> <len>             # hexdump a region
"""
import sys, json

def hb(s): return bytes.fromhex(s.replace(" ", ""))

def load(path): return bytearray(open(path, "rb").read())

def check(buf, patches):
    errs = []
    for p in patches:
        off = p["off"] if isinstance(p["off"], int) else int(p["off"], 0)
        orig = hb(p["orig"])
        got = bytes(buf[off:off+len(orig)])
        ok = got == orig
        print(("  OK  " if ok else " FAIL ") + f"@0x{off:06x} expect {orig.hex()} got {got.hex()}  {p.get('note','')}")
        if not ok: errs.append(off)
    return errs

def apply(buf, patches):
    for p in patches:
        off = p["off"] if isinstance(p["off"], int) else int(p["off"], 0)
        new = hb(p["new"])
        buf[off:off+len(new)] = new

def main():
    cmd = sys.argv[1]
    if cmd == "show":
        buf = load(sys.argv[2]); off = int(sys.argv[3], 0); n = int(sys.argv[4], 0)
        for i in range(off, off+n, 16):
            row = buf[i:i+16]
            print(f"{i:06x}: " + " ".join(f"{b:02x}" for b in row) + "  " +
                  "".join(chr(b) if 32 <= b < 127 else "." for b in row))
        return
    buf = load(sys.argv[2])
    patches = json.load(open(sys.argv[-1]))
    print(f"# {cmd}: {sys.argv[2]}  ({len(patches)} patches)")
    errs = check(buf, patches)
    if cmd == "verify":
        print("ALL OK" if not errs else f"{len(errs)} MISMATCH(es)"); return
    if cmd == "apply":
        if errs:
            print("ABORT: orig mismatch, nothing written"); sys.exit(1)
        apply(buf, patches)
        out = sys.argv[3]
        open(out, "wb").write(buf)
        print(f"wrote {out} ({len(buf)} bytes)")

if __name__ == "__main__":
    main()
