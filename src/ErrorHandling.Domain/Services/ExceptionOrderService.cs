using ErrorHandling.Domain.Entities;
using ErrorHandling.Domain.Exceptions;
using ErrorHandling.Domain.Repositories;
using ErrorHandling.Domain.ValueObjects;

namespace ErrorHandling.Domain.Services;

/// <summary>
/// Order service using traditional exception-based error handling
/// </summary>
public class ExceptionOrderService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;

    public ExceptionOrderService(
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IOrderRepository orderRepository
    )
    {
        _customerRepository =
            customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _productRepository =
            productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _orderRepository =
            orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
    }

    public async Task<Order> CreateOrderAsync(Guid customerId, string shippingAddress)
    {
        if (customerId == Guid.Empty)
            throw new ValidationException("customerId", "Customer ID is required");

        if (string.IsNullOrWhiteSpace(shippingAddress))
            throw new ValidationException("shippingAddress", "Shipping address is required");

        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer == null)
            throw new EntityNotFoundException(nameof(Customer), customerId);

        if (customer.Status != CustomerStatus.Active)
            throw new BusinessRuleException(
                "INACTIVE_CUSTOMER",
                $"Customer {customerId} is not active. Current status: {customer.Status}"
            );

        var order = new Order(customerId, shippingAddress);

        await _orderRepository.SaveAsync(order);

        return order;
    }

    public async Task<Order> AddItemToOrderAsync(Guid orderId, Guid productId, int quantity)
    {
        if (orderId == Guid.Empty)
            throw new ValidationException("orderId", "Order ID is required");

        if (productId == Guid.Empty)
            throw new ValidationException("productId", "Product ID is required");

        if (quantity <= 0)
            throw new ValidationException("quantity", "Quantity must be greater than zero");

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new EntityNotFoundException(nameof(Order), orderId);

        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
            throw new EntityNotFoundException(nameof(Product), productId);

        if (!product.IsActive)
            throw new BusinessRuleException(
                "PRODUCT_INACTIVE",
                $"Product {product.Name} is not available for purchase"
            );

        product.ReserveStock(quantity);

        order.AddItem(product, quantity);

        await _productRepository.SaveAsync(product);
        await _orderRepository.SaveAsync(order);

        return order;
    }

    public async Task<Order> SubmitOrderAsync(Guid orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new EntityNotFoundException(nameof(Order), orderId);

        var customer = await _customerRepository.GetByIdAsync(order.CustomerId);
        if (customer == null)
            throw new EntityNotFoundException(nameof(Customer), order.CustomerId);

        customer.UseCredit(order.TotalAmount);

        order.Submit();

        await _customerRepository.SaveAsync(customer);
        await _orderRepository.SaveAsync(order);

        return order;
    }

    public async Task<Order> ProcessPaymentAsync(Guid orderId, Money? paymentAmount)
    {
        if (paymentAmount is null)
            throw new ArgumentNullException(nameof(paymentAmount));

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new EntityNotFoundException(nameof(Order), orderId);

        if (paymentAmount < order.TotalAmount)
            throw new BusinessRuleException(
                "INSUFFICIENT_PAYMENT",
                $"Payment amount {paymentAmount} is less than order total {order.TotalAmount}"
            );

        order.Approve();

        await _orderRepository.SaveAsync(order);

        return order;
    }

    public async Task<Order> ShipOrderAsync(Guid orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new EntityNotFoundException(nameof(Order), orderId);

        order.Ship();

        await _orderRepository.SaveAsync(order);

        return order;
    }

    public async Task CancelOrderAsync(Guid orderId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationException("reason", "Cancellation reason is required");

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new EntityNotFoundException(nameof(Order), orderId);

        var customer = await _customerRepository.GetByIdAsync(order.CustomerId);
        if (customer == null)
            throw new EntityNotFoundException(nameof(Customer), order.CustomerId);

        if (order.Status == OrderStatus.Approved || order.Status == OrderStatus.Submitted)
        {
            customer.RestoreCredit(order.TotalAmount);
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
                product.RestockProduct(item.Quantity);
                productsToUpdate.Add(product);
            }
        }

        // Batch save all products
        if (productsToUpdate.Any())
        {
            await _productRepository.SaveAllAsync(productsToUpdate);
        }

        order.Cancel(reason);

        await _customerRepository.SaveAsync(customer);
        await _orderRepository.SaveAsync(order);
    }
}
