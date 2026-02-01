using ErrorHandling.Domain.Entities;
using ErrorHandling.Domain.Repositories;
using ErrorHandling.Domain.Results;
using ErrorHandling.Domain.ValueObjects;

namespace ErrorHandling.Domain.Services;

/// <summary>
/// Order service that uses the Result pattern and type-safe <see cref="OrderError"/> for failures.
/// </summary>
/// <remarks>
/// All operations return <see cref="Result{T}"/> or <see cref="Result"/> instead of throwing.
/// Failures use sealed <see cref="OrderError"/> subtypes (e.g. <see cref="OrderValidationError"/>,
/// <see cref="EntityNotFoundError"/>) so the API can pattern-match and map to HTTP status and
/// Problem Details without string-based error codes. Supports railway-oriented style via
/// <see cref="ResultExtensions.BindAsync"/> (e.g. ProcessOrderWorkflowAsync).
/// </remarks>
public class ResultOrderService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;

    public ResultOrderService(
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IOrderRepository orderRepository
    )
    {
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _orderRepository = orderRepository;
    }

    public async Task<Result<Order>> CreateOrderAsync(Guid customerId, string shippingAddress)
    {
        if (customerId == Guid.Empty)
            return Result<Order>.Failure(new OrderValidationError("customerId", "Customer ID is required"));

        if (string.IsNullOrWhiteSpace(shippingAddress))
            return Result<Order>.Failure(
                new OrderValidationError("shippingAddress", "Shipping address is required")
            );

        var customer = await _customerRepository.GetByIdOrDefaultAsync(customerId);
        if (customer == null)
            return Result<Order>.Failure(new EntityNotFoundError(nameof(Customer), customerId));

        if (customer.Status != CustomerStatus.Active)
            return Result<Order>.Failure(new CustomerNotActiveError(customerId, customer.Status));

        var orderResult = Order.Create(customerId, shippingAddress);
        if (orderResult.IsFailure)
            return orderResult;

        await _orderRepository.SaveAsync(orderResult.Value);

        return orderResult;
    }

    public async Task<Result<Order>> AddItemToOrderAsync(Guid orderId, Guid productId, int quantity)
    {
        if (orderId == Guid.Empty)
            return Result<Order>.Failure(new OrderValidationError("orderId", "Order ID is required"));

        if (productId == Guid.Empty)
            return Result<Order>.Failure(new OrderValidationError("productId", "Product ID is required"));

        if (quantity <= 0)
            return Result<Order>.Failure(
                new OrderValidationError("quantity", "Quantity must be greater than zero")
            );

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            return Result<Order>.Failure(new EntityNotFoundError(nameof(Order), orderId));

        var product = await _productRepository.GetByIdOrDefaultAsync(productId);
        if (product == null)
            return Result<Order>.Failure(new EntityNotFoundError(nameof(Product), productId));

        if (!product.IsActive)
            return Result<Order>.Failure(new ProductInactiveError(productId, product.Name));

        var reserveResult = product.TryReserveStock(quantity);
        if (reserveResult.IsFailure)
            return Result<Order>.Failure(reserveResult.Error!);

        var addItemResult = order.AddItemSafe(product, quantity);
        if (addItemResult.IsFailure)
            return Result<Order>.Failure(addItemResult.Error!);

        await _productRepository.SaveAsync(product);
        await _orderRepository.SaveAsync(order);

        return Result<Order>.Success(order);
    }

    public async Task<Result<Order>> SubmitOrderAsync(Guid orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            return Result<Order>.Failure(new EntityNotFoundError(nameof(Order), orderId));

        var customer = await _customerRepository.GetByIdOrDefaultAsync(order.CustomerId);
        if (customer == null)
            return Result<Order>.Failure(new EntityNotFoundError(nameof(Customer), order.CustomerId));

        var creditResult = customer.TryUseCredit(order.TotalAmount);
        if (creditResult.IsFailure)
            return Result<Order>.Failure(creditResult.Error!);

        var submitResult = order.SubmitSafe();
        if (submitResult.IsFailure)
            return Result<Order>.Failure(submitResult.Error!);

        await _customerRepository.SaveAsync(customer);
        await _orderRepository.SaveAsync(order);

        return Result<Order>.Success(order);
    }

    public async Task<Result<Order>> ProcessPaymentAsync(Guid orderId, Money? paymentAmount)
    {
        if (paymentAmount is null)
            return Result<Order>.Failure(new NullValueError("paymentAmount", "Payment amount cannot be null"));

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            return Result<Order>.Failure(new EntityNotFoundError(nameof(Order), orderId));

        if (paymentAmount < order.TotalAmount)
            return Result<Order>.Failure(new InsufficientPaymentError(paymentAmount, order.TotalAmount));

        var approveResult = order.ApproveSafe();
        if (approveResult.IsFailure)
            return Result<Order>.Failure(approveResult.Error!);

        await _orderRepository.SaveAsync(order);

        return Result<Order>.Success(order);
    }

    public async Task<Result<Order>> ShipOrderAsync(Guid orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            return Result<Order>.Failure(new EntityNotFoundError(nameof(Order), orderId));

        var shipResult = order.ShipSafe();
        if (shipResult.IsFailure)
            return Result<Order>.Failure(shipResult.Error!);

        await _orderRepository.SaveAsync(order);

        return Result<Order>.Success(order);
    }

    public async Task<Result> CancelOrderAsync(Guid orderId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(new OrderValidationError("reason", "Cancellation reason is required"));

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            return Result.Failure(new EntityNotFoundError(nameof(Order), orderId));

        var customer = await _customerRepository.GetByIdOrDefaultAsync(order.CustomerId);
        if (customer == null)
            return Result.Failure(new EntityNotFoundError(nameof(Customer), order.CustomerId));

        if (order.Status == OrderStatus.Approved || order.Status == OrderStatus.Submitted)
        {
            var restoreResult = customer.RestoreCreditSafe(order.TotalAmount);
            if (restoreResult.IsFailure)
                return restoreResult;
        }

        // Batch fetch all products
        var productIds = order.Items.Select(i => i.ProductId).ToList();
        var products = await _productRepository.GetByIdsAsync(productIds);
        
        // Create a dictionary for quick lookup
        var productDict = products.ToDictionary(p => p.Id);
        var productsToUpdate = new List<Product>();

        foreach (var item in order.Items)
        {
            if (productDict.TryGetValue(item.ProductId, out var product))
            {
                var restockResult = product.RestockProductSafe(item.Quantity);
                if (restockResult.IsFailure)
                    return restockResult;
                productsToUpdate.Add(product);
            }
        }

        // Batch save all products
        if (productsToUpdate.Any())
        {
            await _productRepository.SaveAllAsync(productsToUpdate);
        }

        var cancelResult = order.CancelSafe(reason);
        if (cancelResult.IsFailure)
            return cancelResult;

        await _customerRepository.SaveAsync(customer);
        await _orderRepository.SaveAsync(order);

        return Result.Success();
    }

    // Example of railway-oriented programming with Result
    public async Task<Result<Order>> ProcessOrderWorkflowAsync(
        Guid customerId,
        string shippingAddress,
        (Guid productId, int quantity)[] items,
        Money paymentAmount
    )
    {
        return await CreateOrderAsync(customerId, shippingAddress)
            .BindAsync(async order =>
            {
                // Add all items
                foreach (var (productId, quantity) in items)
                {
                    var addResult = await AddItemToOrderAsync(order.Id, productId, quantity);
                    if (addResult.IsFailure)
                        return addResult;
                }
                return Result<Order>.Success(order);
            })
            .BindAsync(order => SubmitOrderAsync(order.Id))
            .BindAsync(order => ProcessPaymentAsync(order.Id, paymentAmount))
            .BindAsync(order => ShipOrderAsync(order.Id));
    }
}
