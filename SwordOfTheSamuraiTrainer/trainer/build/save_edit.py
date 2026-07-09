#!/usr/bin/env python3
"""Sword of the Samurai save-game editor  (talltale.dat)

The save is a 7650-byte image of the game's state block. It holds an array of 0x60-byte
character records starting at 0x6F: record 0 is YOU, the rest are other daimyo. Every
attribute/honor/land field is a 0-128 (0x80) scaled value; money and army size are DERIVED
from these, so you raise the scaled fields, not a "koku" number.

Verified field map (offsets relative to a record start R):
  R+0x30  given-name index   (DO NOT touch -> changes the character's name)
  R+0x32  family-name index  (records sharing YOUR value are your KIN)
  R+0x33  age / life-stage   (0=youth, 1=mature adult, 2=old; higher=older)
  R+0x3A .. R+0x58  stat cluster + economic fields  (the 0-128 values to edit)
Safe rule: only rewrite bytes currently valued 2..128, only in R+0x3A..R+0x58.
That leaves names, clan markers, padding, and record delimiters untouched.
The age byte (R+0x33) sits OUTSIDE that range, so max-me/min never touch it;
edit it only with the dedicated age/youth-me commands.

Commands:
  list  <save>                          show every record: age + stats + YOU/kin/other
  max-me <save> <out>                   max your stats (record 0) -> 0x80
  max   <save> <out> <rec#> [rec#...]   max the given records' stats
  min   <save> <out> <rec#> [rec#...]   cripple the given records' stats -> 1
  youth-me <save> <out>                 reset YOU (record 0) to youth (age 0)
  age   <save> <out> <rec#> <stage>     set a record's age (youth/mature/old or 0/1/2)
  set   <save> <out> <off> <val> [size] write a raw byte/word (advanced)
  dump  <save> <off> <len>              hex + ascii view
  find  <save> <value>                  locate a value (u8 / u16le / u16be)
  diff  <saveA> <saveB>                 word-level differences

Always writes to <out> (a copy); never edit your only save in place. Keep a backup.
"""
import sys

REC0   = 0x6F          # first (player) record
STRIDE = 0x60          # record size
STAT_LO, STAT_HI = 0x3A, 0x58   # editable span within a record (relative)
NAME_OFF, FAMILY_OFF = 0x30, 0x32
AGE_OFF = 0x33         # life-stage byte: 0=youth 1=mature adult 2=old (higher=older)
MAXV, MINV = 0x80, 1

# confirmed labels (Christopher=youth=0, Chomei=mature=1, Yukinaga=old=2)
AGE_LABELS  = {0: "youth", 1: "mature adult", 2: "old"}
AGE_ALIASES = {"youth": 0, "young": 0,
               "mature": 1, "adult": 1, "mature-adult": 1, "mature adult": 1,
               "old": 2, "aged": 2}

def parse_age(s):
    """Accept a stage name (youth/mature/old...) or a raw number (0/1/2)."""
    k = str(s).strip().lower()
    if k in AGE_ALIASES: return AGE_ALIASES[k]
    return int(k, 0)

def age_label(v): return AGE_LABELS.get(v, f"stage {v}")

def load(p): return bytearray(open(p, "rb").read())
def u16(b, o): return b[o] | (b[o+1] << 8)

def find_records(b):
    """Character records are a contiguous 0x60-byte array from 0x6F up to the name table.
    We locate the name table (first run of >=4 ASCII letters at/after 0x2A0 -- the player's
    name) and take every whole record before it. This is stable: it never inspects the stat
    fields, so it works the same before and after editing."""
    import re
    # stat/name fields store their value byte followed by 0x00, so real data never yields a
    # run of >=4 consecutive ASCII letters -- only the plaintext name table does.
    m = re.search(rb'[A-Za-z]{4,}', bytes(b[0x100:]))
    nt = 0x100 + m.start() if m else len(b)
    recs, i = [], 0
    while REC0 + (i + 1) * STRIDE <= nt:
        recs.append(REC0 + i * STRIDE); i += 1
    return recs

def classify(b, recs):
    fam0 = b[recs[0]+FAMILY_OFF]
    out = []
    for i, rs in enumerate(recs):
        if i == 0: role = "YOU"
        elif b[rs+FAMILY_OFF] == fam0: role = "kin"
        else: role = "other"
        out.append(role)
    return out

def stats(b, rs): return [b[rs+STAT_LO+2*k] for k in range(6)]

def edit_record(b, rs, target):
    n = 0
    for o in range(rs+STAT_LO, rs+STAT_HI):
        if 2 <= b[o] <= 128 and b[o] != target:
            b[o] = target; n += 1
    return n

def cmd_list(save):
    b = load(save); recs = find_records(b); roles = classify(b, recs)
    print(f"{save}: {len(recs)} character records\n")
    print(" #  offset  role   name-idx family-idx  age               stat cluster (+0x3A..)")
    for i, rs in enumerate(recs):
        a = b[rs+AGE_OFF]
        age = f"{a} ({age_label(a)})"
        print(f"{i:2}  0x{rs:04x}  {roles[i]:5}  {b[rs+NAME_OFF]:5}   {b[rs+FAMILY_OFF]:5}      {age:16}  {stats(b,rs)}")
    print("\nYOU = record 0 (your samurai).  kin = same family index as you (leave alone).")
    print("Weaken the 'other' records that are your rivals (verify names in-game).")

def _write(save, out, mutate):
    b = load(save); recs = find_records(b)
    mutate(b, recs)
    open(out, "wb").write(b)
    print(f"wrote {out} ({len(b)} bytes, size {'OK' if len(b)==7650 else 'CHANGED!'})")

def cmd_maxme(save, out):
    def m(b, recs): print("max record 0 (YOU):", edit_record(b, recs[0], MAXV), "bytes -> 0x80")
    _write(save, out, m)

def cmd_max(save, out, *nums):
    def m(b, recs):
        for n in nums:
            n=int(n); print(f"max record {n}:", edit_record(b, recs[n], MAXV), "bytes -> 0x80")
    _write(save, out, m)

def cmd_min(save, out, *nums):
    def m(b, recs):
        for n in nums:
            n=int(n); print(f"min record {n}:", edit_record(b, recs[n], MINV), "bytes -> 1")
    _write(save, out, m)

def cmd_youthme(save, out):
    def m(b, recs):
        rs = recs[0]; old = b[rs+AGE_OFF]; b[rs+AGE_OFF] = 0
        print(f"record 0 (YOU): age {old} ({age_label(old)}) -> 0 (youth)")
    _write(save, out, m)

def cmd_age(save, out, rec, stage):
    rec = int(rec); v = parse_age(stage) & 0xff
    def m(b, recs):
        rs = recs[rec]; old = b[rs+AGE_OFF]; b[rs+AGE_OFF] = v
        print(f"record {rec}: age {old} ({age_label(old)}) -> {v} ({age_label(v)})")
    _write(save, out, m)

def cmd_set(save, out, off, val, size="1"):
    b=load(save); off=int(off,0); val=int(val,0); size=int(size)
    b[off]=val&0xff
    if size==2: b[off+1]=(val>>8)&0xff
    open(out,"wb").write(b); print(f"wrote {out}: 0x{off:04x} = {val}")

def cmd_dump(save, off, ln):
    b=load(save); off=int(off,0); ln=int(ln,0)
    for i in range(off, off+ln, 16):
        row=b[i:i+16]
        print(f"0x{i:04x}: " + " ".join(f"{x:02x}" for x in row) + "  " +
              "".join(chr(x) if 32<=x<127 else "." for x in row))

def cmd_find(save, value):
    b=load(save); v=int(value,0); hits=[]
    for o in range(len(b)-1):
        if b[o]==v and v<256: hits.append((o,"u8"))
        if u16(b,o)==v: hits.append((o,"u16le"))
        if (b[o]<<8|b[o+1])==v: hits.append((o,"u16be"))
    print(f"{v} (0x{v:x}): {len(hits)} hits"); [print(f"  0x{o:04x} {t}") for o,t in hits[:200]]

def cmd_diff(a, b):
    x=load(a); y=load(b); n=min(len(x),len(y))
    for o in range(0,n,2):
        if x[o:o+2]!=y[o:o+2]:
            print(f"  0x{o:04x}: {u16(x,o)} (0x{u16(x,o):04x})  ->  {u16(y,o)} (0x{u16(y,o):04x})")

CMDS={"list":cmd_list,"max-me":cmd_maxme,"max":cmd_max,"min":cmd_min,
      "youth-me":cmd_youthme,"age":cmd_age,
      "set":cmd_set,"dump":cmd_dump,"find":cmd_find,"diff":cmd_diff}

if __name__=="__main__":
    if len(sys.argv)<2 or sys.argv[1] not in CMDS:
        print(__doc__); sys.exit(0)
    CMDS[sys.argv[1]](*sys.argv[2:])
