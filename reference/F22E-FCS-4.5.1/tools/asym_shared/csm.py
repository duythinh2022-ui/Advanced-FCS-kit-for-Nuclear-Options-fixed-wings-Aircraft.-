# csm.py - class-body member scanner for the FCS controllers (one class per file, namespace { class { members } })
# members(text) -> list of Member(names, kind, start, end) with line ranges [start, end) that include the leading
# comment / attribute block. kind: 'method' | 'field' | 'prop' | 'other'.
import re

class Member:
    __slots__ = ("names", "kind", "start", "decl", "end", "text")
    def __init__(s, names, kind, start, decl, end, text):
        s.names, s.kind, s.start, s.decl, s.end, s.text = names, kind, start, decl, end, text
    def __repr__(s):
        return f"<{s.kind} {','.join(s.names)} {s.start}-{s.end}>"

def _strip(line, state):
    """remove comments and string/char contents from one line; state carries block-comment across lines"""
    out = []; i = 0; n = len(line)
    while i < n:
        c = line[i]
        if state["blk"]:
            if line.startswith("*/", i): state["blk"] = False; i += 2
            else: i += 1
            continue
        if line.startswith("//", i): break
        if line.startswith("/*", i): state["blk"] = True; i += 2; continue
        if c == '"' or (c in "$@" and i + 1 < n and line[i + 1] == '"') or (c == '$' and line.startswith('$@"', i)):
            # string (interpolated strings: keep braces out; interpolation holes do not change brace balance in practice)
            j = line.index('"', i) + 1
            verb = '@' in line[i:j]
            while j < n:
                if verb and line.startswith('""', j): j += 2; continue
                if not verb and line[j] == '\\': j += 2; continue
                if line[j] == '"': j += 1; break
                j += 1
            out.append('""'); i = j; continue
        if c == "'":
            j = i + 1
            while j < n and line[j] != "'":
                j += 2 if line[j] == '\\' else 1
            out.append("' '"); i = j + 1; continue
        out.append(c); i += 1
    return "".join(out)

def _split_top(s, sep=","):
    parts = []; d = 0; cur = []
    for ch in s:
        if ch in "<([{": d += 1
        elif ch in ">)]}": d -= 1
        if ch == sep and d == 0: parts.append("".join(cur)); cur = []
        else: cur.append(ch)
    parts.append("".join(cur)); return parts

MODS = r'(?:public|private|internal|protected|static|readonly|const|unsafe|override|sealed|virtual|extern|new|volatile|abstract|partial)'

def _names(code):
    c = re.sub(r'\[[^\]]*\]\s*', '', code.strip(), count=0) if code.strip().startswith('[') else code.strip()
    c = c.strip()
    m = re.match(r'^(?:' + MODS + r'\s+)*(.*)$', c, re.S)
    body = m.group(1)
    if re.match(r'^(class|struct|enum|interface|delegate)\b', body):
        nm = re.match(r'^\w+\s+(\w+)', body); return ([nm.group(1)] if nm else []), 'other'
    cm = re.match(r'^(\w+)\s*\(', body)
    if cm: return [cm.group(1)], 'method'
    bp = re.match(r'^[\w<>\[\],\. ?]+?\s(\w+)\s*$', body.split('{')[0].strip()) if '=' not in body.split('{')[0] else None
    if bp and '{' in code: return [bp.group(1)], 'prop'
    # method: <type> Name(  or Name<T>(  ; operator/ctor ignored
    mm = re.match(r'^[\w<>\[\],\. ?()]*?\s(\w+)\s*(?:<[^>]*>)?\s*\(', body)
    head = body.split('=')[0]
    if mm and ('(' in head) and not re.match(r'^\(', body):
        # a field of tuple type would be "(float, float) x" - not used here
        return [mm.group(1)], 'method'
    # property with => or { get
    pm = re.match(r'^[\w<>\[\],\. ?]+?\s(\w+)\s*(=>|\{)', body)
    if pm and not re.search(r'=\s*new\b', body.split(pm.group(2))[0]):
        if pm.group(2) == '=>' or re.search(r'\{\s*(get|set)\b', body):
            return [pm.group(1)], 'prop'
    # field(s): type name [= init] (, name [= init])* ;
    tm = re.match(r'^([\w<>\[\],\. ?]+?)\s+(\w+\s*(?:=|,|;).*)$', body, re.S)
    if not tm: return [], 'other'
    rest = tm.group(2).rstrip().rstrip(';')
    names = []
    for p in _split_top(rest):
        nm = re.match(r'^\s*(\w+)', p)
        if nm: names.append(nm.group(1))
    return names, 'field'

def members(text):
    L = text.split("\n"); st = {"blk": False}
    S = [_strip(l, st) for l in L]
    depth = 0; out = []; i = 0; n = len(L)
    # find class body depth: the depth after 'class FcsController ... {'
    body_depth = None
    while i < n:
        s = S[i]
        if body_depth is None:
            if re.search(r'\bclass\s+\w+', s):
                # class opens on this or next line
                j = i
                while '{' not in S[j]: j += 1
                depth_before = depth
                for k in range(i, j + 1): depth += S[k].count('{') - S[k].count('}')
                body_depth = depth; i = j + 1; continue
            depth += s.count('{') - s.count('}'); i += 1; continue
        if depth < body_depth: break
        if depth == body_depth and s.strip() and s.strip() != '}':
            # a member starts here; collect its leading comments/attributes
            st0 = i
            while st0 > 0 and (L[st0 - 1].strip().startswith("//") or L[st0 - 1].strip().startswith("[")):
                st0 -= 1
            # end: either ';' at body depth before any '{', or a balanced { } block (then optional ';')
            j = i; d = 0; opened = False; code = []
            while j < n:
                code.append(S[j])
                for ch in S[j]:
                    if ch == '{': d += 1; opened = True
                    elif ch == '}': d -= 1
                if not opened and S[j].rstrip().endswith(';') and d == 0: break
                if opened and d == 0:
                    # expression-bodied initialisers like "= new X { ... };" end with ';'
                    if S[j].rstrip().endswith(';') or not re.search(r'=\s*(new\b[^;]*)?$', " ".join(code).split('{')[0]): break
                if opened and d == 0 and S[j].rstrip().endswith(';'): break
                j += 1
            joined = " ".join(code)
            names, kind = _names(joined)
            out.append(Member(names, kind, st0, i, j + 1, "\n".join(L[st0:j + 1])))
            i = j + 1; continue
        depth += s.count('{') - s.count('}'); i += 1
    return L, out

def kind_is_block(code):
    head = code.split('{')[0]
    return '(' in head.split('=')[0] or re.search(r'\w\s*$', head.strip()) is not None and '=' not in head

def index(text):
    L, ms = members(text)
    d = {}
    for m in ms:
        for nm in m.names: d.setdefault(nm, []).append(m)
    return L, ms, d
