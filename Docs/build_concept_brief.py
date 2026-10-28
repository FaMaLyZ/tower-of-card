from pathlib import Path
from copy import deepcopy
from hashlib import sha256
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parent
REF=Path(r'C:\Users\User\AppData\Local\Packages\sandbox.{113a7302-a27f-4ff6-93ca-5c2fbcbc4755}\AC\Temp\browser-use\exports\1-GDD Page Document - Rapid Game Template-0687294d-cb0e-41fc-970e-c32c32c9fd3e.docx')
QA=Path(r'C:\Users\User\.codex\visualizations\2026\10\27\01a17f52-963d-7bb2-916f-51cd47e558ae')
source_hash=sha256(REF.read_bytes()).hexdigest()
doc=Document(REF)
heading_props={p.style.name:deepcopy(p._p.pPr) for p in doc.paragraphs if p.style.name in ['Heading 1','Heading 2']}
QA.joinpath('artifact.md').write_text(f'''# Concept brief template contract
Reference: {REF}
SHA256: {source_hash}
Reference render: C:/Users/User/AppData/Local/Temp/neo-brief-reference
Reference has three pages, one US Letter portrait section, 1 inch margins.
Source title uses Anton 28 pt centered; subtitles Poppins 11 pt; Heading 1 Poppins 20 pt with blue shading, Heading 2 Poppins 16 pt with pale blue shading.
Source section order: Overview, High Concept, Direction, Synopsis, Theme Setting Brief, Design Pillars, Key Features, Core Gameplay Mechanics Features Brief, Design Influences.
Adaptation: user asks for something like the template rather than exact fidelity. Replace all instructional body text, example game image, artwork placeholders, and example tables. Retain source styles, page geometry, heading treatments, relationships, and numbering. Use Arial fallback because source fonts are not installed. Reflow into three deliberate pages with meaningful gameplay and influence visuals. No team credits or story invented. Body and tables are editable. Preserve source file byte for byte. Visual gate: every final page rendered and inspected. Title border sanitizer is intentional for Google Docs compatibility.
''',encoding='utf-8')
for element in list(doc._element.body):
    if element.tag != qn('w:sectPr'):doc._element.body.remove(element)
normal=doc.styles['normal']
normal.font.name='Arial';normal.font.size=Pt(10.5)
normal.paragraph_format.line_spacing=1.08
normal.paragraph_format.space_after=Pt(6)
for style in ['Title','Subtitle','Heading 1','Heading 2']:
    doc.styles[style].font.name='Arial'
    doc.styles[style].font.color.rgb=RGBColor(0,0,0)
doc.styles['Heading 1'].font.size=Pt(18)
doc.styles['Heading 2'].font.size=Pt(14)

def p(text='',bold=False):
    item=doc.add_paragraph(style='normal')
    item.add_run(text).bold=bold
    return item
def heading(text,major=False):
    style='Heading 1' if major else 'Heading 2'
    item=doc.add_paragraph(text,style)
    item._p.remove(item._p.pPr)
    item._p.insert(0,deepcopy(heading_props[style]))
    item.paragraph_format.space_before=Pt(10)
    item.paragraph_format.space_after=Pt(6)
    item.paragraph_format.keep_with_next=True
    for r in item.runs:r.font.name='Arial';r.font.color.rgb=RGBColor(0,0,0);r.font.size=Pt(18 if major else 14)
    return item
def bullet(title,text):
    item=p();item.add_run('• '+title+' ').bold=True;item.add_run(text)
def table(headers,rows,widths):
    t=doc.add_table(rows=1,cols=len(headers));t.autofit=False
    for c,w in zip(t.columns,widths):c.width=Inches(w)
    for cell,text,w in zip(t.rows[0].cells,headers,widths):
        cell.width=Inches(w);cell.text=text
        shade=OxmlElement('w:shd');shade.set(qn('w:fill'),'D2E3F3');cell._tc.get_or_add_tcPr().append(shade)
        for r in cell.paragraphs[0].runs:r.bold=True
    for row in rows:
        for cell,text,w in zip(t.add_row().cells,row,widths):cell.width=Inches(w);cell.text=text
    for row in t.rows:
        row._tr.get_or_add_trPr().append(OxmlElement('w:cantSplit'))
        for cell in row.cells:
            for para in cell.paragraphs:
                para.paragraph_format.space_after=Pt(5);para.paragraph_format.space_before=Pt(5)
                for r in para.runs:r.font.name='Arial';r.font.size=Pt(9.5)
    return t

title=doc.add_paragraph('Neo Tower of Cards','Title');title.alignment=WD_ALIGN_PARAGRAPH.CENTER
for r in title.runs:r.font.name='Arial';r.font.size=Pt(26);r.bold=True
sub=doc.add_paragraph('Concept GDD  |  Version 0.1  |  28 October 2026','Subtitle');sub.alignment=WD_ALIGN_PARAGRAPH.CENTER
heading('Overview',True)
pic=p();pic.alignment=WD_ALIGN_PARAGRAPH.CENTER
pic.add_run().add_picture(str(ROOT.parent/'Assets/Images/main.png'),width=Inches(4.5))
p('A single-player PC card strategy game where poker combinations become the building blocks of a physical house of cards. Build, score, watch the tower settle, and continue from what remains.')
heading('High Concept')
p('The player holds up to eight cards and places up to five each turn. Those placements form one poker hand while also extending a 3D tower. Rank and suit determine the combination; each card’s mass affects the physical structure.')
p('Ending the turn reveals chips multiplied by a hand multiplier and releases the tower into physics. The score accumulates across a run, while surviving supports and newly opened positions shape the next turn.')
heading('Direction')
bullet('Plan comfortably.','Use clear snap positions and card details so decisions come from reading the hand and structure.')
bullet('Make resolution satisfying.','Give poker scoring, shaking cards, and break effects a readable turn-ending payoff.')
bullet('Keep a casino identity.','Use navy, gold, illuminated red accents, chips, and bold lettering around a legible tabletop.')

doc.add_page_break()
heading('Synopsis')
p('An eight-card hand sits in front of an empty table. The player starts a tent, builds supports and bridges, and chooses which ranks and suits to commit. Each turn becomes a small wager on their own construction: a combination scores, the tower moves, and the next decision starts from the result. The aim is to make the most of the run’s cards and turns.')
heading('Theme and Setting Brief')
p('A stylized casino tabletop is the entire play space. The presentation borrows the spectacle of slot-machine branding, gold frames, score displays, and poker chips. The activity is single-player construction and scoring, without an implemented betting economy, dealer, or opponent.')
heading('Design Pillars')
table(['Every card has two roles','Readable construction','A clear payoff each turn'],[[
    'Choose rank and suit for a poker hand, then choose where that card belongs in the tower. Mass gives it a physical role.',
    'Tents support bridges, and bridges unlock higher tents. Previews explain valid positions before commitment.',
    'Alternate quiet planning with a short score reveal and physical resolution. Show both numerical progress and structural consequences.'
]],[2.17,2.16,2.17])
heading('Key Features')
bullet('Poker meets construction.','The same cards form the turn’s scoring hand and the physical tower.')
bullet('Limited hand exchanges.','Keep useful cards between turns and exchange up to three unwanted cards per turn.')
bullet('Physics and rebuilding.','Cards unfreeze at turn end, can break after hard impacts, and leave a structure to repair or restart.')
heading('Core Gameplay Mechanics and Features Brief')
bullet('Build.','Complete two-card tents, bridge adjacent complete tents with a floor, then build another tent above. The current layout permits four base tents.')
bullet('Manage the hand.','Draw toward an eight-card capacity, place up to five, and retain unplayed cards. The standard 52-card deck does not automatically recycle played cards.')
bullet('Score and resolve.','Evaluate the cards placed that turn. Add the scoring cards to base chips, multiply, and add the result to the total. Broken cards still count toward that recorded hand.')
p('Core loop: Draw → inspect and exchange → place → end turn → score and release physics → build again.',True)
p('Run direction: up to ten turns in the current scene. Final end conditions and the relationship between tower survival and score are tracked as open decisions in the full GDD.')

doc.add_page_break()
heading('Design Influences',True)
p('Neo Tower of Cards combines a physical building activity, a poker-scoring reference, and a casino presentation language. Each influence contributes a different part of the experience.')

canvas=Image.new('RGBA',(1200,660),'white')
layer=Image.new('RGBA',canvas.size,(0,0,0,0));draw=ImageDraw.Draw(layer)
for box,col in [((220,35,680,495),(112,164,215,115)),((520,35,980,495),(214,172,71,110)),((370,195,830,655),(132,120,173,100))]:draw.ellipse(box,fill=col,outline=col[:3]+(255,),width=4)
canvas=Image.alpha_composite(canvas,layer).convert('RGB');draw=ImageDraw.Draw(canvas)
fontpath=r'C:\Windows\Fonts\arial.ttf';boldpath=r'C:\Windows\Fonts\arialbd.ttf'
font=ImageFont.truetype(fontpath,27);bold=ImageFont.truetype(boldpath,31)
def label(x,y,title,body):
    draw.text((x,y),title,font=bold,fill='#20283c',anchor='mm')
    draw.text((x,y+39),body,font=font,fill='#333b4c',anchor='mm')
label(407,159,'House of cards','Physical construction')
label(794,159,'Balatro','Poker scoring')
label(600,551,'Casino design','UI and presentation')
draw.rounded_rectangle((423,290,777,390),radius=20,fill='white',outline='#2c3552',width=3)
draw.text((600,326),'NEO TOWER',font=bold,fill='#24253d',anchor='mm')
draw.text((600,365),'OF CARDS',font=bold,fill='#24253d',anchor='mm')
diagram=QA/'neo-influences.png';canvas.save(diagram)
pic=p();pic.alignment=WD_ALIGN_PARAGRAPH.CENTER
pic.add_run().add_picture(str(diagram),width=Inches(5.25))
table(['Influence','What it contributes','How we use it'],[
('House of cards','Tent-and-bridge building, fragile balance, and vertical growth.','A physical tower built in legal snap positions, released into gravity and collisions at turn end.'),
('Balatro','Recognizable poker hands, chips × multiplier scoring, and staged feedback.','Cards placed in one turn form the scored hand. Hand selection and the score reveal drive repeated attempts.'),
('Casino UI design','Gold trim, dark surfaces, illuminated signs, poker chips, and slot-machine imagery.','Existing menu and score artwork establish a celebratory visual identity while gameplay information stays readable.')
],[1.05,2.3,3.15])
heading('What Makes the Combination Our Own')
p('A played card earns points and occupies a structural position at the same time. The score reveal shares its moment with a physical test of the tower. What survives changes the next construction opportunity, giving familiar poker choices a visible place on the table.')
p('The influences guide the design; they do not imply a slot-machine mechanic, Balatro’s shop or joker progression, or a real-money gambling system.')

doc.core_properties.title='Neo Tower of Cards Concept GDD'
doc.core_properties.subject='Concept brief and design influences'
doc.core_properties.author=''
doc.save(ROOT/'Neo Tower of Cards Concept Brief.docx')
assert sha256(REF.read_bytes()).hexdigest()==source_hash
print(ROOT/'Neo Tower of Cards Concept Brief.docx')
