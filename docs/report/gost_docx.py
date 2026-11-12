# -*- coding: utf-8 -*-
"""
Сборщик пояснительной записки в DOCX по методическим указаниям колледжа.

Текст записки пишется в простом текстовом формате (см. note.txt), а этот модуль
превращает его в оформленный документ Word:
  - титульный лист по шаблону колледжа (title_template.docx);
  - содержание с номерами страниц (номера берутся из пробной сборки в PDF);
  - поля 30/10/20/20 мм, Times New Roman 14, интервал 1,5, отступ 1,25 см;
  - номера страниц внизу по центру, «Содержание» — страница 2;
  - заголовки разделов с новой страницы, по центру, заглавными, жирным;
  - таблицы с подписью «Таблица N – …» слева над таблицей, шрифт 12;
  - рисунки с подписью «Рисунок N – …» по центру под рисунком;
  - формулы с номером справа и расшифровкой «где …»;
  - приложения «Приложение А», «Приложение Б»… с новой страницы.

Разметка note.txt:
  # ЗАГОЛОВОК РАЗДЕЛА          — раздел (в содержании), с новой страницы
  ## 1.1 Подраздел            — подраздел (в содержании)
  @appendix А | Название     — приложение с новой страницы
  обычный абзац              — абзац с отступом 1,25 см
  - пункт списка             — элемент списка с тире
  :table Название            — далее строки «яч | яч | яч», первая — шапка, конец — пустая строка
  :figure путь | Название    — рисунок
  :formula текст | (1)       — формула с номером
  :code / :endcode           — листинг (моноширинный шрифт)
  :pagebreak                 — разрыв страницы
  :raw-center текст          — строка по центру без отступа
"""
import copy
import re
import subprocess
import tempfile
from pathlib import Path

from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK, WD_LINE_SPACING, WD_TAB_ALIGNMENT, WD_TAB_LEADER
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Mm, Pt, RGBColor

FONT = "Times New Roman"
MONO = "Courier New"


# ------------------------------------------------------------------ низкоуровневое
def set_font(run, size=14, bold=None, italic=None, font=FONT):
    run.font.name = font
    run.font.size = Pt(size)
    run.font.color.rgb = RGBColor(0, 0, 0)
    r_pr = run._element.get_or_add_rPr()
    r_fonts = r_pr.find(qn("w:rFonts"))
    if r_fonts is None:
        r_fonts = OxmlElement("w:rFonts")
        r_pr.append(r_fonts)
    for attr in ("w:ascii", "w:hAnsi", "w:cs", "w:eastAsia"):
        r_fonts.set(qn(attr), font)
    if bold is not None:
        run.bold = bold
    if italic is not None:
        run.italic = italic


def fmt(p, align=WD_ALIGN_PARAGRAPH.JUSTIFY, indent=1.25, spacing=1.5, before=0, after=0,
        keep_next=False, hyphen=True):
    pf = p.paragraph_format
    pf.alignment = align
    pf.first_line_indent = Cm(indent) if indent else Cm(0)
    pf.left_indent = Cm(0)
    pf.right_indent = Cm(0)
    pf.line_spacing_rule = WD_LINE_SPACING.ONE_POINT_FIVE if spacing == 1.5 else WD_LINE_SPACING.SINGLE
    pf.space_before = Pt(before)
    pf.space_after = Pt(after)
    pf.keep_with_next = keep_next
    pf.widow_control = True
    if not hyphen:
        p_pr = p._p.get_or_add_pPr()
        el = OxmlElement("w:suppressAutoHyphens")
        p_pr.append(el)


def add_field(paragraph, instr, size=14):
    run = paragraph.add_run()
    set_font(run, size)
    b = OxmlElement("w:fldChar")
    b.set(qn("w:fldCharType"), "begin")
    t = OxmlElement("w:instrText")
    t.set(qn("xml:space"), "preserve")
    t.text = instr
    s = OxmlElement("w:fldChar")
    s.set(qn("w:fldCharType"), "separate")
    txt = OxmlElement("w:t")
    txt.text = "1"
    e = OxmlElement("w:fldChar")
    e.set(qn("w:fldCharType"), "end")
    for el in (b, t, s, txt, e):
        run._element.append(el)


def set_cell_borders(cell):
    tc_pr = cell._tc.get_or_add_tcPr()
    borders = OxmlElement("w:tcBorders")
    for edge in ("top", "left", "bottom", "right"):
        el = OxmlElement(f"w:{edge}")
        el.set(qn("w:val"), "single")
        el.set(qn("w:sz"), "4")
        el.set(qn("w:color"), "000000")
        borders.append(el)
    tc_pr.append(borders)


def repeat_header(row):
    tr_pr = row._tr.get_or_add_trPr()
    el = OxmlElement("w:tblHeader")
    el.set(qn("w:val"), "true")
    tr_pr.append(el)


def page_numbers(section, show=True, start=None):
    section.footer.is_linked_to_previous = False
    footer = section.footer
    for p in footer.paragraphs:
        p._p.getparent().remove(p._p)
    p = footer.add_paragraph()
    fmt(p, align=WD_ALIGN_PARAGRAPH.CENTER, indent=0, spacing=1)
    if show:
        add_field(p, "PAGE", 12)
    if start is not None:
        sect_pr = section._sectPr
        pg = sect_pr.find(qn("w:pgNumType"))
        if pg is None:
            pg = OxmlElement("w:pgNumType")
            sect_pr.append(pg)
        pg.set(qn("w:start"), str(start))


def margins(section):
    section.page_width = Mm(210)
    section.page_height = Mm(297)
    section.left_margin = Mm(30)
    section.right_margin = Mm(10)
    section.top_margin = Mm(20)
    section.bottom_margin = Mm(20)
    section.footer_distance = Mm(10)


def auto_widths(rows, total=17.0):
    """Ширины столбцов: не уже самого длинного слова, остальное — пропорционально объёму текста."""
    n = len(rows[0])
    mins, weights = [], []
    for j in range(n):
        cells = [r[j] if j < len(r) else "" for r in rows]
        body_longest = max((len(w) for c in cells[1:] for w in c.replace("\\n", " ").split()), default=1)
        head_longest = max((len(w) for w in cells[0].split()), default=1)
        mins.append(min(3.0, max(body_longest * 0.19 + 0.4, head_longest * 0.17 + 0.3)))
        avg = sum(len(c) for c in cells[1:]) / max(1, len(cells) - 1)
        weights.append(avg + 1)
    rest = max(0.0, total - sum(mins))
    s = sum(weights)
    widths = [mins[j] + rest * weights[j] / s for j in range(n)]
    k = total / sum(widths)
    return [w * k for w in widths]


# ------------------------------------------------------------------ титульный лист
def fill_title(doc, info):
    """Заполняет шаблон титульного листа колледжа данными работы."""
    paras = doc.paragraphs

    def replace(p, text, size=None, bold=None):
        runs = p.runs
        if not runs:
            r = p.add_run()
            runs = [r]
        first = runs[0]
        for r in runs[1:]:
            r._element.getparent().remove(r._element)
        first.text = text
        set_font(first, size or (first.font.size.pt if first.font.size else 14), bold=bold)
        # убираем жёлтую подсветку из шаблона
        r_pr = first._element.get_or_add_rPr()
        for h in r_pr.findall(qn("w:highlight")):
            r_pr.remove(h)

    for p in paras:
        t = p.text.strip()
        if t.startswith("Тема"):
            replace(p, f"Тема: «{info['topic']}»", 14, True)
            p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        elif t.startswith("ПО ПМ"):
            replace(p, info["pm"], 14, True)
        elif t.startswith("МДК"):
            replace(p, info["mdk"], 14, True)
        elif t.startswith("Специальность"):
            replace(p, info["specialty"], 14, True)
        elif t.startswith("студент группы"):
            replace(p, f"студент группы {info['group']}", 14, False)
        elif t == "ФИО":
            replace(p, info["student"], 14, False)
            if info.get("student_placeholder"):
                r_pr = p.runs[0]._element.get_or_add_rPr()
                hl = OxmlElement("w:highlight")
                hl.set(qn("w:val"), "yellow")
                r_pr.append(hl)
        elif t.startswith("Руководитель"):
            replace(p, "Руководитель:", 14, False)
            br = p.runs[0]
            br.add_break()
            r2 = p.add_run(info["teacher"])
            set_font(r2, 14, False)
        elif t.startswith("Сургут"):
            # убираем 4 пустых абзаца перед строкой «Сургут», чтобы титул поместился на один лист
            prev = p._p.getprevious()
            removed = 0
            while prev is not None and removed < 4:
                nxt = prev.getprevious()
                if prev.tag == qn("w:p") and not "".join(prev.itertext()).strip() and not prev.findall(".//" + qn("w:drawing")):
                    prev.getparent().remove(prev)
                    removed += 1
                prev = nxt
            replace(p, f"Сургут, {info['year']} г.", 12, True)


# ------------------------------------------------------------------ сборщик
class NoteBuilder:
    def __init__(self, template, info, base_dir):
        self.doc = Document(template)
        self.info = info
        self.base = Path(base_dir)
        fill_title(self.doc, info)
        # поля титульного листа оставляем как в шаблоне колледжа
        # на титульном листе номер не ставится
        page_numbers(self.doc.sections[0], show=False)
        self.table_no = 0
        self.figure_no = 0
        self.toc_entries = []       # (уровень, текст)
        self.toc_pages = {}
        self.first_heading = True
        self.search_keys = {}

        # базовый стиль
        st = self.doc.styles["Normal"]
        st.font.name = FONT
        st.font.size = Pt(14)

        # автоперенос слов во всём документе
        settings = self.doc.settings.element
        auto = OxmlElement("w:autoHyphenation")
        auto.set(qn("w:val"), "true")
        settings.append(auto)

    # --- разделы документа
    def new_section(self, show_numbers=True, start=None):
        sec = self.doc.add_section(WD_SECTION.NEW_PAGE)
        margins(sec)
        page_numbers(sec, show=show_numbers, start=start)
        return sec

    def review_page(self):
        """Лист для рецензии: заполняет преподаватель, номер не ставится."""
        self.new_section(show_numbers=False)
        p = self.doc.add_paragraph()
        fmt(p, align=WD_ALIGN_PARAGRAPH.CENTER, indent=0)
        set_font(p.add_run("РЕЦЕНЗИЯ"), 14, True)

    def toc(self):
        """Содержание: страница 2, номера страниц подставляются после пробной сборки."""
        self.new_section(show_numbers=True, start=2)
        p = self.doc.add_paragraph()
        fmt(p, align=WD_ALIGN_PARAGRAPH.CENTER, indent=0, after=12, hyphen=False)
        set_font(p.add_run("СОДЕРЖАНИЕ"), 14, True)
        self.toc_anchor = p
        self.first_heading = False   # ВВЕДЕНИЕ начнётся с новой страницы после содержания

    def _render_toc(self):
        anchor = self.toc_anchor
        for level, text in reversed(self.toc_entries):
            p = self.doc.add_paragraph()
            anchor._p.addnext(p._p)
            fmt(p, align=WD_ALIGN_PARAGRAPH.LEFT, indent=0, spacing=1, after=4, hyphen=False)
            p.paragraph_format.left_indent = Cm(0.75 if level == 2 else 0)
            p.paragraph_format.tab_stops.add_tab_stop(Cm(17) - Cm(0.05), WD_TAB_ALIGNMENT.RIGHT, WD_TAB_LEADER.DOTS)
            page = self.toc_pages.get(text, "")
            set_font(p.add_run(f"{text}\t{page}"), 14)

    # --- элементы текста
    def heading1(self, text, in_toc=True):
        p = self.doc.add_paragraph()
        fmt(p, align=WD_ALIGN_PARAGRAPH.CENTER, indent=0, after=12, keep_next=True, hyphen=False)
        if not self.first_heading:
            p.paragraph_format.page_break_before = True
        self.first_heading = False
        set_font(p.add_run(text.upper()), 14, True)
        if in_toc:
            self.toc_entries.append((1, text.upper()))

    def heading2(self, text):
        p = self.doc.add_paragraph()
        fmt(p, align=WD_ALIGN_PARAGRAPH.CENTER, indent=0, before=12, after=12, keep_next=True, hyphen=False)
        set_font(p.add_run(text), 14, True)
        self.toc_entries.append((2, text))

    def appendix(self, letter, title):
        p = self.doc.add_paragraph()
        fmt(p, align=WD_ALIGN_PARAGRAPH.CENTER, indent=0, keep_next=True, hyphen=False)
        p.paragraph_format.page_break_before = True
        set_font(p.add_run(f"Приложение {letter}"), 14, True)
        p2 = self.doc.add_paragraph()
        fmt(p2, align=WD_ALIGN_PARAGRAPH.CENTER, indent=0, after=12, keep_next=True, hyphen=False)
        set_font(p2.add_run(title), 14, True)
        key = f"ПРИЛОЖЕНИЕ {letter}. {title.upper()}"
        self.toc_entries.append((1, key))
        self.search_keys[key] = f"Приложение {letter}"

    def paragraph(self, text, align=WD_ALIGN_PARAGRAPH.JUSTIFY, indent=1.25):
        p = self.doc.add_paragraph()
        fmt(p, align=align, indent=indent)
        self._inline(p, text)
        return p

    def bullet(self, text):
        p = self.doc.add_paragraph()
        fmt(p, indent=1.25)
        self._inline(p, "– " + text)

    def _inline(self, p, text, size=14):
        # **жирный** и `код` внутри абзаца
        parts = re.split(r"(\*\*[^*]+\*\*|`[^`]+`)", text)
        for part in parts:
            if not part:
                continue
            if part.startswith("**"):
                set_font(p.add_run(part[2:-2]), size, True)
            elif part.startswith("`"):
                set_font(p.add_run(part[1:-1]), size - 1, font=MONO)
            else:
                set_font(p.add_run(part), size)

    def table(self, title, rows, widths=None, font_size=12):
        self.table_no += 1
        cap = self.doc.add_paragraph()
        fmt(cap, align=WD_ALIGN_PARAGRAPH.LEFT, indent=0, keep_next=True, before=6)
        set_font(cap.add_run(f"Таблица {self.table_no} – {title}"), 14)
        ncols = len(rows[0])
        t = self.doc.add_table(rows=0, cols=ncols)
        t.alignment = WD_TABLE_ALIGNMENT.CENTER
        for i, row in enumerate(rows):
            cells = t.add_row().cells
            for j in range(ncols):
                val = row[j] if j < len(row) else ""
                c = cells[j]
                set_cell_borders(c)
                p = c.paragraphs[0]
                fmt(p, align=WD_ALIGN_PARAGRAPH.CENTER if i == 0 else WD_ALIGN_PARAGRAPH.LEFT,
                    indent=0, spacing=1, hyphen=False)
                lines = val.split("\\n")
                for k, line in enumerate(lines):
                    if k:
                        p = c.add_paragraph()
                        fmt(p, align=WD_ALIGN_PARAGRAPH.LEFT, indent=0, spacing=1)
                    if i == 0:
                        set_font(p.add_run(line), font_size, True)
                    else:
                        self._inline(p, line, font_size)
        repeat_header(t.rows[0])
        # строки не разрываются между страницами; небольшая таблица целиком держится вместе
        # с подписью, у большой с подписью остаются хотя бы шапка и первая строка
        keep_rows = len(t.rows) - 1 if len(t.rows) <= 12 else 2
        for ri, row in enumerate(t.rows):
            tr_pr = row._tr.get_or_add_trPr()
            cs = OxmlElement("w:cantSplit")
            cs.set(qn("w:val"), "true")
            tr_pr.append(cs)
            if ri < keep_rows:
                for c in row.cells:
                    for cp in c.paragraphs:
                        cp.paragraph_format.keep_with_next = True
        if not widths:
            widths = auto_widths(rows)
        t.autofit = False
        tbl_pr = t._tbl.tblPr
        layout = OxmlElement("w:tblLayout")
        layout.set(qn("w:type"), "fixed")
        tbl_pr.append(layout)
        grid = t._tbl.tblGrid
        for j, gc in enumerate(grid.findall(qn("w:gridCol"))):
            gc.set(qn("w:w"), str(int(widths[j] * 567)))
        for row in t.rows:
            for j, w in enumerate(widths):
                row.cells[j].width = Cm(w)
        after = self.doc.add_paragraph()
        fmt(after, indent=0, spacing=1)
        return self.table_no

    def figure(self, path, title, width_cm=15, max_h_cm=12):
        self.figure_no += 1
        full = (self.base / path) if not Path(path).is_absolute() else Path(path)
        p = self.doc.add_paragraph()
        fmt(p, align=WD_ALIGN_PARAGRAPH.CENTER, indent=0, spacing=1, before=6, keep_next=True)
        if full.exists():
            from PIL import Image
            with Image.open(full) as im:
                w, h = im.size
            # высота не больше 12 см: рисунок с подписью помещается в оставшуюся часть страницы
            # и не «уезжает» на следующую, оставляя пустое место
            width = min(width_cm, max_h_cm * w / h)
            p.add_run().add_picture(str(full), width=Cm(width))
        else:
            set_font(p.add_run(f"[Место для рисунка: {path}]"), 14)
        cap = self.doc.add_paragraph()
        fmt(cap, align=WD_ALIGN_PARAGRAPH.CENTER, indent=0, after=6, hyphen=False)
        set_font(cap.add_run(f"Рисунок {self.figure_no} – {title}"), 14)
        return self.figure_no

    def formula(self, text, number):
        p = self.doc.add_paragraph()
        fmt(p, align=WD_ALIGN_PARAGRAPH.LEFT, indent=0, before=6, after=6)
        tabs = p.paragraph_format.tab_stops
        tabs.add_tab_stop(Cm(8.5), WD_TAB_ALIGNMENT.CENTER)
        tabs.add_tab_stop(Cm(17), WD_TAB_ALIGNMENT.RIGHT)
        set_font(p.add_run("\t" + text + "\t" + number), 14, italic=False)

    def code(self, lines):
        short = len(lines) <= 30
        for k, line in enumerate(lines):
            p = self.doc.add_paragraph()
            fmt(p, align=WD_ALIGN_PARAGRAPH.LEFT, indent=0, spacing=1, hyphen=False,
                keep_next=short and k < len(lines) - 1)
            p.paragraph_format.left_indent = Cm(0.5)
            run = p.add_run(line.replace("\t", "    ") if line else " ")
            set_font(run, 10, font=MONO)

    def listing_caption(self, title):
        """Подпись листинга над кодом: «Листинг N – Название»."""
        self.listing_no = getattr(self, "listing_no", 0) + 1
        cap = self.doc.add_paragraph()
        fmt(cap, align=WD_ALIGN_PARAGRAPH.LEFT, indent=0, keep_next=True, before=6, after=3)
        set_font(cap.add_run(f"Листинг {self.listing_no} – {title}"), 14)

    def page_break(self):
        p = self.doc.add_paragraph()
        p.add_run().add_break(WD_BREAK.PAGE)

    def raw_center(self, text, bold=False):
        p = self.doc.add_paragraph()
        fmt(p, align=WD_ALIGN_PARAGRAPH.CENTER, indent=0)
        set_font(p.add_run(text), 14, bold)

    # --- разбор note.txt
    def load(self, text):
        lines = text.splitlines()
        i = 0
        while i < len(lines):
            line = lines[i].rstrip()
            if not line.strip():
                i += 1
                continue
            if line.startswith("## "):
                self.heading2(line[3:].strip())
            elif line.startswith("# "):
                self.heading1(line[2:].strip())
            elif line.startswith("@appendix "):
                letter, title = [s.strip() for s in line[10:].split("|", 1)]
                self.appendix(letter, title)
            elif line.startswith(":table "):
                title = line[7:].strip()
                rows = []
                i += 1
                while i < len(lines) and lines[i].strip():
                    rows.append([c.strip() for c in lines[i].split("|")])
                    i += 1
                self.table(title, rows)
                continue
            elif line.startswith(":figure "):
                path, title = [s.strip() for s in line[8:].split("|", 1)]
                # снимки экрана чуть уже схем, чтобы записка не разрасталась
                self.figure(path, title, 13.5 if "screenshots" in path else 15, 7.5 if "screenshots" in path else 9.5)
            elif line.startswith(":formula "):
                f, n = [s.strip() for s in line[9:].rsplit("|", 1)]
                self.formula(f, n)
            elif line.startswith(":code"):
                title = line[5:].strip()
                if title:
                    self.listing_caption(title)
                block = []
                i += 1
                while i < len(lines) and not lines[i].startswith(":endcode"):
                    block.append(lines[i].rstrip())
                    i += 1
                self.code(block)
            elif line.startswith(":include "):
                path = self.base / line[9:].strip()
                # XML-комментарии «///» в печатном листинге не нужны — пояснения есть в тексте
                lines_ = [ln for ln in path.read_text(encoding="utf-8-sig").splitlines()
                          if not ln.strip().startswith("///")]
                # в приложениях листинги длинные — разрешаем разрыв между страницами
                for ln in lines_:
                    p = self.doc.add_paragraph()
                    fmt(p, align=WD_ALIGN_PARAGRAPH.LEFT, indent=0, spacing=1, hyphen=False)
                    set_font(p.add_run(ln.replace("\t", "    ") if ln else " "), 10, font=MONO)
            elif line.startswith(":pagebreak"):
                self.page_break()
            elif line.startswith(":raw-center "):
                self.raw_center(line[12:].strip())
            elif line.startswith(":signature"):
                self.signature()
            elif line.startswith("- "):
                self.bullet(line[2:].strip())
            else:
                self.paragraph(line.strip())
            i += 1

    def signature(self):
        p = self.doc.add_paragraph()
        fmt(p, align=WD_ALIGN_PARAGRAPH.LEFT, indent=0, before=24)
        set_font(p.add_run("«___» ____________ 2026 г.            ______________ / ______________"), 14)
        p2 = self.doc.add_paragraph()
        fmt(p2, align=WD_ALIGN_PARAGRAPH.LEFT, indent=0, spacing=1)
        set_font(p2.add_run("                                                                  (подпись)                   (ФИО)"), 10)


def renumber(text):
    """
    Сквозная нумерация ссылок «рисунок N», «таблица N», «листинг N» в тексте:
    ссылка получает номер ближайшего следующего за ней рисунка (таблицы, листинга).
    Так после удаления или добавления рисунков номера в тексте остаются верными.
    """
    lines = text.splitlines()
    body_end = next((i for i, l in enumerate(lines) if l.startswith("@appendix")), len(lines))
    kinds = {"fig": (":figure ", r"(рисун(?:ок|ке|ка|ку|ком))\s+(\d+)"),
             "tab": (":table ", r"((?<![а-яА-Я])таблиц(?:а|е|ы|у|ей))\s+(\d+)"),
             "lst": (":code ", r"(листинг(?:е|а|у|ом)?)\s+(\d+)")}
    for kind, (marker, rx) in kinds.items():
        positions = [i for i, l in enumerate(lines[:body_end]) if l.startswith(marker)]
        if not positions:
            continue

        def number_for(line_no):
            for k, pos in enumerate(positions, 1):
                if pos >= line_no:
                    return k
            return len(positions)

        for i in range(body_end):
            l = lines[i]
            if l.startswith((":figure", ":table", ":code", "#")):
                continue
            base = number_for(i)
            counter = iter(range(len(positions) + 1))
            # несколько ссылок в одной строке — на идущие подряд рисунки
            lines[i] = re.sub(rx, lambda m: f"{m.group(1)} {min(base + next(counter), len(positions))}", l)
    return "\n".join(lines)


def build(template, info, note_path, out_path):
    """Двухпроходная сборка: сначала узнаём страницы заголовков по PDF, затем пишем содержание."""
    note_path = Path(note_path)
    out_path = Path(out_path)
    text = renumber(note_path.read_text(encoding="utf-8"))

    def make(pages):
        b = NoteBuilder(template, info, note_path.parent)
        b.toc()
        b.load(text)
        b.toc_pages = pages
        b._render_toc()
        # Word обновит поля при открытии (номера страниц уже проставлены)
        return b

    b = make({})
    b.doc.save(out_path)
    pages = find_pages(out_path, [t for _, t in b.toc_entries], b.search_keys)
    b = make(pages)
    b.doc.save(out_path)
    # проверочный PDF рядом
    pages2 = find_pages(out_path, [t for _, t in b.toc_entries], b.search_keys, keep_pdf=True)
    return b, pages2


def find_pages(docx_path, titles, search_keys=None, keep_pdf=False):
    """Конвертирует DOCX в PDF через LibreOffice и ищет номер страницы каждого заголовка."""
    docx_path = Path(docx_path)
    with tempfile.TemporaryDirectory() as tmp:
        subprocess.run(["soffice", "--headless", "--convert-to", "pdf", "--outdir", tmp, str(docx_path)],
                       check=True, capture_output=True)
        pdf = Path(tmp) / (docx_path.stem + ".pdf")
        if keep_pdf:
            (docx_path.parent / pdf.name).write_bytes(pdf.read_bytes())
        out = subprocess.run(["pdftotext", "-layout", str(pdf), "-"], capture_output=True, text=True).stdout
    pages_text = out.split("\f")
    result = {}
    # Физическая страница N в PDF: 1 — титул, 2 — содержание (номер 2) → номер = N
    start_search = 2
    # пропускаем страницы самого содержания (в них много точек-заполнителей)
    while start_search < len(pages_text) and pages_text[start_search].count("....") >= 3:
        start_search += 1
    for title in titles:
        key = re.sub(r"\s+", " ", (search_keys or {}).get(title, title)).strip()
        for idx in range(start_search, len(pages_text)):
            page = re.sub(r"\s+", " ", pages_text[idx])
            if key in page:
                result[title] = idx + 1  # idx считается с 0
                start_search = idx
                break
    return result
