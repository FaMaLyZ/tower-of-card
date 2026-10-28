from pathlib import Path
import re
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

root = Path(__file__).resolve().parent
doc = Document()
sec = doc.sections[0]
sec.page_width, sec.page_height = Inches(8.27), Inches(11.69)
sec.top_margin = sec.bottom_margin = Inches(.65)
sec.left_margin = sec.right_margin = Inches(.65)
for name in ['Normal', 'Title', 'Subtitle', 'Heading 1', 'Heading 2', 'Heading 3']:
    s = doc.styles[name]
    s.font.name = 'Arial'
    s.font.color.rgb = RGBColor(0, 0, 0)
normal = doc.styles['Normal']
normal.font.size = Pt(10)
normal.paragraph_format.space_after = Pt(6)
normal.paragraph_format.line_spacing = 1.08
for name,size in [('Title',26),('Heading 1',18),('Heading 2',13)]:
    doc.styles[name].font.size=Pt(size)
    doc.styles[name].paragraph_format.space_before=Pt(11)
    doc.styles[name].paragraph_format.space_after=Pt(6)

def runs(p, text):
    for i, part in enumerate(re.split(r'\*\*(.*?)\*\*', text)):
        r=p.add_run(part)
        r.bold=bool(i%2)

pages=(root/'Neo Tower of Cards GDD.md').read_text(encoding='utf-8').split('<!-- page -->')
for page_no,chunk in enumerate(pages):
    if page_no: doc.add_page_break()
    lines=chunk.strip().splitlines()
    i=0
    while i<len(lines):
        line=lines[i].strip()
        if not line:
            i+=1;continue
        if line.startswith('|'):
            rows=[]
            while i<len(lines) and lines[i].strip().startswith('|'):
                cells=[c.strip() for c in lines[i].strip().strip('|').split('|')]
                if not all(re.fullmatch(r'[: -]+',c) for c in cells): rows.append(cells)
                i+=1
            t=doc.add_table(rows=0, cols=len(rows[0]))
            t.style='Table Grid'
            for ri,row in enumerate(rows):
                cells=t.add_row().cells
                for cell,txt in zip(cells,row):
                    p=cell.paragraphs[0]
                    p.paragraph_format.space_after=Pt(3)
                    p.paragraph_format.space_before=Pt(3)
                    runs(p,txt)
                    for r in p.runs:
                        r.font.size=Pt(8.5)
                        if ri==0:r.bold=True
                    if ri==0:
                        shade=OxmlElement('w:shd');shade.set(qn('w:fill'),'E7EDF5');cell._tc.get_or_add_tcPr().append(shade)
                trpr=t.rows[-1]._tr.get_or_add_trPr()
                trpr.append(OxmlElement('w:cantSplit'))
                if ri==0:trpr.append(OxmlElement('w:tblHeader'))
            doc.add_paragraph().paragraph_format.space_after=Pt(0)
            continue
        if line.startswith('#'):
            level=len(line)-len(line.lstrip('#'))
            title=line[level:].strip()
            if page_no==0 and title=='Contents':
                doc.add_picture(str(root.parent/'Assets/Images/main.png'),width=Inches(5.6))
            doc.add_paragraph(title,'Title' if level==1 else f'Heading {level-1}')
        elif line.startswith('- ') or re.match(r'^\d+\. ',line):
            doc.add_paragraph(re.sub(r'^(?:\d+\.|-) ', '',line), 'List Bullet' if line.startswith('- ') else 'List Number')
        else:
            para=[line]
            while i+1<len(lines) and lines[i+1].strip() and not lines[i+1].startswith(('#','|','- ')):
                i+=1;para.append(lines[i].strip())
            p=doc.add_paragraph();runs(p,' '.join(para))
        i+=1

foot=sec.footer.paragraphs[0]
foot.add_run('Neo Tower of Cards  |  GDD v0.1  |  ')
field=OxmlElement('w:fldSimple');field.set(qn('w:instr'),'PAGE');foot._p.append(field)
for r in foot.runs:r.font.size=Pt(8)
doc.core_properties.title='Neo Tower of Cards Game Design Document'
doc.core_properties.subject='Gameplay rules, systems, assets, and design decisions'
out=root/'Neo Tower of Cards GDD.docx'
doc.save(out)
print(out)
