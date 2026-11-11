# -*- coding: utf-8 -*-
"""
Автоматический прогон ручных проверок через браузер и снятие скриншотов.
Запускается на сервере GitHub (workflow screenshots.yml) при работающем приложении.

  python tools/screenshots.py before   — основной сценарий до перезапуска
  python tools/screenshots.py after    — проверка после перезапуска приложения
  python tools/screenshots.py tests test-output.txt — картинка с выводом dotnet test

Фактические результаты (что реально появилось на странице) пишутся в
docs/screenshots/results.json — их не придумывают, а читают со страницы.
"""
import html
import json
import sys
from datetime import date, timedelta
from pathlib import Path

from playwright.sync_api import sync_playwright

BASE = "http://localhost:5080"
OUT = Path("docs/screenshots")
OUT.mkdir(parents=True, exist_ok=True)
RESULTS = OUT / "results.json"
TODAY = date.today().isoformat()
TOMORROW = (date.today() + timedelta(days=1)).isoformat()


def load_results():
    return json.loads(RESULTS.read_text(encoding="utf-8")) if RESULTS.exists() else {}


def save_results(results):
    RESULTS.write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")


def alerts(page):
    """Тексты всех сообщений на странице (успех, предупреждение, ошибка)."""
    texts = page.locator(".alert").all_inner_texts()
    return " | ".join(t.strip().replace("\n", " ") for t in texts if t.strip())


def shot(page, name):
    page.screenshot(path=str(OUT / f"{name}.png"), full_page=True)


def before(page, r):
    # 1. Главная
    page.goto(BASE)
    shot(page, "01_home")

    # 2. Справочник с остатками и выделением
    page.goto(BASE + "/Products")
    rows = page.locator("tbody tr").count()
    restock = page.locator("tbody tr.table-danger").count()
    r["products_list"] = f"Отображено товаров: {rows}, выделено «Требует пополнения»: {restock}"
    shot(page, "02_products")

    # 3. Поиск
    page.goto(BASE + "/Products?Search=ручка")
    names = page.locator("tbody tr td:nth-child(2)").all_inner_texts()
    r["search"] = "Найдено: " + "; ".join(names)
    shot(page, "03_search")

    # 4. Ниже минимума
    page.goto(BASE + "/Products?BelowMinOnly=true")
    names = page.locator("tbody tr td:nth-child(2)").all_inner_texts()
    r["below_min"] = "Показаны: " + "; ".join(names)
    shot(page, "04_below_min")

    # 5. Карточка товара KT-001 (остаток и история)
    page.goto(BASE + "/Products/Details/1")
    balance = page.locator(".display-6").inner_text().strip()
    r["card_balance"] = f"Остаток в карточке KT-001: {balance}; строк истории: {page.locator('tbody tr').count()}"
    shot(page, "05_product_card")

    # 6. Поступление: форма и результат
    page.goto(BASE + "/Operations/Receipt?productId=1")
    page.fill("#Quantity", "50")
    page.fill("#Supplier", "ООО «КанцОптТорг»")
    page.fill("#DocumentNumber", "ТН-1100")
    shot(page, "06_receipt_form")
    page.click("button[type=submit]")
    balance = page.locator(".display-6").inner_text().strip()
    r["receipt_ok"] = f"{alerts(page)}; остаток KT-001 стал: {balance}"
    shot(page, "07_receipt_done")

    # 7. Поступление без поставщика и документа — отклоняется
    page.goto(BASE + "/Operations/Receipt?productId=2")
    page.fill("#Quantity", "5")
    page.click("button[type=submit]")
    r["receipt_no_supplier"] = alerts(page)
    shot(page, "08_receipt_rejected")

    # 8. Выдача сверх остатка — главное правило (KT-002, остаток 38)
    page.goto(BASE + "/Operations/Issue?productId=2")
    page.select_option("#EmployeeId", "1")
    page.fill("#Quantity", "100")
    page.click("button[type=submit]")
    r["issue_too_much"] = alerts(page)
    shot(page, "09_issue_rejected")

    # 9. Выдача без получателя
    page.goto(BASE + "/Operations/Issue?productId=2")
    page.fill("#Quantity", "1")
    page.click("button[type=submit]")
    r["issue_no_employee"] = alerts(page)
    shot(page, "10_issue_no_employee")

    # 10. Выдача с датой в будущем
    page.goto(BASE + "/Operations/Issue?productId=2")
    page.select_option("#EmployeeId", "1")
    page.fill("#Quantity", "1")
    page.evaluate("document.querySelector('#Date').removeAttribute('max')")
    page.fill("#Date", TOMORROW)
    page.click("button[type=submit]")
    r["issue_future"] = alerts(page)

    # 11. Количество 0
    page.goto(BASE + "/Operations/Issue?productId=2")
    page.select_option("#EmployeeId", "1")
    page.fill("#Quantity", "0")
    page.click("button[type=submit]")
    still_on_form = "/Operations/Issue" in page.url and page.locator(".display-6").count() == 0
    r["issue_zero"] = ("Браузер не отправил форму (поле «Количество» допускает минимум 1)" if still_on_form and not alerts(page)
                       else alerts(page))
    resp = page.request.post(BASE + "/api/operations/issue",
                             data={"productId": 2, "quantity": 0, "date": TODAY, "employeeId": 1})
    r["issue_zero_api"] = f"HTTP {resp.status}: {resp.text()[:200]}"

    # 12. Выдача ровно до минимума — успех с предупреждением (KT-005: остаток 16, минимум 3)
    page.goto(BASE + "/Operations/Issue?productId=5")
    page.select_option("#EmployeeId", "5")
    page.fill("#Quantity", "13")
    page.fill("#Comment", "Для архива документов")
    page.click("button[type=submit]")
    balance = page.locator(".display-6").inner_text().strip()
    r["issue_warning"] = f"{alerts(page)}; остаток KT-005: {balance}"
    shot(page, "11_issue_warning")

    # 13. Выдача ровно всего остатка — допустимо (KT-006: остаток 4)
    page.goto(BASE + "/Operations/Issue?productId=6")
    page.select_option("#EmployeeId", "8")
    page.fill("#Quantity", "4")
    page.click("button[type=submit]")
    balance = page.locator(".display-6").inner_text().strip()
    r["issue_all"] = f"{alerts(page)}; остаток KT-006: {balance}"

    # 14. Архивный товар не предлагается в форме выдачи и операции по нему запрещены
    page.goto(BASE + "/Operations/Issue")
    options = page.locator("#ProductId option").all_inner_texts()
    r["archived_hidden"] = "KT-011 в списке формы выдачи: " + ("есть" if any("KT-011" in o for o in options) else "нет")
    resp = page.request.post(BASE + "/api/operations/issue",
                             data={"productId": 11, "quantity": 1, "date": TODAY, "employeeId": 1})
    r["archived_api"] = f"HTTP {resp.status}: {resp.json().get('error')}"

    # 15. История с фильтром: выдачи по KT-001
    page.goto(BASE + "/Operations?ProductId=1&Type=Issue")
    r["history_filter"] = page.locator("p.text-muted").first.inner_text().strip()
    shot(page, "12_history_filter")

    # 16. История за период
    page.goto(BASE + "/Operations?From=2026-10-01&To=2026-10-15")
    r["history_period"] = page.locator("p.text-muted").first.inner_text().strip()

    # 17. Итоги за октябрь
    page.goto(BASE + "/Operations/Totals?from=2026-10-01&to=2026-10-31")
    total = page.locator("tfoot tr").inner_text().replace("\t", " ").strip()
    r["totals"] = total
    shot(page, "13_totals")

    # 18. Итоги: начало позже конца
    page.goto(BASE + "/Operations/Totals?from=2026-10-31&to=2026-10-01")
    r["totals_bad_period"] = alerts(page)

    # 19. Добавление товара, затем повтор артикула
    page.goto(BASE + "/Products/Create")
    page.fill("#Article", "KT-013")
    page.fill("#Name", "Скотч прозрачный 19 мм")
    page.select_option("#Unit", "шт.")
    page.fill("#MinQuantity", "4")
    page.click("button[type=submit]")
    r["create_ok"] = alerts(page)
    page.goto(BASE + "/Products/Create")
    page.fill("#Article", "kt-001")
    page.fill("#Name", "Ручка-дубль")
    page.click("button[type=submit]")
    r["create_duplicate"] = alerts(page)
    shot(page, "14_create_duplicate")

    # 20. Пустое наименование
    page.goto(BASE + "/Products/Create")
    page.fill("#Article", "KT-014")
    page.fill("#Name", "")
    page.click("button[type=submit]")
    r["create_empty_name"] = page.locator(".text-danger, .alert-danger").all_inner_texts()
    r["create_empty_name"] = " | ".join(t.strip() for t in r["create_empty_name"] if t.strip())

    # 21. Изменение товара: новый минимум у KT-013
    page.goto(BASE + "/Products?Search=KT-013")
    page.click("text=Изменить")
    page.fill("#MinQuantity", "6")
    page.click("button[type=submit]")
    r["edit_ok"] = alerts(page) + "; мин. остаток: " + page.locator("dd").nth(2).inner_text().strip()

    # 22. Архивирование товара
    page.goto(BASE + "/Products?Search=KT-013")
    page.click("text=Карточка")
    page.click("text=В архив")
    r["archive_ok"] = alerts(page)

    # 23. Неизвестный Id
    resp = page.goto(BASE + "/Products/Details/999")
    r["not_found_page"] = f"HTTP {resp.status}: " + page.locator("h4").inner_text().strip()
    shot(page, "15_not_found")
    resp = page.request.get(BASE + "/api/products/999")
    r["not_found_api"] = f"HTTP {resp.status}: {resp.json().get('error')}"

    # 24. API справочника (JSON)
    page.goto(BASE + "/api/products/1")
    r["api_product"] = page.locator("body").inner_text().strip()[:200]
    shot(page, "16_api_product")

    # 25. Сотрудники
    page.goto(BASE + "/Employees/Create")
    page.fill("#FullName", "Фёдорова Наталья Юрьевна")
    page.fill("#Department", "Бухгалтерия")
    page.click("button[type=submit]")
    r["employee_create"] = alerts(page)
    shot(page, "17_employees")


def after(page, r):
    # После перезапуска приложения данные, введённые до него, на месте
    page.goto(BASE + "/Products?Search=KT-013&ShowArchived=true")
    found = page.locator("tbody tr td:nth-child(2)").all_inner_texts()
    page.goto(BASE + "/Products/Details/1")
    balance = page.locator(".display-6").inner_text().strip()
    r["after_restart"] = (f"KT-013 найден: {'да' if found else 'нет'} ({'; '.join(found)}); "
                          f"остаток KT-001 после перезапуска: {balance}")
    page.goto(BASE + "/Products?Search=KT-013&ShowArchived=true")
    shot(page, "18_after_restart")


def tests_image(page, text_file):
    text = Path(text_file).read_text(encoding="utf-8", errors="replace")
    lines = [l for l in text.splitlines() if l.strip()]
    lines = lines[-70:]
    body = html.escape("\n".join(lines))
    page.set_content(
        "<html><body style='margin:0;background:#0c0c0c'>"
        "<pre style='font:13px Consolas,monospace;color:#d0d0d0;padding:16px;white-space:pre-wrap'>"
        f"PS&gt; dotnet test\n{body}</pre></body></html>")
    shot(page, "19_dotnet_test")


def main():
    mode = sys.argv[1]
    r = load_results()
    with sync_playwright() as p:
        # Используем Google Chrome, который уже установлен на сервере GitHub
        browser = p.chromium.launch(channel="chrome", args=["--lang=ru-RU"])
        page = browser.new_page(viewport={"width": 1280, "height": 800}, locale="ru-RU")
        if mode == "before":
            before(page, r)
        elif mode == "after":
            after(page, r)
        elif mode == "tests":
            tests_image(page, sys.argv[2])
        browser.close()
    save_results(r)


if __name__ == "__main__":
    main()
