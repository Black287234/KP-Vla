// Одностраничное приложение без сторонних библиотек.
// Маршрут — часть адреса после «#», данные запрашиваются у API функцией fetch.
"use strict";

const view = document.getElementById("view");
const UNITS = ["шт.", "пачка", "упаковка", "коробка", "набор", "рулон"];
let categories = [];

// ---------------------------------------------------------------- вспомогательные функции
function esc(s) {
  return String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

/** Запрос к API. При ошибке выбрасывает объект { message, errors } из ответа сервера. */
async function api(url, method = "GET", body) {
  const r = await fetch(url, {
    method,
    headers: body ? { "Content-Type": "application/json" } : {},
    body: body ? JSON.stringify(body) : undefined
  });
  const text = await r.text();
  const data = text ? JSON.parse(text) : null;
  if (!r.ok) throw Object.assign(new Error(data?.message || `Ошибка ${r.status}`), { status: r.status, errors: data?.errors || {} });
  return data;
}

function flash(text, kind = "good") {
  sessionStorage.setItem("flash", JSON.stringify({ text, kind }));
}

function takeFlash() {
  const f = sessionStorage.getItem("flash");
  sessionStorage.removeItem("flash");
  if (!f) return "";
  const { text, kind } = JSON.parse(f);
  return `<div class="msg ${kind}" role="status">${esc(text)}</div>`;
}

/** Вывод ошибки сервера над формой и у полей. */
function showErrors(form, e) {
  form.querySelectorAll(".err").forEach((x) => x.remove());
  form.querySelectorAll(".invalid").forEach((x) => x.classList.remove("invalid"));
  const box = form.querySelector(".form-msg");
  box.innerHTML = `<div class="msg bad" role="alert">${esc(e.message)}</div>`;
  for (const [field, list] of Object.entries(e.errors || {})) {
    const input = form.elements[field];
    if (!input) continue;
    input.classList.add("invalid");
    input.insertAdjacentHTML("afterend", `<span class="err">${esc(list.join("; "))}</span>`);
  }
}

function formData(form) {
  const o = {};
  for (const el of form.elements) {
    if (!el.name) continue;
    if (el.type === "checkbox") o[el.name] = el.checked;
    else if (el.type === "number") o[el.name] = el.value === "" ? null : Number(el.value);
    else o[el.name] = el.value === "" ? null : el.value;
  }
  return o;
}

function options(list, selected, empty) {
  return (empty ? `<option value="">${esc(empty)}</option>` : "") +
    list.map(([v, t]) => `<option value="${esc(v)}" ${String(v) === String(selected ?? "") ? "selected" : ""}>${esc(t)}</option>`).join("");
}

function query(params) {
  const q = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v !== null && v !== undefined && v !== "" && v !== false) q.set(k, v);
  const s = q.toString();
  return s ? "?" + s : "";
}

// ---------------------------------------------------------------- страницы
const pages = {};

pages["/products"] = async (params) => {
  const items = await api("/api/products" + query({ search: params.get("search"), category: params.get("category"), archived: params.get("archived") }));
  view.innerHTML = `
    ${takeFlash()}
    <div class="head"><div><h1>Справочник товаров</h1><p class="lead">Найдено позиций: ${items.length}</p></div>
      <a class="btn" href="#/products/new">Добавить товар</a></div>
    <form class="box filters" id="f">
      <label>Поиск<input name="search" value="${esc(params.get("search"))}" placeholder="артикул или наименование"></label>
      <label>Группа<select name="category">${options(categories.map((c) => [c.code, c.name]), params.get("category"), "все группы")}</select></label>
      <label class="check"><input type="checkbox" name="archived" ${params.get("archived") ? "checked" : ""}> выведенные из ассортимента</label>
      <button class="btn">Найти</button><a class="btn ghost" href="#/products">Сбросить</a>
    </form>
    ${items.length === 0 ? `<div class="msg info">Товары, удовлетворяющие условиям, не найдены.</div>` : `
    <table class="list">
      <thead><tr><th>Артикул</th><th>Наименование</th><th>Группа</th><th>Ед. изм.</th><th class="n">Мин. остаток</th><th></th></tr></thead>
      <tbody>${items.map((p) => `<tr class="${p.isArchived ? "off" : ""}"><td>${esc(p.article)}</td><td>${esc(p.name)}${p.isArchived ? ' <span class="tag">в архиве</span>' : ""}</td>
        <td>${esc(p.categoryName)}</td><td>${esc(p.unit)}</td><td class="n">${p.minQuantity}</td>
        <td class="act"><a href="#/products/${p.id}">Карточка</a></td></tr>`).join("")}</tbody>
    </table>`}`;
  document.getElementById("f").addEventListener("submit", (ev) => {
    ev.preventDefault();
    const d = formData(ev.target);
    location.hash = "#/products" + query({ search: d.search, category: d.category, archived: d.archived ? "true" : "" });
  });
};

async function productForm(id) {
  const p = id ? await api(`/api/products/${id}`) : { article: "", name: "", category: "", unit: "шт.", minQuantity: 0 };
  view.innerHTML = `
    <h1>${id ? "Изменение товара " + esc(p.article) : "Новый товар"}</h1>
    <p class="lead">Артикул указывается в виде КТ-0001 и должен быть уникальным.</p>
    <form class="box form" id="f" novalidate>
      <div class="form-msg"></div>
      <div class="grid2">
        <label>Артикул<input name="article" value="${esc(p.article)}" maxlength="7"></label>
        <label>Группа товаров<select name="category">${options(categories.map((c) => [c.code, c.name]), p.category, "выберите группу")}</select></label>
        <label class="wide">Наименование<input name="name" value="${esc(p.name)}" maxlength="120"></label>
        <label>Единица измерения<input name="unit" value="${esc(p.unit)}" list="units" maxlength="15"></label>
        <label>Минимальный остаток<input name="minQuantity" type="number" min="0" value="${p.minQuantity}"></label>
      </div>
      <datalist id="units">${UNITS.map((u) => `<option value="${u}">`).join("")}</datalist>
      <div class="buttons"><button class="btn">Сохранить</button><a class="btn ghost" href="${id ? "#/products/" + id : "#/products"}">Отмена</a></div>
    </form>`;
  const form = document.getElementById("f");
  form.addEventListener("submit", async (ev) => {
    ev.preventDefault();
    try {
      const saved = await api(id ? `/api/products/${id}` : "/api/products", id ? "PUT" : "POST", formData(form));
      flash(id ? `Сведения о товаре ${saved.article} сохранены` : `Товар ${saved.article} добавлен в справочник`);
      location.hash = `#/products/${saved.id}`;
    } catch (e) { showErrors(form, e); }
  });
}

pages["/products/new"] = () => productForm(null);

async function productCard(id) {
  const [p, history] = await Promise.all([api(`/api/products/${id}`), api(`/api/products/${id}/movements`)]);
  const balance = history.length ? history[0].balanceAfter : 0;
  view.innerHTML = `
    ${takeFlash()}
    <div class="head"><div><h1>${esc(p.name)}</h1><p class="lead">Артикул ${esc(p.article)} · ${esc(p.categoryName)}</p></div>
      <a class="btn ghost" href="#/products/${p.id}/edit">Изменить</a></div>
    <div class="box"><dl class="facts">
      <dt>Единица измерения</dt><dd>${esc(p.unit)}</dd>
      <dt>Остаток на складе</dt><dd><b>${balance} ${esc(p.unit)}</b>${!p.isArchived && balance < p.minQuantity ? ' <span class="tag bad">ниже минимума</span>' : ""}</dd>
      <dt>Минимальный остаток</dt><dd>${p.minQuantity}</dd>
      <dt>Состояние</dt><dd>${p.isArchived ? '<span class="tag">выведен из ассортимента</span>' : "в ассортименте"}</dd>
    </dl></div>
    <div class="box"><div class="form-msg" id="m"></div>
      <button class="btn ${p.isArchived ? "" : "danger"}" id="arch">${p.isArchived ? "Вернуть в ассортимент" : "Вывести из ассортимента"}</button></div>
    ${p.isArchived ? "" : `<p><a class="btn" href="#/receipt?product=${p.id}">Поступление</a> <a class="btn" href="#/issue?product=${p.id}">Выдача</a></p>`}
    <h2>История движения</h2>
    ${history.length === 0 ? `<div class="msg info">Операций по товару нет.</div>` : `
    <table class="list"><thead><tr><th>Дата</th><th>Операция</th><th class="n">Кол-во</th><th>Поставщик / получатель</th><th class="n">Остаток после</th></tr></thead>
      <tbody>${movementRows(history, false, true)}</tbody></table>`}
    <p><a href="#/products">К справочнику</a></p>`;
  document.getElementById("arch").addEventListener("click", async () => {
    try {
      await api(`/api/products/${p.id}/${p.isArchived ? "restore" : "archive"}`, "POST");
      flash(p.isArchived ? "Товар возвращён в ассортимент" : "Товар выведен из ассортимента");
      render();
    } catch (e) { document.getElementById("m").innerHTML = `<div class="msg bad" role="alert">${esc(e.message)}</div>`; }
  });
}

pages["/departments"] = async () => {
  const list = await api("/api/departments");
  view.innerHTML = `
    ${takeFlash()}
    <h1>Подразделения</h1>
    <p class="lead">Получатели канцелярских товаров. Подразделений: ${list.length}</p>
    <table class="list"><thead><tr><th>Подразделение</th><th>Руководитель</th></tr></thead>
      <tbody>${list.map((d) => `<tr><td>${esc(d.name)}</td><td>${esc(d.head)}</td></tr>`).join("")}</tbody></table>
    <h2>Новое подразделение</h2>
    <form class="box form" id="f" novalidate>
      <div class="form-msg"></div>
      <div class="grid2"><label>Название<input name="name" maxlength="80"></label><label>Руководитель (ФИО)<input name="head" maxlength="100"></label></div>
      <div class="buttons"><button class="btn">Добавить</button></div>
    </form>`;
  const form = document.getElementById("f");
  form.addEventListener("submit", async (ev) => {
    ev.preventDefault();
    try {
      const d = await api("/api/departments", "POST", formData(form));
      flash(`Подразделение «${d.name}» добавлено`);
      render();
    } catch (e) { showErrors(form, e); }
  });
};


// ---------------------------------------------------------------- движение товаров
const KIND = { Receipt: "Поступление", Issue: "Выдача" };
const fmtDate = (iso) => iso ? iso.split("-").reverse().join(".") : "";
const todayIso = () => new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 10);

function movementRows(list, withProduct, withBalance) {
  return list.map((m) => `<tr>
    <td>${fmtDate(m.date)}</td>
    <td><span class="kind ${m.kind === "Receipt" ? "in" : "out"}">${KIND[m.kind]}</span></td>
    ${withProduct ? `<td><a href="#/products/${m.productId}">${esc(m.article)}</a> ${esc(m.product)}</td>` : ""}
    <td class="n">${m.kind === "Receipt" ? "+" : "−"}${m.quantity} ${esc(m.unit)}</td>
    <td>${m.kind === "Receipt" ? `${esc(m.supplier)}${m.documentNumber ? ", " + esc(m.documentNumber) : ""}` : `${esc(m.department)}, ${esc(m.recipient)}`}</td>
    ${withBalance ? `<td class="n"><b>${m.balanceAfter}</b></td>` : ""}
  </tr>`).join("");
}

pages["/"] = async () => {
  const s = await api("/api/reports/summary");
  const low = await api("/api/stock/low");
  view.innerHTML = `
    <h1>Сводка по складу</h1>
    <p class="lead">На ${fmtDate(s.today)}</p>
    <div class="tiles">
      <div class="tile"><b>${s.positions}</b>позиций в ассортименте</div>
      <div class="tile"><b>${s.inStock}</b>есть на складе</div>
      <div class="tile warn"><b>${s.belowMinimum}</b>ниже минимального остатка</div>
      <div class="tile"><b>${s.receiptsThisMonth} / ${s.issuesThisMonth}</b>поступлений / выдач за месяц</div>
    </div>
    <div class="cols">
      <section><h2>Требуют пополнения</h2>
        ${low.length ? `<table class="list"><thead><tr><th>Товар</th><th class="n">Остаток</th><th class="n">Минимум</th></tr></thead>
        <tbody>${low.slice(0, 6).map((l) => `<tr><td><a href="#/products/${l.productId}">${esc(l.article)}</a> ${esc(l.name)}</td>
          <td class="n bad">${l.quantity}</td><td class="n">${l.minQuantity}</td></tr>`).join("")}</tbody></table>
          <p><a href="#/low">Весь список (${low.length})</a></p>` : `<div class="msg good">Все товары выше минимального остатка.</div>`}
      </section>
      <section><h2>Последние операции</h2>
        <table class="list"><thead><tr><th>Дата</th><th>Операция</th><th>Товар</th><th class="n">Кол-во</th><th>Контрагент</th></tr></thead>
        <tbody>${movementRows(s.recent, true, false)}</tbody></table>
        <p><a href="#/history">Вся история</a></p>
      </section>
    </div>`;
};

pages["/stock"] = async (params) => {
  const items = await api("/api/stock");
  const onlyLow = params.get("low") === "1";
  const shown = onlyLow ? items.filter((l) => l.belowMinimum) : items;
  view.innerHTML = `
    <h1>Остатки товаров</h1>
    <p class="lead">Остаток рассчитывается автоматически: поступило − выдано. Позиций: ${shown.length}</p>
    <div class="box filters"><a class="btn ${onlyLow ? "ghost" : ""}" href="#/stock">Все товары</a>
      <a class="btn ${onlyLow ? "" : "ghost"}" href="#/stock?low=1">Только ниже минимума</a></div>
    <table class="list">
      <thead><tr><th>Артикул</th><th>Наименование</th><th>Ед. изм.</th><th class="n">Поступило</th><th class="n">Выдано</th><th class="n">Остаток</th><th class="n">Минимум</th><th>Состояние</th></tr></thead>
      <tbody>${shown.map((l) => `<tr><td><a href="#/products/${l.productId}">${esc(l.article)}</a></td><td>${esc(l.name)}</td><td>${esc(l.unit)}</td>
        <td class="n">${l.received}</td><td class="n">${l.issued}</td><td class="n"><b>${l.quantity}</b></td><td class="n">${l.minQuantity}</td>
        <td>${l.belowMinimum ? `<span class="tag bad">ниже минимума на ${l.shortage}</span>` : `<span class="tag ok">норма</span>`}</td></tr>`).join("")}</tbody>
    </table>`;
};

pages["/low"] = async () => {
  const items = await api("/api/stock/low");
  view.innerHTML = `
    <h1>Товары к пополнению</h1>
    <p class="lead">Остаток ниже минимального количества. Сначала — товары с наибольшей относительной нехваткой.</p>
    ${items.length === 0 ? `<div class="msg good">Все товары выше минимального остатка.</div>` : `
    <table class="list">
      <thead><tr><th>Артикул</th><th>Наименование</th><th class="n">Остаток</th><th class="n">Минимум</th><th class="n">Не хватает</th><th></th></tr></thead>
      <tbody>${items.map((l) => `<tr><td>${esc(l.article)}</td><td>${esc(l.name)}</td><td class="n bad"><b>${l.quantity}</b></td>
        <td class="n">${l.minQuantity}</td><td class="n">${l.shortage} ${esc(l.unit)}</td>
        <td class="act"><a class="btn small" href="#/receipt?product=${l.productId}&qty=${l.shortage}">Оформить поступление</a></td></tr>`).join("")}</tbody>
    </table>`}`;
};

async function productPicker(selected) {
  const stock = await api("/api/stock");
  return { stock, html: options(stock.map((l) => [l.productId, `${l.article} — ${l.name} (остаток ${l.quantity} ${l.unit})`]), selected, "выберите товар") };
}

pages["/receipt"] = async (params) => {
  const { html } = await productPicker(params.get("product"));
  view.innerHTML = `
    ${takeFlash()}
    <h1>Поступление товара</h1>
    <p class="lead">Оформление прихода по накладной поставщика. Дата — не позже сегодняшней и не ранее чем за 30 дней.</p>
    <form class="box form" id="f" novalidate>
      <div class="form-msg"></div>
      <div class="grid2">
        <label class="wide">Товар<select name="productId">${html}</select></label>
        <label>Количество<input name="quantity" type="number" min="1" value="${esc(params.get("qty") || "")}"></label>
        <label>Дата поступления<input name="date" type="date" value="${todayIso()}"></label>
        <label>Поставщик<input name="supplier" maxlength="100" list="suppliers"></label>
        <label>Номер накладной<input name="documentNumber" maxlength="30"></label>
        <label class="wide">Комментарий<input name="comment" maxlength="200"></label>
      </div>
      <datalist id="suppliers"><option value="ООО «Офис-Комплект»"><option value="ИП Гарифуллин Р. Р."></datalist>
      <div class="buttons"><button class="btn">Оформить поступление</button></div>
    </form>`;
  bindMovementForm("receipt");
};

pages["/issue"] = async (params) => {
  const { html, stock } = await productPicker(params.get("product"));
  const deps = await api("/api/departments");
  view.innerHTML = `
    ${takeFlash()}
    <h1>Выдача товара</h1>
    <p class="lead">Выдача подразделению. Нельзя выдать больше, чем есть на складе.</p>
    <form class="box form" id="f" novalidate>
      <div class="form-msg"></div>
      <div class="grid2">
        <label class="wide">Товар<select name="productId">${html}</select></label>
        <div class="wide balance" id="bal"></div>
        <label>Количество<input name="quantity" type="number" min="1"></label>
        <label>Дата выдачи<input name="date" type="date" value="${todayIso()}"></label>
        <label>Подразделение<select name="departmentId">${options(deps.map((d) => [d.id, d.name]), null, "выберите подразделение")}</select></label>
        <label>Получатель (ФИО)<input name="recipient" maxlength="100"></label>
        <label class="wide">Комментарий<input name="comment" maxlength="200"></label>
      </div>
      <div class="buttons"><button class="btn">Оформить выдачу</button></div>
    </form>`;
  const form = document.getElementById("f");
  const showBalance = () => {
    const l = stock.find((x) => String(x.productId) === form.elements.productId.value);
    document.getElementById("bal").innerHTML = l
      ? `На складе: <b>${l.quantity} ${esc(l.unit)}</b>${l.belowMinimum ? ` <span class="tag bad">ниже минимума (${l.minQuantity})</span>` : ""}` : "";
  };
  form.elements.productId.addEventListener("change", showBalance);
  showBalance();
  bindMovementForm("issue");
};

function bindMovementForm(kind) {
  const form = document.getElementById("f");
  form.addEventListener("submit", async (ev) => {
    ev.preventDefault();
    try {
      const m = await api(`/api/movements/${kind}`, "POST", formData(form));
      flash(`${m.kind === "Receipt" ? "Поступление оформлено" : "Выдача оформлена"}: ${m.article} ${m.product}, ${m.quantity} ${m.unit}; остаток на складе: ${m.balanceAfter} ${m.unit}`);
      render();
    } catch (e) { showErrors(form, e); }
  });
}

pages["/history"] = async (params) => {
  const [stock, deps] = await Promise.all([api("/api/stock?archived=true"), api("/api/departments")]);
  const f = Object.fromEntries(params);
  let list = [], problem = "";
  try {
    list = await api("/api/movements" + query({ from: f.from, to: f.to, productId: f.productId, departmentId: f.departmentId, kind: f.kind }));
  } catch (e) { problem = e.message; }
  view.innerHTML = `
    <h1>История движения</h1>
    <p class="lead">Операций: ${list.length}</p>
    <form class="box filters" id="f">
      <label>С<input type="date" name="from" value="${esc(f.from)}"></label>
      <label>По<input type="date" name="to" value="${esc(f.to)}"></label>
      <label>Операция<select name="kind">${options([["Receipt", "Поступление"], ["Issue", "Выдача"]], f.kind, "все")}</select></label>
      <label>Товар<select name="productId">${options(stock.map((l) => [l.productId, `${l.article} ${l.name}`]), f.productId, "все товары")}</select></label>
      <label>Подразделение<select name="departmentId">${options(deps.map((d) => [d.id, d.name]), f.departmentId, "все")}</select></label>
      <button class="btn">Показать</button><a class="btn ghost" href="#/history">Сбросить</a>
    </form>
    ${problem ? `<div class="msg bad">${esc(problem)}</div>` : list.length === 0 ? `<div class="msg info">Операции не найдены.</div>` : `
    <table class="list"><thead><tr><th>Дата</th><th>Операция</th><th>Товар</th><th class="n">Кол-во</th><th>Поставщик / получатель</th></tr></thead>
      <tbody>${movementRows(list, true, false)}</tbody></table>`}`;
  document.getElementById("f").addEventListener("submit", (ev) => {
    ev.preventDefault();
    location.hash = "#/history" + query(formData(ev.target));
  });
};

pages["/report"] = async (params) => {
  const t = todayIso();
  const from = params.get("from") || t.slice(0, 8) + "01", to = params.get("to") || t;
  let rows = [], problem = "";
  try { rows = await api(`/api/reports/consumption?from=${from}&to=${to}`); } catch (e) { problem = e.message; }
  const total = rows.reduce((a, r) => a + r.quantity, 0);
  view.innerHTML = `
    <h1>Расход по подразделениям</h1>
    <p class="lead">Выдано единиц товара за период: ${total}</p>
    <form class="box filters" id="f">
      <label>С<input type="date" name="from" value="${from}"></label><label>По<input type="date" name="to" value="${to}"></label>
      <button class="btn">Сформировать</button></form>
    ${problem ? `<div class="msg bad">${esc(problem)}</div>` : `
    <table class="list"><thead><tr><th>Подразделение</th><th>Руководитель</th><th class="n">Выдач</th><th class="n">Позиций</th><th class="n">Единиц</th><th>Доля</th></tr></thead>
      <tbody>${rows.map((r) => `<tr><td>${esc(r.department)}</td><td>${esc(r.head)}</td><td class="n">${r.operations}</td><td class="n">${r.positions}</td>
        <td class="n"><b>${r.quantity}</b></td><td><span class="bar"><i style="width:${total ? Math.round(100 * r.quantity / total) : 0}%"></i></span>
        ${total ? Math.round(100 * r.quantity / total) : 0} %</td></tr>`).join("")}</tbody></table>`}`;
  document.getElementById("f").addEventListener("submit", (ev) => {
    ev.preventDefault();
    location.hash = "#/report" + query(formData(ev.target));
  });
};

// ---------------------------------------------------------------- маршрутизация
const dynamic = [
  [/^\/products\/(\d+)$/, (m) => productCard(m[1])],
  [/^\/products\/(\d+)\/edit$/, (m) => productForm(m[1])]
];

async function render() {
  // Признак завершения отрисовки (используется сценариями автоматической проверки)
  delete document.body.dataset.ready;
  try { await draw(); } finally { document.body.dataset.ready = "1"; }
}

async function draw() {
  const [path, qs] = (location.hash.replace(/^#/, "") || "/").split("?");
  const params = new URLSearchParams(qs || "");
  document.querySelectorAll("#nav a").forEach((a) => {
    const href = a.getAttribute("href").slice(1);
    a.classList.toggle("on", path === href || (href !== "/" && path.startsWith(href + "/")));
  });
  try {
    if (!categories.length) categories = await api("/api/categories");
    if (pages[path]) return await pages[path](params);
    for (const [rx, fn] of dynamic) {
      const m = path.match(rx);
      if (m) return await fn(m);
    }
    view.innerHTML = `<h1>Страница не найдена</h1><div class="msg bad">Адрес «${esc(path)}» не существует.</div>`;
  } catch (e) {
    view.innerHTML = e.status === 404
      ? `<h1>Запись не найдена</h1><div class="msg bad">${esc(e.message)}</div><p><a href="#/products">К справочнику товаров</a></p>`
      : `<div class="msg bad">${esc(e.message)}</div>`;
  }
}

window.addEventListener("hashchange", render);
render();
