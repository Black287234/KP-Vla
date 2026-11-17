// Одностраничное приложение без сторонних библиотек: маршрутизация по адресу после «#», данные — через fetch.
"use strict";

const view = document.getElementById("view");

function esc(s) {
  return String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

async function getJson(url) {
  const r = await fetch(url);
  const body = await r.json();
  if (!r.ok) throw new Error(body.message || `Ошибка ${r.status}`);
  return body;
}

const routes = {
  "/products": async () => {
    const items = await getJson("/api/products");
    view.innerHTML = `
      <h1>Справочник товаров</h1>
      <p class="lead">Позиций в справочнике: ${items.length}</p>
      <table class="list">
        <thead><tr><th>Артикул</th><th>Наименование</th><th>Группа</th><th>Ед. изм.</th><th class="n">Мин. остаток</th></tr></thead>
        <tbody>${items.map((p) => `<tr><td>${esc(p.article)}</td><td>${esc(p.name)}</td><td>${esc(p.category)}</td>
          <td>${esc(p.unit)}</td><td class="n">${p.minQuantity}</td></tr>`).join("")}</tbody>
      </table>`;
  }
};

async function render() {
  const path = location.hash.replace(/^#/, "") || "/products";
  document.querySelectorAll("#nav a").forEach((a) => a.classList.toggle("on", a.getAttribute("href") === "#" + path));
  const page = routes[path];
  try {
    if (page) await page();
    else view.innerHTML = `<h1>Страница не найдена</h1><div class="msg bad">Адрес «${esc(path)}» не существует.</div>`;
  } catch (e) {
    view.innerHTML = `<div class="msg bad">${esc(e.message)}</div>`;
  }
}

window.addEventListener("hashchange", render);
render();
