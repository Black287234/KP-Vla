"""График: остаток товара во времени и выдача задним числом."""
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

plt.rcParams["font.family"] = "Liberation Serif"
days = [-10, -4, -1, 0]
fig, ax = plt.subplots(figsize=(9, 3.6), dpi=200)
ax.step([-12, -10, -4, -1, 0.5], [0, 20, 5, 25, 25], where="post", color="#2b2d31", lw=2, label="остаток до новой выдачи")
ax.step([-12, -10, -6, -4, -1, 0.5], [0, 20, 10, -5, 15, 15], where="post", color="#b3261e", lw=1.5, ls="--",
        label="если выдать 10 ед. датой «−6 дней»")
ax.axhline(0, color="#9a9ea6", lw=0.8)
ax.annotate("поступление 20", (-10, 20), (-11.6, 23), fontsize=9, arrowprops=dict(arrowstyle="->", lw=0.6))
ax.annotate("выдача 15", (-4, 5), (-5.6, 1.5), fontsize=9, arrowprops=dict(arrowstyle="->", lw=0.6))
ax.annotate("поступление 20", (-1, 25), (-3.3, 28), fontsize=9, arrowprops=dict(arrowstyle="->", lw=0.6))
ax.annotate("остаток −5", (-4, -5), (-2.8, -8.5), fontsize=9, color="#b3261e", arrowprops=dict(arrowstyle="->", lw=0.6, color="#b3261e"))
ax.set_xticks([-10, -6, -4, -1, 0]); ax.set_xticklabels(["−10 дн.", "−6 дн.", "−4 дн.", "−1 дн.", "сегодня"])
ax.set_ylabel("остаток, ед.")
ax.set_ylim(-11, 31)
for s in ("top", "right"):
    ax.spines[s].set_visible(False)
ax.legend(loc="upper left", fontsize=9, frameon=False)
plt.tight_layout()
plt.savefig("balance.png", bbox_inches="tight")
