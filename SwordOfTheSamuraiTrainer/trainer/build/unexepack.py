#!/usr/bin/env python3
"""Microsoft EXEPACK unpacker -> plain runnable MZ. Validated against the RP packed/unpacked pair."""
import sys, struct

def u16(b, o): return b[o] | (b[o+1] << 8)

def unpack(data):
    assert data[:2] == b'MZ', "not MZ"
    e_cblp   = u16(data, 2)
    e_cp     = u16(data, 4)
    e_crlc   = u16(data, 6)
    e_cparh  = u16(data, 8)
    e_minal  = u16(data, 0x0a)
    e_maxal  = u16(data, 0x0c)
    e_ss     = u16(data, 0x0e)
    e_sp     = u16(data, 0x10)
    e_ip     = u16(data, 0x14)
    e_cs     = u16(data, 0x16)
    hdr_len  = e_cparh * 16
    # loaded image (everything after the MZ header), placed at load_seg:0000
    image = bytearray(data[hdr_len:])
    # exepack var struct is at e_cs:0000 within the loaded image
    vbase = e_cs * 16
    sig = u16(image, vbase + 0x10)
    assert sig == 0x4252, "no 'RB' signature at e_cs:0x10 (got %04x)" % sig
    real_ip   = u16(image, vbase + 0x00)
    real_cs   = u16(image, vbase + 0x02)
    exepk_sz  = u16(image, vbase + 0x06)
    real_sp   = u16(image, vbase + 0x08)
    real_ss   = u16(image, vbase + 0x0a)
    dest_len  = u16(image, vbase + 0x0c)   # paragraphs of decompressed program
    skip_len  = u16(image, vbase + 0x0e)
    # compressed program data = image[0 : vbase]; decompress in place into a dest_len*16 buffer
    comp_len = vbase
    unpacked_len = dest_len * 16
    buf = bytearray(unpacked_len)
    buf[:comp_len] = image[:comp_len]
    # decompress backward
    src = comp_len
    while src > 0 and buf[src-1] == 0xFF:
        src -= 1
    dst = unpacked_len
    while True:
        cmd = buf[src-1]; src -= 1
        length = buf[src-2] | (buf[src-1] << 8); src -= 2
        op = cmd & 0xFE
        if op == 0xB0:       # fill run
            fill = buf[src-1]; src -= 1
            for _ in range(length):
                dst -= 1; buf[dst] = fill
        elif op == 0xB2:     # copy run
            for _ in range(length):
                dst -= 1; src -= 1; buf[dst] = buf[src]
        else:
            raise ValueError("bad exepack cmd 0x%02x at src=%d" % (cmd, src))
        if cmd & 0x01:
            break
    prog = bytes(buf)        # decompressed program image (dest_len*16 bytes)

    # --- packed relocation table: lives in the exepack block after the decompressor code ---
    # block = image[vbase : vbase + exepk_sz]; reloc table is 16 (seg-page) groups: [count][offs...]
    block = image[vbase: vbase + exepk_sz]
    # find reloc table: it is the trailing structure of the block. Parse 16 groups from a probe offset
    # by scanning for a position where exactly 16 groups consume the remainder cleanly.
    relocs = []
    def try_parse(start):
        p = start; out = []
        for page in range(16):
            if p + 2 > len(block): return None
            cnt = block[p] | (block[p+1] << 8); p += 2
            for _ in range(cnt):
                if p + 2 > len(block): return None
                off = block[p] | (block[p+1] << 8); p += 2
                out.append((page * 0x1000, off))
        return (out, p)
    # the reloc table starts right after the decompressor stub. Probe every even offset; accept the
    # parse whose consumed end lands within a few bytes of the block end (allowing the 'corrupt' msg
    # to be outside exepk_sz, or trailing padding).
    best = None
    for start in range(0x12, len(block) - 1, 1):
        r = try_parse(start)
        if r is None: continue
        out, end = r
        # heuristic: plausible reloc count and consumes to near end
        if 0 < len(out) < 20000 and 0 <= len(block) - end <= 0x20:
            best = (out, start, end); break
    if best is None:
        raise ValueError("could not locate reloc table")
    relocs = best[0]

    # --- rebuild a plain MZ ---
    nrel = len(relocs)
    reloc_off = 0x1c
    hdr_bytes = reloc_off + nrel * 4
    hdr_paras = (hdr_bytes + 15) // 16
    hdr_total = hdr_paras * 16
    total = hdr_total + len(prog)
    e_cp_new  = (total + 511) // 512
    e_cblp_new = total % 512
    out = bytearray(hdr_total)
    out[0:2] = b'MZ'
    struct.pack_into('<HHHHHHHHHHHHH', out, 2,
        e_cblp_new, e_cp_new, nrel, hdr_paras,
        e_minal, e_maxal, real_ss, real_sp, 0, real_ip, real_cs, reloc_off, 0)
    p = reloc_off
    for seg, off in relocs:
        struct.pack_into('<HH', out, p, off, seg); p += 4
    out += prog
    return bytes(out), dict(real_cs=real_cs, real_ip=real_ip, real_ss=real_ss, real_sp=real_sp,
                            dest_len=dest_len, nrel=nrel, relocs=relocs, prog=prog)

def reloc_set_from_exe(data):
    e_crlc  = u16(data, 6); e_lfarlc = u16(data, 0x18)
    s = set()
    for i in range(e_crlc):
        off = u16(data, e_lfarlc + i*4); seg = u16(data, e_lfarlc + i*4 + 2)
        s.add((seg, off))
    return s

def prog_image(data):
    return data[u16(data,8)*16:]

if __name__ == '__main__':
    cmd = sys.argv[1]
    if cmd == 'unpack':
        src, dst = sys.argv[2], sys.argv[3]
        out, info = unpack(open(src,'rb').read())
        open(dst,'wb').write(out)
        print("unpacked %s -> %s  (%d bytes, %d relocs, entry %04x:%04x ss:sp %04x:%04x)" %
              (src, dst, len(out), info['nrel'], info['real_cs'], info['real_ip'], info['real_ss'], info['real_sp']))
    elif cmd == 'validate':
        packed, known = sys.argv[2], sys.argv[3]
        out, info = unpack(open(packed,'rb').read())
        kd = open(known,'rb').read()
        my_prog = info['prog']
        kn_prog = prog_image(kd)
        # compare program images (trim to min len)
        n = min(len(my_prog), len(kn_prog))
        prog_ok = my_prog[:n] == kn_prog[:n]
        first_diff = next((i for i in range(n) if my_prog[i]!=kn_prog[i]), -1)
        my_rel = set(info['relocs']); kn_rel = reloc_set_from_exe(kd)
        print("program image: mylen=%d knownlen=%d  match_over_%d=%s  first_diff=%s" %
              (len(my_prog), len(kn_prog), n, prog_ok, hex(first_diff) if first_diff>=0 else "none"))
        print("relocs: mine=%d known=%d  equal=%s  missing=%d extra=%d" %
              (len(my_rel), len(kn_rel), my_rel==kn_rel, len(kn_rel-my_rel), len(my_rel-kn_rel)))
        if my_rel != kn_rel:
            print("  sample missing:", sorted(list(kn_rel-my_rel))[:5])
            print("  sample extra:  ", sorted(list(my_rel-kn_rel))[:5])
