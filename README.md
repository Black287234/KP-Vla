# StationeryAccounting — модуль учёта движения канцелярских товаров

Курсовой проект, тема №19: «Проектирование и разработка модуля учета движения канцелярских товаров».
ЧПОУ «Сургутский колледж предпринимательства», 2026. Версия **v1.0**.

Веб-приложение ASP.NET Core MVC для кладовщика: справочник товаров, поступление и выдача,
автоматический расчёт остатка (Σ поступлений − Σ выдач), история движения с фильтрами,
контроль минимального количества, итоги за период.

## Что нужно установить

| Программа | Версия | Проверка |
|---|---|---|
| .NET SDK | 10.0 | `dotnet --version` → `10.x.x` |
| Git (по желанию) | любая | `git --version` |
| Браузер | любой современный | — |

Интернет нужен при первой сборке (пакеты NuGet) и для оформления страниц (Bootstrap подключается с CDN).
Отдельно устанавливать SQLite не нужно.

## Получение и запуск

```powershell
git clone https://github.com/Black287234/KP-Vla.git
cd KP-Vla
dotnet run --project StationeryAccounting
```

Открыть **http://localhost:5080**. Остановить — `Ctrl+C`.
В Visual Studio 2026: открыть `StationeryAccounting.sln`, нажать F5.

## База данных и учебные данные

- Используется SQLite через EF Core 10. При запуске приложение вызывает `Database.Migrate()` —
  создаётся файл `StationeryAccounting/stationery.db` и таблицы по миграции `InitialCreate`.
- `DbSeeder` при пустой базе добавляет учебные данные: 12 товаров, 8 сотрудников, 27 операций.
- Данные сохраняются между запусками. **Сброс к учебным данным:** остановить приложение, удалить `stationery.db`, запустить снова.
- Альтернатива: `dotnet tool install --global dotnet-ef`, затем `dotnet ef database update --project StationeryAccounting`.

Пакеты NuGet: `Microsoft.EntityFrameworkCore.Sqlite 10.0.0`, `Microsoft.EntityFrameworkCore.Design 10.0.0`;
тесты — `xunit 2.9.3`, `xunit.runner.visualstudio 3.1.4`, `Microsoft.NET.Test.Sdk 17.14.1`.

## Тесты

```powershell
dotnet test
```
Ожидается: `Passed: 48, Failed: 0`.

## Проверка основного сценария

1. «Товары» — остатки, красным выделены товары «Требует пополнения».
2. «Поступление» — KT-001, 50 шт., поставщик → остаток вырос.
3. «Выдача» — KT-002, 100 шт. → отказ «Недостаточно товара…».
4. «Выдача» — KT-005, 13 уп. → успех с предупреждением о пополнении.
5. «История» — фильтр по товару и типу; «Итоги» — за октябрь 2026.

Ручные проверки API — файл `StationeryAccounting/StationeryAccounting.http`.

## Адреса

| Адрес | Что показывает |
|---|---|
| `/Products` | справочник с поиском и фильтрами |
| `/Products/BelowMin` | товары ниже минимума |
| `/Products/Details/1` | карточка товара с историей |
| `/Operations/Receipt`, `/Operations/Issue` | поступление, выдача |
| `/Operations` | история движения |
| `/Operations/Totals` | итоги за период |
| `/Employees` | сотрудники |
| `/api/products`, `/api/operations` | JSON |

## Структура

```
StationeryAccounting/          веб-приложение
  Models/        сущности: Product, Employee, StockOperation
  Data/          AppDbContext, DbSeeder, Migrations/
  Services/      предметная логика: StockService, ProductService, EmployeeService, StockCalculator
  ViewModels/    формы и фильтры
  Controllers/   страницы и Api/
  Views/         Razor
StationeryAccounting.Tests/    автотесты xUnit
docs/                          пояснительная записка, презентация, таблица проверок, материалы к защите
tools/screenshots.py           автоматический прогон ручных проверок со скриншотами (GitHub Actions)
```

## Архив для сдачи

```powershell
git archive -o stage5.zip HEAD
```
