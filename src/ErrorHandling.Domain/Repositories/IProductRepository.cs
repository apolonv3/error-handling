using ErrorHandling.Domain.Entities;

namespace ErrorHandling.Domain.Repositories;

/// <summary>
/// Repository contract for <see cref="Product"/> aggregate.
/// </summary>
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id);
    Task<Product?> GetByIdOrDefaultAsync(Guid id);
    Task<List<Product>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task SaveAsync(Product product);
    Task SaveAllAsync(IEnumerable<Product> products);
}
