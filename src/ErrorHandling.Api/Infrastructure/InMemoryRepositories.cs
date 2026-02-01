using ErrorHandling.Domain.Entities;
using ErrorHandling.Domain.Repositories;
using ErrorHandling.Domain.ValueObjects;

namespace ErrorHandling.Api.Infrastructure;

/// <summary>
/// In-memory implementation of <see cref="ICustomerRepository"/>.
/// </summary>
/// <remarks>
/// Stores customers in a dictionary. Seeded with one demo customer. Intended for
/// demos and tests; replace with a real persistence implementation (e.g. EF Core)
/// for production.
/// </remarks>
public class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly Dictionary<Guid, Customer> _customers = new();

    public InMemoryCustomerRepository()
    {
        // Seed with demo data
        var customerId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var email = Email.Create("john@example.com");
        var creditLimit = Money.Create(10000m, "USD");
        var customer = new Customer("John Doe", email, creditLimit);
        _customers[customerId] = customer;
    }

    public Task<Customer?> GetByIdAsync(Guid id)
    {
        if (_customers.TryGetValue(id, out var customer))
            return Task.FromResult<Customer?>(customer);
        return Task.FromResult<Customer?>(null);
    }

    public Task<Customer?> GetByIdOrDefaultAsync(Guid id)
    {
        return GetByIdAsync(id);
    }

    public Task SaveAsync(Customer customer)
    {
        _customers[customer.Id] = customer;
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory implementation of <see cref="IProductRepository"/>.
/// </summary>
/// <remarks>
/// Stores products in a dictionary. Seeded with two demo products (Laptop, Mouse).
/// Used for demos and tests; use a real data store in production.
/// </remarks>
public class InMemoryProductRepository : IProductRepository
{
    private readonly Dictionary<Guid, Product> _products = new();

    public InMemoryProductRepository()
    {
        // Seed with demo products
        var productId1 = Guid.Parse("1fa85f64-5717-4562-b3fc-2c963f66afa6");
        var price1 = Money.Create(99.99m, "USD");
        var product1 = new Product("Laptop", "High-performance laptop", price1, 50, "SKU001");
        _products[productId1] = product1;

        var productId2 = Guid.Parse("2fa85f64-5717-4562-b3fc-2c963f66afa6");
        var price2 = Money.Create(29.99m, "USD");
        var product2 = new Product("Mouse", "Wireless mouse", price2, 100, "SKU002");
        _products[productId2] = product2;
    }

    public Task<Product?> GetByIdAsync(Guid id)
    {
        if (_products.TryGetValue(id, out var product))
            return Task.FromResult<Product?>(product);
        return Task.FromResult<Product?>(null);
    }

    public Task<Product?> GetByIdOrDefaultAsync(Guid id)
    {
        return GetByIdAsync(id);
    }

    public Task<List<Product>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var products = ids
            .Select(id => _products.TryGetValue(id, out var product) ? product : null)
            .Where(p => p != null)
            .Cast<Product>()
            .ToList();
        return Task.FromResult(products);
    }

    public Task SaveAsync(Product product)
    {
        _products[product.Id] = product;
        return Task.CompletedTask;
    }

    public Task SaveAllAsync(IEnumerable<Product> products)
    {
        foreach (var product in products)
        {
            _products[product.Id] = product;
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory implementation of <see cref="IOrderRepository"/>.
/// </summary>
/// <remarks>
/// Stores orders in a dictionary. No seed data; orders are added when created via the API.
/// Suitable for demos and tests; replace with persistent storage for production.
/// </remarks>
public class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _orders = new();

    public Task<Order?> GetByIdAsync(Guid id)
    {
        if (_orders.TryGetValue(id, out var order))
            return Task.FromResult<Order?>(order);
        return Task.FromResult<Order?>(null);
    }

    public Task SaveAsync(Order order)
    {
        _orders[order.Id] = order;
        return Task.CompletedTask;
    }
}
