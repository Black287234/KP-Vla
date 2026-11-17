"""Диаграмма последовательности оформления выдачи (рисуется matplotlib)."""
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

plt.rcParams["font.family"] = "Liberation Serif"
lanes = ["Интерфейс\n(браузер)", "API\n(StockEndpoints)", "Сервис\n(StockService)", "База данных\n(SQLite)"]
X = [1, 3.6, 6.2, 8.8]
msgs = [
    (0, 1, "1. Заявка на выдачу: товар, количество, дата", False),
    (1, 2, "2. Данные проверены — Issue()", False),
    (2, 3, "3. Прочитать историю товара", False),
    (2, 2, "4. Хватает ли товара на дату выдачи?", False),
    (2, 3, "5. Да — сохранить операцию", False),
    (1, 0, "6. Ответ: «Выдача оформлена», остаток", True),
]
fig, ax = plt.subplots(figsize=(10, 5.2), dpi=200)
top, step = 0.5, 0.62
bottom = top + step * (len(msgs) + 1)
for x, name in zip(X, lanes):
    ax.text(x, top - 0.15, name, ha="center", va="bottom", fontsize=12,
            bbox=dict(boxstyle="round,pad=0.35", fc="#fbf1df", ec="#c27c0e"))
    ax.plot([x, x], [top, bottom], color="#9a9ea6", lw=0.8, ls="--")
for i, (a, b, text, ret) in enumerate(msgs):
    y = top + step * (i + 1)
    if a == b:
        ax.annotate("", xy=(X[a] + 0.05, y + 0.18), xytext=(X[a] + 0.05, y),
                    arrowprops=dict(arrowstyle="->", connectionstyle="arc3,rad=-1.2", color="#2b2d31", lw=0.9))
        ax.text(X[a] + 0.35, y + 0.08, text, fontsize=11.5, va="center", bbox=dict(fc="white", ec="none", pad=1.5))
    else:
        ax.annotate("", xy=(X[b], y), xytext=(X[a], y),
                    arrowprops=dict(arrowstyle="->", color="#2b2d31", lw=0.9, ls="--" if ret else "-"))
        ax.text((X[a] + X[b]) / 2, y - 0.07, text, fontsize=11.5, ha="center", va="bottom",
                color="#23262b")
ax.set_xlim(0.2, 10.2)
ax.set_ylim(bottom + 0.2, top - 0.6)
ax.axis("off")
plt.tight_layout()
plt.savefig("sequence.png", bbox_inches="tight")
