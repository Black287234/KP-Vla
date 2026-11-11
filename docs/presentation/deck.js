// Сборка презентации к защите: node docs/presentation/deck.js
// Содержание слайдов и текст речи — в content.json рядом с этим файлом.
const fs = require("fs");
const path = require("path");
const pptxgen = require("pptxgenjs");

const HERE = __dirname;
const cfg = JSON.parse(fs.readFileSync(path.join(HERE, "content.json"), "utf8"));
const T = cfg.theme; // цвета без «#»
const pres = new pptxgen();
pres.layout = "LAYOUT_WIDE"; // 13.33 x 7.5
pres.theme = { headFontFace: T.head, bodyFontFace: T.body };
pres.title = cfg.title;

const W = 13.33, H = 7.5, M = 0.6;

function img(p) { return path.resolve(HERE, p); }

function sizeImage(p, maxW, maxH) {
  // размеры PNG без сторонних библиотек: ширина и высота в заголовке IHDR
  const buf = fs.readFileSync(img(p));
  const w = buf.readUInt32BE(16), h = buf.readUInt32BE(20);
  const k = Math.min(maxW / w, maxH / h);
  return { w: w * k, h: h * k };
}

function frame(slide, dark) {
  slide.background = { color: dark ? T.dark : T.light };
}

function title(slide, text, num, dark) {
  slide.addText(text, { x: M, y: 0.35, w: W - 2 * M - 1, h: 0.8, fontFace: T.head, fontSize: 32, bold: true,
    color: dark ? T.onDark : T.ink, margin: 0, isTextBox: true });
  if (num) {
    slide.addShape(pres.shapes.OVAL, { x: W - M - 0.6, y: 0.42, w: 0.6, h: 0.6, fill: { color: T.accent } });
    slide.addText(String(num).padStart(2, "0"), { x: W - M - 0.6, y: 0.42, w: 0.6, h: 0.6, fontSize: 14, bold: true,
      color: T.onAccent, align: "center", valign: "middle", margin: 0, isTextBox: true });
  }
}

function bullets(slide, items, x, y, w, h, dark, size) {
  slide.addText(items.map((t, i) => ({ text: t, options: { bullet: { indent: 18 }, breakLine: i < items.length - 1 } })),
    { x, y, w, h, fontFace: T.body, fontSize: size || 18, color: dark ? T.onDark : T.ink, valign: "top",
      paraSpaceAfter: 10, margin: 0, isTextBox: true });
}

function picture(slide, p, x, y, maxW, maxH, caption, dark) {
  const s = sizeImage(p, maxW, maxH - (caption ? 0.4 : 0));
  const px = x + (maxW - s.w) / 2;
  slide.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: px - 0.06, y: y - 0.06, w: s.w + 0.12, h: s.h + 0.12,
    fill: { color: "FFFFFF" }, line: { color: T.line, width: 1 }, rectRadius: 0.08,
    shadow: { type: "outer", blur: 6, offset: 2, angle: 90, color: "000000", opacity: 0.15 } });
  slide.addImage({ path: img(p), x: px, y, w: s.w, h: s.h });
  if (caption) {
    slide.addText(caption, { x, y: y + s.h + 0.12, w: maxW, h: 0.35, fontSize: 12, italic: true,
      color: dark ? T.onDark : T.muted, align: "center", margin: 0, isTextBox: true });
  }
}

cfg.slides.forEach((s, idx) => {
  const slide = pres.addSlide();
  const dark = !!s.dark;
  frame(slide, dark);
  const n = idx + 1;

  if (s.type === "cover") {
    slide.addText(s.kicker, { x: M, y: 0.8, w: W - 2 * M, h: 0.4, fontSize: 14, bold: true, color: T.accent,
      charSpacing: 3, margin: 0, isTextBox: true });
    slide.addText(s.title, { x: M, y: 1.5, w: W - 2 * M, h: 2.2, fontFace: T.head, fontSize: 44, bold: true,
      color: T.onDark, valign: "top", margin: 0, isTextBox: true });
    slide.addText(s.lines.join("\n"), { x: M, y: 4.6, w: W - 2 * M, h: 1.6, fontSize: 16, color: T.onDarkMuted,
      valign: "top", margin: 0, paraSpaceAfter: 6, isTextBox: true });
  } else if (s.type === "cards") {
    title(slide, s.title, n, dark);
    if (s.lead) slide.addText(s.lead, { x: M, y: 1.35, w: W - 2 * M, h: 0.8, fontSize: 20, color: T.muted, margin: 0, isTextBox: true });
    const cols = s.cards.length, gap = 0.3;
    const cw = (W - 2 * M - gap * (cols - 1)) / cols, top = s.lead ? 2.4 : 1.8, ch = Math.min(4.2, H - top - 0.8);
    s.cards.forEach((c, i) => {
      const x = M + i * (cw + gap);
      slide.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: top, w: cw, h: ch, fill: { color: T.card },
        line: { color: T.line, width: 1 }, rectRadius: 0.12 });
      slide.addShape(pres.shapes.OVAL, { x: x + 0.3, y: top + 0.3, w: 0.7, h: 0.7, fill: { color: T.accent } });
      slide.addText(c.badge, { x: x + 0.3, y: top + 0.3, w: 0.7, h: 0.7, fontSize: 16, bold: true, color: T.onAccent,
        align: "center", valign: "middle", margin: 0, isTextBox: true });
      slide.addText(c.head, { x: x + 0.3, y: top + 1.2, w: cw - 0.6, h: 0.8, fontSize: 22, bold: true, color: T.ink,
        valign: "top", margin: 0, isTextBox: true });
      slide.addText(c.text, { x: x + 0.3, y: top + 2.0, w: cw - 0.6, h: ch - 2.2, fontSize: 17, color: T.ink,
        valign: "top", margin: 0, isTextBox: true });
    });
  } else if (s.type === "image-right" || s.type === "image-left") {
    title(slide, s.title, n, dark);
    const textW = s.textW || 4.6, gap = 0.4;
    const imgW = W - 2 * M - textW - gap;
    const left = s.type === "image-left";
    const tx = left ? M + imgW + gap : M, ix = left ? M : M + textW + gap;
    if (s.big) {
      slide.addText(s.big, { x: tx, y: 1.5, w: textW, h: 1.4, fontSize: 72, bold: true, color: T.accent, margin: 0, isTextBox: true });
      slide.addText(s.bigLabel, { x: tx, y: 2.9, w: textW, h: 0.8, fontSize: 18, bold: true, color: dark ? T.onDark : T.ink, margin: 0, isTextBox: true });
      bullets(slide, s.points, tx, 3.9, textW, H - 4.6, dark, 15);
    } else {
      bullets(slide, s.points, tx, 1.6, textW, H - 2.3, dark, s.size || 17);
    }
    picture(slide, s.image, ix, 1.5, imgW, H - 2.0, s.caption, dark);
  } else if (s.type === "two-images") {
    title(slide, s.title, n, dark);
    const gap = 0.4, iw = (W - 2 * M - gap) / 2;
    picture(slide, s.images[0], M, 1.5, iw, H - 2.6, s.captions[0], dark);
    picture(slide, s.images[1], M + iw + gap, 1.5, iw, H - 2.6, s.captions[1], dark);
    if (s.footer) slide.addText(s.footer, { x: M, y: H - 0.9, w: W - 2 * M, h: 0.5, fontSize: 15, color: T.muted, margin: 0, isTextBox: true });
  } else if (s.type === "table") {
    title(slide, s.title, n, dark);
    const rows = s.rows.map((r, i) => r.map((c) => ({ text: c, options: {
      bold: i === 0, color: i === 0 ? "FFFFFF" : T.ink, fill: { color: i === 0 ? T.accentDark : (i % 2 ? "FFFFFF" : T.card) },
      fontSize: 15, valign: "middle" } })));
    slide.addTable(rows, { x: M, y: 1.5, w: W - 2 * M, colW: s.colW, border: { type: "solid", pt: 0.5, color: T.line }, margin: 0.1, rowH: 0.48 });
    if (s.footer) slide.addText(s.footer, { x: M, y: H - 0.9, w: W - 2 * M, h: 0.5, fontSize: 15, color: T.muted, margin: 0, isTextBox: true });
  } else if (s.type === "steps") {
    title(slide, s.title, n, dark);
    const k = s.steps.length, gap = 0.25, sw = (W - 2 * M - gap * (k - 1)) / k;
    s.steps.forEach((st, i) => {
      const x = M + i * (sw + gap);
      slide.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 1.8, w: sw, h: 1.6, fill: { color: st.bad ? T.bad : T.accent },
        rectRadius: 0.1, line: { color: st.bad ? T.bad : T.accent } });
      slide.addText(st.head, { x: x + 0.15, y: 1.8, w: sw - 0.3, h: 1.6, fontSize: 18, bold: true, color: st.bad ? "FFFFFF" : T.onAccent,
        align: "center", valign: "middle", margin: 0, isTextBox: true });
      slide.addText(st.text, { x, y: 3.6, w: sw, h: 2.6, fontSize: 17, color: T.ink, valign: "top", margin: 0, isTextBox: true });
    });
  } else if (s.type === "closing") {
    slide.addText(s.title, { x: M, y: 2.3, w: W - 2 * M, h: 1.2, fontFace: T.head, fontSize: 44, bold: true, color: T.onDark,
      margin: 0, isTextBox: true });
    slide.addText(s.subtitle, { x: M, y: 3.6, w: W - 2 * M, h: 0.8, fontSize: 18, color: T.accent, margin: 0, isTextBox: true });
  }
  if (s.notes) slide.addNotes(s.notes);
});

pres.writeFile({ fileName: path.join(HERE, cfg.output) }).then((f) => console.log("Создано:", f));
