using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Stationery.Api.Data;
using Stationery.Api.Domain;
using Stationery.Api.Infrastructure;

namespace Stationery.Api.Services;

/// <summary>Данные формы товара. Атрибуты задают проверки, выполняемые фильтром Validate.</summary>
public record ProductForm(
    [property: Required(ErrorMessage = "Укажите артикул")]
    [property: RegularExpression(@"^\s*[А-Яа-яA-Za-z]{2}-\d{4}\s*$", ErrorMessage = "Артикул указывается в виде КТ-0001: две буквы, дефис, четыре цифры")]
    string? Article,
    [property: Required(ErrorMessage = "Укажите наименование")]
    [property: StringLength(120, MinimumLength = 3, ErrorMessage = "Наименование — от 3 до 120 символов")]
    string? Name,
    [property: Required(ErrorMessage = "Выберите группу товаров")]
    ProductCategory? Category,
    [property: Required(ErrorMessage = "Укажите единицу измерения")]
    [property: StringLength(15, ErrorMessage = "Единица измерения — не более 15 символов")]
    string? Unit,
    [property: Range(0, 10000, ErrorMessage = "Минимальный остаток — от 0 до 10 000")]
    int MinQuantity);

public record DepartmentForm(
    [property: Required(ErrorMessage = "Укажите название подразделения")]
    [property: StringLength(80, MinimumLength = 3, ErrorMessage = "Название — от 3 до 80 символов")]
    string? Name,
    [property: Required(ErrorMessage = "Укажите руководителя")]
    [property: StringLength(100, MinimumLength = 5, ErrorMessage = "ФИО руководителя — от 5 до 100 символов")]
    string? Head);

/// <summary>Справочники товаров и подразделений.</summary>
public class CatalogService(IProductRepository products, IDepartmentRepository departments, IMovementRepository movements)
{
    public IReadOnlyList<Product> Products(string? search, ProductCategory? category, bool includeArchived)
    {
        IEnumerable<Product> list = products.List(includeArchived);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            list = list.Where(p => p.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                                || p.Article.Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        if (category is { } c) list = list.Where(p => p.Category == c);
        return list.ToList();
    }

    public Product Product(int id) => products.Find(id) ?? throw AppException.NotFound($"Товар с кодом {id} не найден");

    public Product CreateProduct(ProductForm form)
    {
        var product = new Product();
        Apply(product, form);
        products.Add(product);
        products.Save();
        return product;
    }

    public Product UpdateProduct(int id, ProductForm form)
    {
        var product = Product(id);
        // Количество в операциях указано в прежней единице: её замена исказила бы историю и остаток
        var unit = form.Unit!.Trim();
        if (!string.Equals(unit, product.Unit, StringComparison.OrdinalIgnoreCase) && movements.HasMovements(id))
            throw AppException.Conflict(
                $"Нельзя изменить единицу измерения: по товару уже есть операции, количество в них указано в единицах «{product.Unit}». " +
                "Для новой фасовки заведите отдельный товар");
        Apply(product, form);
        products.Save();
        return product;
    }

    public Product SetArchived(int id, bool archived)
    {
        var product = Product(id);
        if (product.IsArchived == archived)
            throw AppException.Conflict(archived ? "Товар уже выведен из ассортимента" : "Товар уже находится в ассортименте");
        // Выводить из ассортимента можно только товар, которого нет на складе
        var balance = movements.BalanceOf(id);
        if (archived && balance > 0)
            throw AppException.Conflict($"Нельзя вывести из ассортимента товар, который есть на складе: остаток {balance} {product.Unit}");
        product.IsArchived = archived;
        products.Save();
        return product;
    }

    public IReadOnlyList<Department> Departments() => departments.List();

    public Department CreateDepartment(DepartmentForm form)
    {
        var name = Regex.Replace(form.Name!.Trim(), @"\s+", " ");
        if (departments.NameExists(name, null))
            throw AppException.Conflict($"Подразделение «{name}» уже есть в справочнике");
        var d = new Department { Name = name, Head = form.Head!.Trim() };
        departments.Add(d);
        departments.Save();
        return d;
    }

    private void Apply(Product product, ProductForm form)
    {
        var article = form.Article!.Trim().ToUpperInvariant();
        if (products.ArticleExists(article, product.Id == 0 ? null : product.Id))
            throw AppException.Conflict($"Товар с артикулом {article} уже есть в справочнике");
        product.Article = article;
        product.Name = Regex.Replace(form.Name!.Trim(), @"\s+", " ");
        product.Category = form.Category!.Value;
        product.Unit = form.Unit!.Trim();
        product.MinQuantity = form.MinQuantity;
    }
}
