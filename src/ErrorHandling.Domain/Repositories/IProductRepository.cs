using ErrorHandling.Domain.Entities;

namespace ErrorHandling.Domain.Repositories;

/// <summary>
/// Repository contract for the <see cref="Product"/> aggregate.
/// </summary>
/// <remarks>
/// Abstracts persistence of products. Supports single and batch get by ID, and
/// single or batch save. Used by order services to load products and persist
/// stock changes (e.g. on add item or cancel order).
/// </remarks>
public interface IProductRepository
{
    /// <summary>Gets a product by ID, or null if not found.</summary>
    Task<Product?> GetByIdAsync(Guid id);

    /// <summary>Gets a product by ID, or null if not found. Alias for GetByIdAsync for semantic clarity.</summary>
    Task<Product?> GetByIdOrDefaultAsync(Guid id);

    /// <summary>Gets multiple products by IDs. Missing IDs are skipped; returned list may be smaller than requested.</summary>
    Task<List<Product>> GetByIdsAsync(IEnumerable<Guid> ids);

    /// <summary>Persists a single product (insert or update).</summary>
    Task SaveAsync(Product product);

    /// <summary>Persists multiple products (e.g. after restocking on order cancel).</summary>
    Task SaveAllAsync(IEnumerable<Product> products);
}
