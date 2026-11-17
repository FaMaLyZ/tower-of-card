"""Build a self-contained, printable reading edition of the editable GDD."""
from pathlib import Path
import base64
import html
import re

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parent
source = (ROOT / 'Neo Tower of Cards GDD.md').read_text(encoding='utf-8')

def inline(text):
    text = html.escape(text)
    text = re.sub(r'\*\*(.+?)\*\*', r'<strong>\1</strong>', text)
    text = re.sub(r'https://docs\.google\.com/[^\s<]+', lambda m: '<a href="' + m[0] + '">Open reference document</a>', text)
    return text

def convert(chunk):
    lines = chunk.strip().splitlines()
    result = []
    i = 0
    while i < len(lines):
        line = lines[i].strip()
        if not line:
            i += 1
            continue
        if line.startswith('|'):
            rows = []
            while i < len(lines) and lines[i].strip().startswith('|'):
                cells = [x.strip() for x in lines[i].strip().strip('|').split('|')]
                if not all(re.fullmatch(r'[: -]+', x) for x in cells):
                    rows.append(cells)
                i += 1
            result.append('<table><thead><tr>' + ''.join('<th>' + inline(x) + '</th>' for x in rows[0]) + '</tr></thead><tbody>')
            result.extend('<tr>' + ''.join('<td>' + inline(x) + '</td>' for x in row) + '</tr>' for row in rows[1:])
            result.append('</tbody></table>')
            continue
        if line.startswith('#'):
            level = len(line) - len(line.lstrip('#'))
            result.append(f'<h{level}>' + inline(line[level:].strip()) + f'</h{level}>')
        elif re.match(r'^\d+\. ', line) or line.startswith('- '):
            ordered = bool(re.match(r'^\d+\. ', line))
            tag = 'ol' if ordered else 'ul'
            result.append('<' + tag + '>')
            while i < len(lines) and (bool(re.match(r'^\d+\. ', lines[i].strip())) if ordered else lines[i].strip().startswith('- ')):
                result.append('<li>' + inline(re.sub(r'^(?:\d+\.|-) ', '', lines[i].strip())) + '</li>')
                i += 1
            result.append('</' + tag + '>')
            continue
        else:
            para = [line]
            while i + 1 < len(lines) and lines[i + 1].strip() and not lines[i + 1].startswith(('#', '|', '- ')):
                i += 1
                para.append(lines[i].strip())
            result.append('<p>' + inline(' '.join(para)) + '</p>')
        i += 1
    return '\n'.join(result)

pages = [convert(x) for x in source.split('<!-- page -->')]
art = base64.b64encode((PROJECT / 'Assets/Images/main.png').read_bytes()).decode()
pages[0] = pages[0].replace('<h2>Contents</h2>', f'<figure><img src="data:image/png;base64,{art}" alt="Existing Neo Tower of Card main menu artwork"><figcaption>Existing project artwork</figcaption></figure><h2>Contents</h2>')
css = '''
:root{--navy:#24253d;--blue:#2d4d73;--gold:#c79f42;--ink:#20232a;--muted:#626977}
*{box-sizing:border-box}body{margin:0;background:#e8eaf0;color:var(--ink);font:15px/1.46 Arial,sans-serif}
nav{position:sticky;top:0;z-index:10;background:var(--navy);color:white;display:flex;align-items:center;gap:12px;padding:12px 24px;flex-wrap:wrap}
nav strong{font-size:14px;margin-right:auto}button{font:inherit;border:1px solid #c9cbd3;border-radius:4px;background:white;color:var(--navy);padding:6px 13px;cursor:pointer}button:disabled{opacity:.4;cursor:default}
.page{position:relative;background:white;width:794px;min-height:1123px;margin:26px auto;padding:55px 62px 66px;box-shadow:0 2px 16px #2222;display:none}.page.active{display:block}
.running{font-size:10px;letter-spacing:1.5px;color:var(--muted);text-transform:uppercase;margin-bottom:24px}
h1{font-size:30px;line-height:1.1;color:black;margin:6px 0 17px}h2{font-size:23px;line-height:1.2;color:var(--blue);margin:22px 0 12px}h3{font-size:17px;line-height:1.25;color:var(--blue);margin:20px 0 9px}
p{margin:0 0 12px}strong{font-weight:700}table{width:100%;border-collapse:collapse;margin:14px 0 17px;font-size:12.7px;line-height:1.32;table-layout:auto}th{background:#e7edf5;color:#213a58;text-align:left;font-weight:bold}td,th{border:1px solid #bac3ce;padding:8px 9px;vertical-align:top}tr:nth-child(even) td{background:#f6f8fb}th:first-child,td:first-child{min-width:90px}
ul,ol{padding-left:21px;margin:9px 0 14px}li{margin-bottom:5px}a{color:var(--blue);overflow-wrap:anywhere}figure{margin:17px 0}figure img{display:block;width:100%;height:auto}figcaption{font-size:10px;color:var(--muted);margin-top:5px}
.page.dense{font-size:14px;line-height:1.4}.dense table{font-size:12.4px;line-height:1.28}.dense td,.dense th{padding:7px 8px}.dense h3{margin-top:17px}
.folio{position:absolute;bottom:28px;left:62px;right:62px;display:flex;justify-content:space-between;font-size:10px;color:var(--muted)}
@media(max-width:820px){.page{width:calc(100% - 24px);padding:32px 26px 66px;min-height:1000px}.folio{left:26px;right:26px}nav strong{width:100%}table{font-size:12px}td,th{padding:6px}}
@media print{@page{size:A4;margin:0}body{background:white;font-size:11pt}nav{display:none}.page,.page.active{display:block;width:210mm;min-height:297mm;margin:0;padding:15mm 16mm 18mm;box-shadow:none;break-after:page}.page:last-child{break-after:auto}h2,h3{break-after:avoid}tr,figure{break-inside:avoid}.folio{bottom:8mm}a{color:inherit;text-decoration:none}}
'''
sections = '\n'.join(f'<section class="page {"active" if i == 0 else ""} {"dense" if i+1 in [9,10,11,13,15] else ""}" id="page-{i+1}"><div class="running">Neo Tower of Cards · Game Design Document</div>{body}<footer class="folio"><span>Version 0.1 · 28 October 2026</span><span>{i+1} / {len(pages)}</span></footer></section>' for i, body in enumerate(pages))
script = '''let current=0;const pages=[...document.querySelectorAll('.page')];function show(n){current=Math.max(0,Math.min(pages.length-1,n));pages.forEach((p,i)=>p.classList.toggle('active',i===current));document.querySelector('#counter').textContent=`Page ${current+1} of ${pages.length}`;document.querySelector('#previous').disabled=current===0;document.querySelector('#next').disabled=current===pages.length-1;window.scrollTo(0,0)}document.querySelector('#previous').onclick=()=>show(current-1);document.querySelector('#next').onclick=()=>show(current+1);document.querySelector('#print').onclick=()=>window.print();show(0);'''
output = '<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Neo Tower of Cards Game Design Document</title><style>' + css + '</style></head><body><nav aria-label="Document navigation"><strong>Neo Tower of Cards · GDD</strong><button id="previous">Previous</button><span id="counter"></span><button id="next">Next</button><button id="print">Print or Save PDF</button></nav>' + sections + '<script>' + script + '</script></body></html>'
(ROOT / 'Neo Tower of Cards GDD.html').write_text(output, encoding='utf-8')
print(f'Built {len(pages)} pages')
