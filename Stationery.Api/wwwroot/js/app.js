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
  const p = await api(`/api/products/${id}`);
  view.innerHTML = `
    ${takeFlash()}
    <div class="head"><div><h1>${esc(p.name)}</h1><p class="lead">Артикул ${esc(p.article)} · ${esc(p.categoryName)}</p></div>
      <a class="btn ghost" href="#/products/${p.id}/edit">Изменить</a></div>
    <div class="box"><dl class="facts">
      <dt>Единица измерения</dt><dd>${esc(p.unit)}</dd>
      <dt>Минимальный остаток</dt><dd>${p.minQuantity}</dd>
      <dt>Состояние</dt><dd>${p.isArchived ? '<span class="tag">выведен из ассортимента</span>' : "в ассортименте"}</dd>
    </dl></div>
    <div class="box"><div class="form-msg" id="m"></div>
      <button class="btn ${p.isArchived ? "" : "danger"}" id="arch">${p.isArchived ? "Вернуть в ассортимент" : "Вывести из ассортимента"}</button></div>
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

// ---------------------------------------------------------------- маршрутизация
const dynamic = [
  [/^\/products\/(\d+)$/, (m) => productCard(m[1])],
  [/^\/products\/(\d+)\/edit$/, (m) => productForm(m[1])]
];

async function render() {
  const [path, qs] = (location.hash.replace(/^#/, "") || "/products").split("?");
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
