using ErrorHandling.Domain.Entities;
using ErrorHandling.Domain.Repositories;
using ErrorHandling.Domain.Results;
using ErrorHandling.Domain.ValueObjects;

namespace ErrorHandling.Domain.Services;

/// <summary>
/// Order service that uses the Result pattern and type-safe <see cref="OrderError"/> for failures.
/// </summary>
/// <remarks>
/// All operations return <see cref="Result{TValue, TError}"/> with <see cref="OrderError"/> so the
/// compiler enforces that only <see cref="OrderError"/> can appear. Supports railway-oriented style
/// via <see cref="ResultExtensions.BindAsync"/> for <see cref="Result{TValue, TError}"/> (e.g. ProcessOrderWorkflowAsync).
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

    public async Task<Result<Order, OrderError>> CreateOrderAsync(Guid customerId, string shippingAddress)
    {
        if (customerId == Guid.Empty)
            return Result<Order, OrderError>.Failure(new OrderValidationError("customerId", "Customer ID is required"));

        if (string.IsNullOrWhiteSpace(shippingAddress))
            return Result<Order, OrderError>.Failure(
                new OrderValidationError("shippingAddress", "Shipping address is required")
            );

        var customer = await _customerRepository.GetByIdOrDefaultAsync(customerId);
        if (customer == null)
            return Result<Order, OrderError>.Failure(new EntityNotFoundError(nameof(Customer), customerId));

        if (customer.Status != CustomerStatus.Active)
            return Result<Order, OrderError>.Failure(new CustomerNotActiveError(customerId, customer.Status));

        var orderResult = Order.Create(customerId, shippingAddress);
        if (orderResult.IsFailure)
            return Result<Order, OrderError>.Failure((OrderError)orderResult.Error!);

        await _orderRepository.SaveAsync(orderResult.Value);

        return Result<Order, OrderError>.Success(orderResult.Value);
    }

    public async Task<Result<Order, OrderError>> AddItemToOrderAsync(Guid orderId, Guid productId, int quantity)
    {
        if (orderId == Guid.Empty)
            return Result<Order, OrderError>.Failure(new OrderValidationError("orderId", "Order ID is required"));

        if (productId == Guid.Empty)
            return Result<Order, OrderError>.Failure(new OrderValidationError("productId", "Product ID is required"));

        if (quantity <= 0)
            return Result<Order, OrderError>.Failure(
                new OrderValidationError("quantity", "Quantity must be greater than zero")
            );

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            return Result<Order, OrderError>.Failure(new EntityNotFoundError(nameof(Order), orderId));

        var product = await _productRepository.GetByIdOrDefaultAsync(productId);
        if (product == null)
            return Result<Order, OrderError>.Failure(new EntityNotFoundError(nameof(Product), productId));

        if (!product.IsActive)
            return Result<Order, OrderError>.Failure(new ProductInactiveError(productId, product.Name));

        var reserveResult = product.TryReserveStock(quantity);
        if (reserveResult.IsFailure)
            return Result<Order, OrderError>.Failure((OrderError)reserveResult.Error!);

        var addItemResult = order.AddItemSafe(product, quantity);
        if (addItemResult.IsFailure)
            return Result<Order, OrderError>.Failure((OrderError)addItemResult.Error!);

        await _productRepository.SaveAsync(product);
        await _orderRepository.SaveAsync(order);

        return Result<Order, OrderError>.Success(order);
    }

    public async Task<Result<Order, OrderError>> SubmitOrderAsync(Guid orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            return Result<Order, OrderError>.Failure(new EntityNotFoundError(nameof(Order), orderId));

        var customer = await _customerRepository.GetByIdOrDefaultAsync(order.CustomerId);
        if (customer == null)
            return Result<Order, OrderError>.Failure(new EntityNotFoundError(nameof(Customer), order.CustomerId));

        var creditResult = customer.TryUseCredit(order.TotalAmount);
        if (creditResult.IsFailure)
            return Result<Order, OrderError>.Failure((OrderError)creditResult.Error!);

        var submitResult = order.SubmitSafe();
        if (submitResult.IsFailure)
            return Result<Order, OrderError>.Failure((OrderError)submitResult.Error!);

        await _customerRepository.SaveAsync(customer);
        await _orderRepository.SaveAsync(order);

        return Result<Order, OrderError>.Success(order);
    }

    public async Task<Result<Order, OrderError>> ProcessPaymentAsync(Guid orderId, Money? paymentAmount)
    {
        if (paymentAmount is null)
            return Result<Order, OrderError>.Failure(new NullValueError("paymentAmount", "Payment amount cannot be null"));

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            return Result<Order, OrderError>.Failure(new EntityNotFoundError(nameof(Order), orderId));

        if (paymentAmount < order.TotalAmount)
            return Result<Order, OrderError>.Failure(new InsufficientPaymentError(paymentAmount, order.TotalAmount));

        var approveResult = order.ApproveSafe();
        if (approveResult.IsFailure)
            return Result<Order, OrderError>.Failure((OrderError)approveResult.Error!);

        await _orderRepository.SaveAsync(order);

        return Result<Order, OrderError>.Success(order);
    }

    public async Task<Result<Order, OrderError>> ShipOrderAsync(Guid orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            return Result<Order, OrderError>.Failure(new EntityNotFoundError(nameof(Order), orderId));

        var shipResult = order.ShipSafe();
        if (shipResult.IsFailure)
            return Result<Order, OrderError>.Failure((OrderError)shipResult.Error!);

        await _orderRepository.SaveAsync(order);

        return Result<Order, OrderError>.Success(order);
    }

    public async Task<Result<OrderError>> CancelOrderAsync(Guid orderId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result<OrderError>.Failure(new OrderValidationError("reason", "Cancellation reason is required"));

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            return Result<OrderError>.Failure(new EntityNotFoundError(nameof(Order), orderId));

        var customer = await _customerRepository.GetByIdOrDefaultAsync(order.CustomerId);
        if (customer == null)
            return Result<OrderError>.Failure(new EntityNotFoundError(nameof(Customer), order.CustomerId));

        if (order.Status == OrderStatus.Approved || order.Status == OrderStatus.Submitted)
        {
            var restoreResult = customer.RestoreCreditSafe(order.TotalAmount);
            if (restoreResult.IsFailure)
                return Result<OrderError>.Failure((OrderError)restoreResult.Error!);
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
                    return Result<OrderError>.Failure((OrderError)restockResult.Error!);
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
            return Result<OrderError>.Failure((OrderError)cancelResult.Error!);

        await _customerRepository.SaveAsync(customer);
        await _orderRepository.SaveAsync(order);

        return Result<OrderError>.Success();
    }

    // Example of railway-oriented programming with Result<Order, OrderError>
    public async Task<Result<Order, OrderError>> ProcessOrderWorkflowAsync(
        Guid customerId,
        string shippingAddress,
        (Guid productId, int quantity)[] items,
        Money paymentAmount
    )
    {
        return await CreateOrderAsync(customerId, shippingAddress)
            .BindAsync(async order =>
            {
                foreach (var (productId, quantity) in items)
                {
                    var addResult = await AddItemToOrderAsync(order.Id, productId, quantity);
                    if (addResult.IsFailure)
                        return addResult;
                }
                return Result<Order, OrderError>.Success(order);
            })
            .BindAsync(order => SubmitOrderAsync(order.Id))
            .BindAsync(order => ProcessPaymentAsync(order.Id, paymentAmount))
            .BindAsync(order => ShipOrderAsync(order.Id));
    }
}
