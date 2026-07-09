#!/usr/bin/env python3
"""Deploy/restore the SOTS trainer (patched unpacked EXEs) into the live Game dir.
Always backs up an original to <name>.orig ONCE (never overwrites an existing .orig).
Usage:
  deploy.py backup  <game_dir> <EXE> [<EXE>...]          # make .orig backups if absent
  deploy.py install <game_dir> <patched_dir> <EXE>[...]  # backup then copy patched over original
  deploy.py restore <game_dir> <EXE> [<EXE>...]          # copy .orig back over current
  deploy.py status  <game_dir> <EXE> [<EXE>...]
"""
import sys, os, shutil, hashlib

def sha(p):
    try:
        return hashlib.sha256(open(p, "rb").read()).hexdigest()[:12]
    except FileNotFoundError:
        return "(absent)"

def main():
    cmd, game = sys.argv[1], sys.argv[2]
    if cmd in ("backup", "restore", "status"):
        exes = sys.argv[3:]
    elif cmd == "install":
        patched = sys.argv[3]; exes = sys.argv[4:]
    for e in exes:
        live = os.path.join(game, e)
        orig = live + ".orig"
        if cmd == "status":
            print(f"{e:16} live={sha(live)}  orig={sha(orig)}")
        elif cmd == "backup":
            if os.path.exists(orig):
                print(f"{e}: .orig already exists ({sha(orig)}) - keeping")
            else:
                shutil.copy2(live, orig); print(f"{e}: backed up -> {e}.orig ({sha(orig)})")
        elif cmd == "restore":
            if os.path.exists(orig):
                shutil.copy2(orig, live); print(f"{e}: restored from .orig ({sha(live)})")
            else:
                print(f"{e}: no .orig to restore!")
        elif cmd == "install":
            if not os.path.exists(orig):
                shutil.copy2(live, orig); print(f"{e}: backed up -> {e}.orig ({sha(orig)})")
            src = os.path.join(patched, e)
            shutil.copy2(src, live)
            print(f"{e}: installed patched ({sha(live)}) over original (orig kept {sha(orig)})")

if __name__ == "__main__":
    main()
