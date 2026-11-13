#!/usr/bin/env python3
# usage: ildump.py <assembly.dll> <TypeName> [method-substring ...]
# Dumps the CIL of a type's methods with field/method tokens resolved to names.
import sys
import dnfile
from dnfile.enums import MetadataTables
from dncil.cil.body import CilMethodBody
from dncil.cil.body.reader import CilMethodBodyReaderBase
from dncil.cil.error import MethodBodyFormatError
from dncil.clr.token import Token, StringToken, InvalidToken


class Reader(CilMethodBodyReaderBase):
    def __init__(self, pe, rva):
        self.pe = pe
        self.offset = pe.get_offset_from_rva(rva)

    def read(self, n):
        data = self.pe.get_data(self.pe.get_rva_from_offset(self.offset), n)
        self.offset += n
        return data

    def tell(self):
        return self.offset

    def seek(self, off):
        self.offset = off
        return off


pe = dnfile.dnPE(sys.argv[1])
md = pe.net.mdtables
strs = pe.net.user_strings


def typename(t):
    try:
        return f"{t.TypeNamespace}.{t.TypeName}".lstrip(".")
    except Exception:
        return "?"


def resolve(tok):
    if isinstance(tok, StringToken):
        try:
            return '"' + strs.get(tok.rid).value + '"'
        except Exception:
            return "str?"
    table = tok.table
    rid = tok.rid
    try:
        if table == MetadataTables.Field.value:
            f = md.Field[rid - 1]
            return f"field {f.Name}"
        if table == MetadataTables.MethodDef.value:
            m = md.MethodDef[rid - 1]
            return f"method {m.Name}"
        if table == MetadataTables.MemberRef.value:
            m = md.MemberRef[rid - 1]
            parent = m.Class.row
            pn = getattr(parent, "TypeName", None) or getattr(parent, "Name", "?")
            return f"memberref {pn}::{m.Name}"
        if table == MetadataTables.TypeDef.value:
            return "type " + typename(md.TypeDef[rid - 1])
        if table == MetadataTables.TypeRef.value:
            return "typeref " + typename(md.TypeRef[rid - 1])
        if table == MetadataTables.MethodSpec.value:
            ms = md.MethodSpec[rid - 1]
            r = ms.Method.row
            return f"methodspec {getattr(r, 'Name', '?')}"
    except Exception as e:
        return f"tok?{table}:{rid}"
    return f"tok{table}:{rid}"


want_type = sys.argv[2]
subs = sys.argv[3:]
for t in md.TypeDef:
    if t.TypeName != want_type:
        continue
    print("== type", typename(t))
    for f in t.FieldList:
        print("  field", f.row.Name)
    for mi in t.MethodList:
        m = mi.row
        if subs and not any(s.lower() in str(m.Name).lower() for s in subs):
            continue
        if not m.Rva:
            continue
        try:
            body = CilMethodBody(Reader(pe, m.Rva))
        except MethodBodyFormatError as e:
            print("  bad body", m.Name, e)
            continue
        print(f"\n-- {m.Name}")
        for ins in body.instructions:
            op = ins.operand
            if isinstance(op, Token):
                s = resolve(op)
            elif isinstance(op, list):
                s = str([hex(x) if isinstance(x, int) else x for x in op])
            else:
                s = "" if op is None else str(op)
            print(f"  {ins.offset:05x} {str(ins.opcode):12s} {s}")
