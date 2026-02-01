using ErrorHandling.Api.Models;
using ErrorHandling.Domain.Entities;
using ErrorHandling.Domain.Exceptions;
using ErrorHandling.Domain.Services;
using ErrorHandling.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace ErrorHandling.Api.Controllers;

/// <summary>
/// API controller for order operations using exception-based error handling.
/// </summary>
/// <remarks>
/// All endpoints call <see cref="ExceptionOrderService"/>. Failures are signaled by domain
/// exceptions (e.g. <see cref="EntityNotFoundException"/>, <see cref="ValidationException"/>),
/// which are caught by <see cref="GlobalExceptionMiddleware"/> and converted to RFC 7807
/// Problem Details. Use the result-based <see cref="OrdersResultController"/> for type-safe errors.
/// </remarks>
[ApiController]
[Route("api/v1/exception/orders")]
public class OrdersExceptionController : ControllerBase
{
    private readonly ExceptionOrderService _orderService;
    private readonly ILogger<OrdersExceptionController> _logger;

    public OrdersExceptionController(
        ExceptionOrderService orderService,
        ILogger<OrdersExceptionController> logger
    )
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new order (Exception approach)
    /// </summary>
    /// <remarks>
    /// This endpoint uses traditional exception handling.
    /// Errors are thrown as exceptions and caught by middleware.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(Order), StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status400BadRequest
    )]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status422UnprocessableEntity
    )]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        _logger.LogInformation("Creating order for customer {CustomerId}", request.CustomerId);

        // Service method throws exceptions on errors
        var order = await _orderService.CreateOrderAsync(
            request.CustomerId,
            request.ShippingAddress
        );

        _logger.LogInformation("Order {OrderId} created successfully", order.Id);

        return CreatedAtAction(nameof(GetOrder), new { orderId = order.Id }, order);
    }

    [HttpGet("{orderId}")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status404NotFound
    )]
    public async Task<IActionResult> GetOrder(Guid orderId)
    {
        // This would normally call a repository
        // For demo purposes, returning a sample or throwing
        if (orderId == Guid.Empty)
            throw new ValidationException("orderId", "Invalid order ID");

        // Simulated order retrieval
        await Task.Delay(10);

        // For demo, we'll create a sample order
        var order = new Order(Guid.NewGuid(), "123 Main St");
        return Ok(order);
    }

    [HttpPost("{orderId}/items")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status400BadRequest
    )]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status422UnprocessableEntity
    )]
    public async Task<IActionResult> AddItem(Guid orderId, [FromBody] AddItemRequest request)
    {
        _logger.LogInformation(
            "Adding item {ProductId} to order {OrderId}",
            request.ProductId,
            orderId
        );

        var order = await _orderService.AddItemToOrderAsync(
            orderId,
            request.ProductId,
            request.Quantity
        );

        return Ok(order);
    }

    [HttpPost("{orderId}/submit")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status422UnprocessableEntity
    )]
    public async Task<IActionResult> SubmitOrder(Guid orderId)
    {
        _logger.LogInformation("Submitting order {OrderId}", orderId);

        var order = await _orderService.SubmitOrderAsync(orderId);

        _logger.LogInformation("Order {OrderId} submitted successfully", orderId);

        return Ok(order);
    }

    [HttpPost("{orderId}/payment")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status400BadRequest
    )]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status422UnprocessableEntity
    )]
    public async Task<IActionResult> ProcessPayment(
        Guid orderId,
        [FromBody] ProcessPaymentRequest request
    )
    {
        _logger.LogInformation("Processing payment for order {OrderId}", orderId);

        var paymentAmount = Money.Create(request.Amount, request.Currency);
        var order = await _orderService.ProcessPaymentAsync(orderId, paymentAmount);

        return Ok(order);
    }

    [HttpPost("{orderId}/ship")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status422UnprocessableEntity
    )]
    public async Task<IActionResult> ShipOrder(Guid orderId)
    {
        _logger.LogInformation("Shipping order {OrderId}", orderId);

        var order = await _orderService.ShipOrderAsync(orderId);

        _logger.LogInformation("Order {OrderId} shipped successfully", orderId);

        return Ok(order);
    }

    [HttpPost("{orderId}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status400BadRequest
    )]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status404NotFound
    )]
    [ProducesResponseType(
        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        StatusCodes.Status422UnprocessableEntity
    )]
    public async Task<IActionResult> CancelOrder(
        Guid orderId,
        [FromBody] CancelOrderRequest request
    )
    {
        _logger.LogInformation(
            "Cancelling order {OrderId} with reason: {Reason}",
            orderId,
            request.Reason
        );

        await _orderService.CancelOrderAsync(orderId, request.Reason);

        _logger.LogInformation("Order {OrderId} cancelled successfully", orderId);

        return NoContent();
    }
}
