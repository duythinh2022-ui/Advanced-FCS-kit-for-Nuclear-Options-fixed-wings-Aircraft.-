#!/usr/bin/env python3
"""Build NO-FCS-Kit-Manual.pdf from MANUAL.md (the full manual; README.md is the short front page).

usage: python3 tools/build_manual_pdf.py [MANUAL.md] [out.pdf]
needs: pandoc (GitHub-flavoured markdown -> HTML), Python playwright with Chromium (HTML -> PDF).
The PDF has a cover, the contents as its own page, a divider page per part, every numbered section on a new page,
running page numbers and clickable internal links (the README's own anchors).
"""
import os, re, subprocess, sys, datetime, html

KIT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(KIT, "MANUAL.md")
OUT = sys.argv[2] if len(sys.argv) > 2 else os.path.join(KIT, "NO-FCS-Kit-Manual.pdf")

md = open(SRC, encoding="utf-8").read()
# the README's "back to contents" links and its title block are replaced by the PDF's own cover / navigation
md = re.sub(r'\n\[↑ Contents\]\(#contents\)\n', '\n', md)
title_end = md.index("\n---\n")
intro = md[:title_end]
md = md[title_end + 5:]

body = subprocess.run(["pandoc", "-f", "gfm", "-t", "html5", "--mathml", "--wrap=none"], input=md, capture_output=True, text=True, check=True).stdout
intro_html = subprocess.run(["pandoc", "-f", "gfm", "-t", "html5", "--mathml", "--wrap=none"], input=intro, capture_output=True, text=True, check=True).stdout

# diagrams (box-drawing characters) keep their columns; everything else may wrap
def mark_pre(m):
    block = m.group(0)
    return block.replace("<pre", '<pre class="diagram"', 1) if re.search('[─│┌└├▶◀]', block) else block
body = re.sub(r'<pre[^>]*>.*?</pre>', mark_pre, body, flags=re.S)
# part headings (h1) become divider pages
body = re.sub(r'<h1 id="([^"]+)">Part ([IVX]+) · (.+?)</h1>',
              lambda m: f'<section class="part" id="{m.group(1)}"><div class="partno">Part {m.group(2)}</div><div class="partname">{m.group(3)}</div></section>', body)
# the contents block: own page
body = body.replace('<h2 id="start-here">', '<h2 id="start-here" class="newpage">')

# cover: subtitle, jets table, status, author from the README's own intro
sub = re.search(r'<p><strong>(.+?)</strong></p>', intro_html)
tables = re.findall(r'<table>.*?</table>', intro_html, flags=re.S)
status = re.findall(r'<blockquote>(.*?)</blockquote>', intro_html, flags=re.S)
today = datetime.date.today().strftime("%d %B %Y")
cover = f'''
<section class="cover">
  <div class="kicker">Nuclear Option · flight control</div>
  <h1 class="title">NO FCS Kit</h1>
  <div class="subtitle">{sub.group(1) if sub else ""}</div>
  <div class="covertable">{tables[0] if tables else ""}</div>
  {"".join(f'<div class="coverstatus">{b}</div>' for b in status)}
  <div class="coverfoot">
    <div>FCS by <b>Tom</b> (GitHub <code>duythinh2022-ui</code>) · developed with Claude, Sept–Nov 2026</div>
    <div>Manual built {today} from <code>README.md</code></div>
  </div>
</section>'''

CSS = r'''
@page { size: A4; margin: 18mm 16mm 18mm 16mm; }
:root { --ink:#1b1f24; --muted:#5b6470; --rule:#d9dee5; --accent:#1f5fae; --accent2:#0e3a6e; --soft:#f4f6f9; --code:#f6f8fa; }
html { font-family: "Inter", "DejaVu Sans", sans-serif; font-size: 9.6pt; color: var(--ink); line-height: 1.5; }
body { margin: 0; }
a { color: var(--accent); text-decoration: none; }
h1, h2, h3, h4 { font-family: "Inter Display", "Inter", sans-serif; color: var(--accent2); line-height: 1.25; break-after: avoid; }
h2 { font-size: 18pt; margin: 0 0 10pt; padding-bottom: 5pt; border-bottom: 2px solid var(--accent); break-before: page; }
h2.newpage { break-before: page; }
h2#contents { break-before: auto; margin-top: 16pt; }
h3 { font-size: 12.5pt; margin: 16pt 0 6pt; }
h4 { font-size: 10.5pt; margin: 12pt 0 4pt; }
p { margin: 0 0 6pt; orphans: 3; widows: 3; }
ul, ol { margin: 0 0 7pt; padding-left: 16pt; }
li { margin: 1.5pt 0; }
li > p { margin: 0; }
strong { color: #0f2747; }
code { font-family: "DejaVu Sans Mono", monospace; font-size: 8.2pt; background: var(--code); border: 1px solid #e6e9ee; border-radius: 3px; padding: 0 2.5pt; }
pre { background: var(--code); border: 1px solid #e1e5ea; border-left: 3px solid var(--accent); border-radius: 4px; padding: 6pt 8pt; margin: 4pt 0 9pt;
      white-space: pre-wrap; word-break: break-word; font-size: 7.6pt; line-height: 1.38; break-inside: avoid; }
pre code { background: none; border: none; padding: 0; font-size: inherit; }
pre.diagram { white-space: pre; font-size: 6.55pt; overflow: hidden; }
blockquote { margin: 6pt 0 10pt; padding: 7pt 10pt; background: #eef4fc; border-left: 3px solid var(--accent); border-radius: 4px; }
blockquote p:last-child { margin-bottom: 0; }
table { border-collapse: collapse; width: 100%; margin: 4pt 0 11pt; font-size: 8.3pt; line-height: 1.35; break-inside: auto; }
thead { display: table-header-group; }
tr { break-inside: avoid; }
th { background: var(--accent2); color: #fff; text-align: left; font-weight: 600; padding: 4pt 5pt; }
td { padding: 3.5pt 5pt; border-bottom: 1px solid var(--rule); vertical-align: top; }
tbody tr:nth-child(even) td { background: var(--soft); }
td code, th code { font-size: 7.4pt; }
hr { border: none; border-top: 1px solid var(--rule); margin: 12pt 0; }
/* cover */
.cover { height: 257mm; display: flex; flex-direction: column; break-after: page; }
.kicker { margin-top: 14mm; text-transform: uppercase; letter-spacing: .18em; font-size: 9pt; color: var(--muted); }
.title { font-size: 46pt; margin: 4pt 0 6pt; color: var(--accent2); border: none; }
.subtitle { font-size: 14pt; color: var(--ink); max-width: 150mm; margin-bottom: 10mm; }
.covertable table { font-size: 8.4pt; }
.coverstatus { margin-top: 5mm; font-size: 8.8pt; padding: 8pt 11pt; background: #eef4fc; border-left: 3px solid var(--accent); border-radius: 4px; }
.coverstatus p { margin: 0; }
.coverfoot { margin-top: auto; color: var(--muted); font-size: 8.5pt; border-top: 1px solid var(--rule); padding-top: 6pt; }
/* part dividers */
.part { height: 257mm; break-before: page; break-after: page; display: flex; flex-direction: column; justify-content: center;
        border-left: 6px solid var(--accent); padding-left: 14mm; }
.partno { text-transform: uppercase; letter-spacing: .2em; color: var(--muted); font-size: 11pt; }
.partname { font-family: "Inter Display", "Inter", sans-serif; font-size: 32pt; color: var(--accent2); margin-top: 4pt; }
#contents + p strong, #contents ~ p strong { color: var(--accent2); }
'''

doc = f'''<!doctype html><html><head><meta charset="utf-8"><title>NO FCS Kit</title><style>{CSS}</style></head>
<body>{cover}{body}</body></html>'''
tmp = OUT + ".html"
open(tmp, "w", encoding="utf-8").write(doc)

from playwright.sync_api import sync_playwright
foot = ('<div style="font-family:Inter,sans-serif;font-size:7.5pt;color:#7a828c;width:100%;padding:0 16mm;display:flex;'
        'justify-content:space-between"><span>NO FCS Kit · manual</span><span><span class="pageNumber"></span> / '
        '<span class="totalPages"></span></span></div>')
with sync_playwright() as p:
    b = p.chromium.launch()
    pg = b.new_page()
    pg.goto("file://" + os.path.abspath(tmp))
    pg.wait_for_timeout(300)
    pg.pdf(path=OUT, format="A4", print_background=True, display_header_footer=True,
           header_template="<div></div>", footer_template=foot,
           margin={"top": "16mm", "bottom": "18mm", "left": "16mm", "right": "16mm"})
    b.close()
os.remove(tmp)
print("wrote", OUT)
