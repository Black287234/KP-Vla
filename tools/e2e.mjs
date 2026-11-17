// Сквозные сценарии проверки в браузере (Playwright для Node.js).
// Запуск при работающем приложении: node tools/e2e.mjs main | restart
// Фактический результат каждого сценария считывается со страницы или из ответа API
// и записывается в docs/checks/results.json вместе с ожидаемым результатом.
import { chromium } from "playwright";
import fs from "node:fs";
import path from "node:path";

const BASE = process.env.BASE || "http://localhost:5091";
const OUT = "docs/checks";
const SHOTS = path.join(OUT, "screens");
fs.mkdirSync(SHOTS, { recursive: true });
const FILE = path.join(OUT, "results.json");
const data = fs.existsSync(FILE) ? JSON.parse(fs.readFileSync(FILE, "utf8")) : { cases: [] };

const iso = (d) => new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
const day = (shift) => { const d = new Date(); d.setDate(d.getDate() + shift); return iso(d); };
const TODAY = day(0);
data.date = TODAY.split("-").reverse().join(".");

function record(id, title, expected, actual, ok, shot) {
  data.cases = data.cases.filter((c) => c.id !== id);
  data.cases.push({ id, title, expected, actual, ok: !!ok, shot: shot || null });
  console.log(`${ok ? "ВЫПОЛНЕН   " : "НЕ ВЫПОЛНЕН"} ${id} ${title} → ${actual}`);
}

async function snap(page, name) {
  await page.screenshot({ path: path.join(SHOTS, name + ".png"), fullPage: true });
  return name + ".png";
}

async function open(page, hash) {
  await page.goto(BASE + "/#" + hash);
  await page.waitForLoadState("networkidle");
}

async function message(page) {
  const t = await page.locator(".msg").allInnerTexts();
  return t.map((x) => x.replace(/\s+/g, " ").trim()).filter(Boolean).join(" | ");
}

async function submit(page) {
  await page.click("form#f button.btn");
  await page.waitForLoadState("networkidle");
  await page.waitForTimeout(300);
}

async function api(method, url, body, raw) {
  const r = await fetch(BASE + url, { method, headers: { "Content-Type": "application/json" },
    body: raw ?? (body ? JSON.stringify(body) : undefined) });
  const text = await r.text();
  let short = text;
  try { const j = JSON.parse(text); short = j.message ?? JSON.stringify(j); } catch { /* текст */ }
  return { status: r.status, text, short: short.replace(/\s+/g, " ").slice(0, 170) || "(пустой ответ)" };
}

async function productId(article) {
  const list = JSON.parse((await api("GET", "/api/products?archived=true")).text);
  return list.find((p) => p.article === article)?.id;
}

async function main(page) {
  await open(page, "/");
  const tiles = (await page.locator(".tile").allInnerTexts()).map((t) => t.replace(/\s+/g, " ")).join(" / ");
  record("Т01", "Сводка по складу", "Четыре показателя, список товаров к пополнению и последние операции",
    tiles, (await page.locator(".tile").count()) === 4, await snap(page, "01_summary"));

  await open(page, "/products?search=ручка");
  const found = await page.locator("table.list tbody tr td:nth-child(2)").allInnerTexts();
  record("Т02", "Поиск товара по слову «ручка»", "Найдены две ручки (шариковая и гелевая)",
    "Найдено: " + found.join("; "), found.length === 2, await snap(page, "02_search"));

  await open(page, "/products/new");
  await page.fill("[name=article]", "кт-0020");
  await page.selectOption("[name=category]", "Writing");
  await page.fill("[name=name]", "Ластик белый");
  await page.fill("[name=unit]", "шт.");
  await page.fill("[name=minQuantity]", "10");
  await submit(page);
  let msg = await message(page);
  record("Т03", "Добавление товара с артикулом «кт-0020» строчными буквами", "Товар добавлен под артикулом КТ-0020, открыта карточка",
    msg, msg.includes("КТ-0020 добавлен"), await snap(page, "03_product_added"));
  const pid = await productId("КТ-0020");

  await open(page, "/products/new");
  await page.fill("[name=article]", "КТ-0020");
  await page.selectOption("[name=category]", "Writing");
  await page.fill("[name=name]", "Ластик");
  await submit(page);
  msg = await message(page);
  record("Т04", "Повторное добавление артикула КТ-0020", "Отказ: товар с таким артикулом уже есть", msg, msg.includes("уже есть"));

  await open(page, "/products/new");
  await page.fill("[name=article]", "12");
  await page.fill("[name=minQuantity]", "-5");
  await submit(page);
  const fieldErrors = await page.locator(".err").allInnerTexts();
  record("Т05", "Форма товара с неверными данными (артикул «12», без наименования и группы, минимум −5)",
    "Общее сообщение и пояснения у каждого неверного поля", `${await message(page)}; ошибок у полей: ${fieldErrors.length} (${fieldErrors.join("; ")})`,
    fieldErrors.length >= 4, await snap(page, "05_validation"));

  await open(page, `/receipt?product=${pid}`);
  await page.fill("[name=quantity]", "30");
  await page.fill("[name=supplier]", "ООО «Офис-Комплект»");
  await page.fill("[name=documentNumber]", "ТН-2050");
  await submit(page);
  msg = await message(page);
  record("Т06", "Поступление 30 шт. товара КТ-0020", "Поступление оформлено, остаток 30 шт.", msg, msg.includes("Остаток: 30"), await snap(page, "06_receipt"));

  await open(page, `/issue?product=${pid}`);
  const shown = (await page.locator("#bal").innerText()).trim();
  await page.fill("[name=quantity]", "12");
  await page.selectOption("[name=departmentId]", { label: "Учебная часть" });
  await page.fill("[name=recipient]", "Ковалёва И. П.");
  await submit(page);
  msg = await message(page);
  record("Т07", "Выдача 12 шт. в учебную часть", "Выдача оформлена, остаток 18 шт.", `${shown}; ${msg}`, msg.includes("Остаток: 18"), await snap(page, "07_issue"));

  await open(page, `/issue?product=${pid}`);
  await page.fill("[name=quantity]", "50");
  await page.selectOption("[name=departmentId]", { label: "Бухгалтерия" });
  await page.fill("[name=recipient]", "Мартынова С. О.");
  await submit(page);
  msg = await message(page);
  record("Т08", "Выдача 50 шт. при остатке 18 шт.", "Отказ: недостаточно товара, указан остаток", msg,
    msg.includes("на складе 18"), await snap(page, "08_issue_too_much"));

  await page.fill("[name=quantity]", "1");
  await page.fill("[name=date]", day(1));
  await submit(page);
  msg = await message(page);
  record("Т09", "Выдача с завтрашней датой", "Отказ: дата не может быть позже сегодняшней", msg, msg.includes("позже сегодняшней"));

  await open(page, `/issue?product=${pid}`);
  await page.fill("[name=quantity]", "10");
  await page.selectOption("[name=departmentId]", { label: "Библиотека" });
  await page.fill("[name=recipient]", "Тарасова Н. М.");
  await submit(page);
  await open(page, "/low");
  const lowRow = page.locator("table.list tbody tr", { hasText: "КТ-0020" });
  const lowText = (await lowRow.count()) ? (await lowRow.innerText()).replace(/\s+/g, " ") : "товар в списке отсутствует";
  record("Т10", "Контроль минимума: после выдачи ещё 10 шт. остаток 8 при минимуме 10", "Товар в списке «К пополнению», не хватает 2 шт.",
    lowText, lowText.includes("8") && lowText.includes("2 шт."), await snap(page, "10_low_stock"));

  if (await lowRow.count()) {
    await lowRow.locator("a.btn").click();
    await page.waitForLoadState("networkidle");
    const qty = await page.inputValue("[name=quantity]");
    const prod = await page.locator("[name=productId] option:checked").innerText();
    record("Т11", "Переход «Оформить поступление» из списка к пополнению", "Форма поступления с выбранным товаром и количеством 2",
      `товар: ${prod}; количество: ${qty}`, qty === "2" && prod.includes("КТ-0020"));
  }

  await open(page, `/products/${pid}`);
  await page.click("#arch");
  await page.waitForTimeout(400);
  msg = await message(page);
  record("Т12", "Вывод из ассортимента товара с остатком 8 шт.", "Отказ: товар есть на складе", msg, msg.includes("есть на складе"));

  const rows = await page.locator("table.list tbody tr").count();
  const balances = await page.locator("table.list tbody tr td:last-child").allInnerTexts();
  record("Т13", "История движения в карточке товара", "Три операции, остаток после каждой: 8, 18, 30 (сверху — последняя)",
    `операций: ${rows}; остатки: ${balances.join(", ")}`, rows === 3 && balances.join(",") === "8,18,30", await snap(page, "13_product_card"));

  // Выдача задним числом: на дату операции товара ещё не было на складе
  await open(page, `/issue?product=${pid}`);
  await page.fill("[name=quantity]", "5");
  await page.fill("[name=date]", day(-10));
  await page.selectOption("[name=departmentId]", { label: "Бухгалтерия" });
  await page.fill("[name=recipient]", "Мартынова С. О.");
  await submit(page);
  msg = await message(page);
  const hist = JSON.parse((await api("GET", `/api/products/${pid}/movements`)).text);
  const minBalance = Math.min(...hist.map((h) => h.balanceAfter));
  record("Т14", "Выдача 5 шт. датой 10 дней назад — раньше первого поступления товара",
    "Отказ: на дату выдачи товара на складе не было", `${msg}; наименьший остаток в истории: ${minBalance}`,
    msg.includes("на дату") && minBalance >= 0, await snap(page, "14_backdated_issue"));

  // Изменение единицы измерения товара, по которому уже есть операции
  await open(page, `/products/${pid}/edit`);
  await page.fill("[name=unit]", "коробка");
  await submit(page);
  msg = await message(page);
  record("Т15", "Изменение единицы измерения КТ-0020 со «шт.» на «коробка» после операций",
    "Отказ: по товару есть операции, единица измерения не меняется", msg, msg.includes("единиц"), await snap(page, "15_unit_change"));
  if (msg.includes("сохранены")) await api("PUT", `/api/products/${pid}`, { article: "КТ-0020", name: "Ластик белый", category: "Writing", unit: "шт.", minQuantity: 10 });

  await open(page, `/history?kind=Issue&productId=${pid}`);
  const hrows = await page.locator("table.list tbody tr").count();
  record("Т16", "История: только выдачи товара КТ-0020", "Только операции «Выдача» этого товара",
    `строк: ${hrows}; виды: ${[...new Set(await page.locator("table.list tbody .kind").allInnerTexts())].join(", ")}`,
    hrows >= 2 && (await page.locator("table.list tbody .kind.in").count()) === 0, await snap(page, "16_history"));

  await open(page, `/history?from=${TODAY}&to=${day(-5)}`);
  msg = await message(page);
  record("Т17", "История с началом периода позже окончания", "Сообщение «Начало периода позже его окончания»", msg, msg.includes("позже его окончания"));

  await open(page, `/report?from=${day(-30)}&to=${TODAY}`);
  const rep = (await page.locator("table.list tbody tr").first().innerText()).replace(/\s+/g, " ");
  record("Т18", "Отчёт о расходе по подразделениям за 30 дней", "Подразделения по убыванию выданного количества с долей",
    "Первая строка: " + rep, (await page.locator("table.list tbody tr").count()) === 6, await snap(page, "18_report"));

  await open(page, "/departments");
  await page.fill("[name=name]", "Методический кабинет");
  await page.fill("[name=head]", "Белова Ольга Ивановна");
  await submit(page);
  const m1 = await message(page);
  await page.fill("[name=name]", "  методический   КАБИНЕТ ");
  await page.fill("[name=head]", "Белова Ольга Ивановна");
  await submit(page);
  const m2 = await message(page);
  record("Т19", "Добавление подразделения и повтор названия в другом регистре с лишними пробелами",
    "Первое добавлено, повтор отклонён", `${m1} / ${m2}`, m1.includes("добавлено") && m2.includes("уже есть"), await snap(page, "19_departments"));

  const bad = await api("POST", "/api/movements/receipt", null, '{"productId": 2, "quantity": 5, "date": "32.13.2026", "supplier": "ООО"}');
  record("Т20", "API: дата в неверном формате «32.13.2026»", "400 и сообщение на русском языке в поле message",
    `HTTP ${bad.status}: ${bad.short}`, bad.status === 400 && /^\{"message":"[А-Я]/.test(bad.text));

  // Одновременные выдачи
  const p6 = await productId("КТ-0006");
  const before = JSON.parse((await api("GET", "/api/stock")).text).find((l) => l.productId === p6).quantity;
  const portion = Math.max(1, Math.ceil(before / 4));
  const results = await Promise.all(Array.from({ length: 10 }, () =>
    api("POST", "/api/movements/issue", { productId: p6, quantity: portion, date: TODAY, departmentId: 1, recipient: "Ковалёва И. П." })));
  const after = JSON.parse((await api("GET", "/api/stock")).text).find((l) => l.productId === p6).quantity;
  const ok = results.filter((r) => r.status === 201).length;
  record("Т21", `API: 10 одновременных выдач по ${portion} шт. при остатке ${before} шт.`,
    "Выдано не больше остатка, остаток не становится отрицательным",
    `успешно: ${ok}, отклонено: ${results.filter((r) => r.status === 409).length}, остаток после: ${after}`,
    after >= 0 && ok * portion <= before && after === before - ok * portion);

  await open(page, "/products/9999");
  record("Т22", "Карточка несуществующего товара", "Сообщение «Запись не найдена»", await page.locator("h1").innerText(),
    (await page.locator("h1").innerText()).includes("не найдена"), await snap(page, "22_not_found"));

  await open(page, "/no-such-page");
  record("Т23", "Несуществующий раздел интерфейса", "Сообщение «Страница не найдена»", await page.locator("h1").innerText(),
    (await page.locator("h1").innerText()).includes("не найдена"));

  await open(page, "/stock");
  record("Т24", "Ведомость остатков", "Все товары ассортимента с поступлением, выдачей, остатком и состоянием",
    `строк: ${await page.locator("table.list tbody tr").count()}, ниже минимума: ${await page.locator("table.list .tag.bad").count()}`,
    (await page.locator("table.list tbody tr").count()) >= 19, await snap(page, "24_stock"));
}

async function restart(page) {
  const pid = await productId("КТ-0020");
  const hist = pid ? JSON.parse((await api("GET", `/api/products/${pid}/movements`)).text) : [];
  await open(page, `/products/${pid}`);
  record("Т25", "Сохранность данных после перезапуска приложения", "Товар КТ-0020 и его операции сохранены",
    `товар найден: ${pid ? "да" : "нет"}; операций: ${hist.length}; остаток: ${hist[0]?.balanceAfter}`,
    !!pid && hist.length >= 3, await snap(page, "25_after_restart"));
}

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1360, height: 860 }, locale: "ru-RU" });
if (process.argv[2] === "restart") await restart(page); else await main(page);
await browser.close();
data.cases.sort((a, b) => a.id.localeCompare(b.id));
fs.writeFileSync(FILE, JSON.stringify(data, null, 2));
