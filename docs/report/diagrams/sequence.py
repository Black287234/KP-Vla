"""Диаграмма последовательности оформления выдачи (рисуется matplotlib)."""
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

plt.rcParams["font.family"] = "Liberation Serif"
lanes = ["Пользователь\n(app.js)", "StockEndpoints\n+ Validate<IssueForm>", "StockService", "IMovementRepository", "SQLite"]
X = [1, 3.2, 5.4, 7.6, 9.6]
msgs = [
    (0, 1, "POST /api/movements/issue {товар, кол-во, дата, …}", False),
    (1, 1, "проверка атрибутов: обязательные поля, 1…100 000", False),
    (1, 2, "Issue(form)", False),
    (2, 2, "товар в ассортименте, подразделение есть, дата ≤ сегодня и ≥ сегодня − 30", False),
    (2, 3, "BeginTransaction()", False),
    (3, 4, "BEGIN IMMEDIATE", False),
    (2, 3, "List(ProductId) — история товара", False),
    (3, 4, "SELECT … ORDER BY Date, Id", False),
    (2, 2, "AvailableForIssue(история, дата): остаток на дату и после неё", False),
    (2, 1, "[кол-во > доступного] AppException 409 «Недостаточно товара …»", True),
    (2, 3, "Add(movement); Save()", False),
    (3, 4, "INSERT INTO Movements; COMMIT", False),
    (1, 0, "201 Created {операция, остаток после}", True),
]
fig, ax = plt.subplots(figsize=(11, 7.2), dpi=200)
top, step = 0.5, 0.5
bottom = top + step * (len(msgs) + 1)
for x, name in zip(X, lanes):
    ax.text(x, top - 0.15, name, ha="center", va="bottom", fontsize=10,
            bbox=dict(boxstyle="round,pad=0.35", fc="#fbf1df", ec="#c27c0e"))
    ax.plot([x, x], [top, bottom], color="#9a9ea6", lw=0.8, ls="--")
for i, (a, b, text, ret) in enumerate(msgs):
    y = top + step * (i + 1)
    if a == b:
        ax.annotate("", xy=(X[a] + 0.05, y + 0.18), xytext=(X[a] + 0.05, y),
                    arrowprops=dict(arrowstyle="->", connectionstyle="arc3,rad=-1.2", color="#2b2d31", lw=0.9))
        ax.text(X[a] + 0.35, y + 0.08, text, fontsize=8.5, va="center")
    else:
        ax.annotate("", xy=(X[b], y), xytext=(X[a], y),
                    arrowprops=dict(arrowstyle="->", color="#b3261e" if "409" in text else "#2b2d31", lw=0.9, ls="--" if ret else "-"))
        ax.text((X[a] + X[b]) / 2, y - 0.07, text, fontsize=8.5, ha="center", va="bottom",
                color="#b3261e" if "409" in text else "#23262b")
ax.set_xlim(0.2, 10.6)
ax.set_ylim(bottom + 0.2, top - 0.6)
ax.axis("off")
plt.tight_layout()
plt.savefig("sequence.png", bbox_inches="tight")
