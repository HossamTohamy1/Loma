using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AuraCommerce.Application.Common.Interfaces;
using AuraCommerce.Application.Common.Models;
using AuraCommerce.Domain.Entities;
using AuraCommerce.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCommerce.Api.Controllers;

public record CreateOrderItemDto(
    string? ProductId,
    [Required] string ProductNameEn,
    [Required] string ProductNameAr,
    string? ProductImage,
    [Range(0.01, 1000000)] decimal UnitPrice,
    [Range(1, 1000)] int Quantity
);

public record CreateOrderRequest(
    [Required] string CustomerName,
    [Required] string CustomerPhone,
    string? CustomerEmail,
    [Required] string ShippingCity,
    [Required] string ShippingAddress,
    string? ShippingNotes,
    [Required, MinLength(1)] List<CreateOrderItemDto> Items,
    string? PromoCode = null
);

public record OrderItemDto(
    Guid Id,
    Guid? ProductId,
    string ProductNameEn,
    string ProductNameAr,
    string ProductImage,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice
);

public record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid? UserId,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    string ShippingCity,
    string ShippingAddress,
    string? ShippingNotes,
    decimal Subtotal,
    decimal Discount,
    decimal ShippingFee,
    decimal Tax,
    decimal Total,
    string? PromoCode,
    string Status,
    DateTime CreatedAtUtc,
    List<OrderItemDto> Items
);

public record UpdateOrderStatusRequest(
    [Required] string Status
);

[Route("api/[controller]")]
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public OrdersController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OrderDto>>> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Items == null || !request.Items.Any())
        {
            return BadRequest(ApiResponse<OrderDto>.ErrorResult("Cart cannot be empty when placing an order."));
        }

        if (string.IsNullOrWhiteSpace(request.CustomerPhone) || !IsValidPhoneNumber(request.CustomerPhone))
        {
            return BadRequest(ApiResponse<OrderDto>.ErrorResult("رقم الهاتف غير مطابق للمواصفات. يرجى إدخال 11 رقماً يبدأ بـ 010 أو 011 أو 012 أو 015."));
        }

        Guid? userId = null;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var parsedUserId))
        {
            userId = parsedUserId;
        }

        // Generate clean unique order number: ORD-YYYYMMDD-XXXX
        var randomPart = Random.Shared.Next(1000, 9999);
        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{randomPart}";

        var subtotal = request.Items.Sum(i => i.UnitPrice * i.Quantity);
        decimal discount = 0m;
        var shippingFee = 0m;
        var tax = 0m;
        var total = subtotal;

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            UserId = userId,
            CustomerName = request.CustomerName.Trim(),
            CustomerPhone = request.CustomerPhone.Trim(),
            CustomerEmail = request.CustomerEmail?.Trim(),
            ShippingCity = request.ShippingCity.Trim(),
            ShippingAddress = request.ShippingAddress.Trim(),
            ShippingNotes = request.ShippingNotes?.Trim(),
            Subtotal = subtotal,
            Discount = discount,
            ShippingFee = shippingFee,
            Tax = tax,
            Total = total,
            PromoCode = null,
            Status = OrderStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        foreach (var item in request.Items)
        {
            Guid? itemProductId = null;
            if (!string.IsNullOrWhiteSpace(item.ProductId) && Guid.TryParse(item.ProductId, out var parsedGuid))
            {
                itemProductId = parsedGuid;
            }

            var itemTotal = Math.Round(item.UnitPrice * item.Quantity, 2);
            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = itemProductId,
                ProductNameEn = item.ProductNameEn,
                ProductNameAr = item.ProductNameAr,
                ProductImage = item.ProductImage ?? string.Empty,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                TotalPrice = itemTotal,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        await _context.Orders.AddAsync(order, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = MapOrderToDto(order);
        return Ok(ApiResponse<OrderDto>.SuccessResult(dto, "Order placed successfully."));
    }

    [HttpGet("{id}")]
    [HttpGet("track/{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrderById(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(ApiResponse<OrderDto>.ErrorResult("يرجى إدخال رقم الطلب للبحث."));
        }

        var cleanId = id.Trim().TrimStart('#');
        Order? order = null;

        if (Guid.TryParse(cleanId, out var orderGuid))
        {
            order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderGuid, cancellationToken);
        }

        if (order == null)
        {
            order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderNumber.ToLower() == cleanId.ToLower(), cancellationToken);
        }

        // Also search by clean phone number if user typed phone
        if (order == null)
        {
            var cleanPhone = System.Text.RegularExpressions.Regex.Replace(cleanId, @"[\s\-\(\)\.]", "");
            if (cleanPhone.Length >= 8)
            {
                order = await _context.Orders
                    .Include(o => o.Items)
                    .OrderByDescending(o => o.CreatedAtUtc)
                    .FirstOrDefaultAsync(o => o.CustomerPhone.Contains(cleanPhone) || cleanPhone.Contains(o.CustomerPhone), cancellationToken);
            }
        }

        if (order == null)
        {
            return NotFound(ApiResponse<OrderDto>.ErrorResult($"لم يتم العثور على أي طلب يطابق '{id}'. يرجى التحقق من رقم الطلب والمحاولة مرة أخرى."));
        }

        return Ok(ApiResponse<OrderDto>.SuccessResult(MapOrderToDto(order)));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<PaginatedList<OrderDto>>>> GetOrders(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(o =>
                o.CustomerName.ToLower().Contains(term) ||
                o.CustomerPhone.ToLower().Contains(term) ||
                o.OrderNumber.ToLower().Contains(term) ||
                o.ShippingCity.ToLower().Contains(term) ||
                o.ShippingAddress.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(o => o.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var safePage = page <= 0 ? 1 : page;
        var safePageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);

        var orders = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        var dtos = orders.Select(MapOrderToDto).ToList();
        var paginated = new PaginatedList<OrderDto>(dtos, totalCount, safePage, safePageSize);
        return Ok(ApiResponse<PaginatedList<OrderDto>>.SuccessResult(paginated));
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> UpdateOrderStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var newStatus))
        {
            return BadRequest(ApiResponse<OrderDto>.ErrorResult($"Invalid order status '{request.Status}'. Valid statuses: {string.Join(", ", Enum.GetNames<OrderStatus>())}"));
        }

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order == null)
        {
            return NotFound(ApiResponse<OrderDto>.ErrorResult("Order not found."));
        }

        order.Status = newStatus;
        order.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<OrderDto>.SuccessResult(MapOrderToDto(order), $"Order status updated to {newStatus}."));
    }

    private static OrderDto MapOrderToDto(Order order)
    {
        return new OrderDto(
            order.Id,
            order.OrderNumber,
            order.UserId,
            order.CustomerName,
            order.CustomerPhone,
            order.CustomerEmail,
            order.ShippingCity,
            order.ShippingAddress,
            order.ShippingNotes,
            order.Subtotal,
            order.Discount,
            order.ShippingFee,
            order.Tax,
            order.Total,
            order.PromoCode,
            order.Status.ToString(),
            order.CreatedAtUtc,
            order.Items.Select(i => new OrderItemDto(
                i.Id,
                i.ProductId,
                i.ProductNameEn,
                i.ProductNameAr,
                i.ProductImage,
                i.UnitPrice,
                i.Quantity,
                i.TotalPrice
            )).ToList()
        );
    }

    private static bool IsValidPhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        var clean = System.Text.RegularExpressions.Regex.Replace(phone.Trim(), @"[\s\-\(\)\.]", "");
        if (!System.Text.RegularExpressions.Regex.IsMatch(clean, @"^\+?\d+$")) return false;

        // Egyptian mobile phone format (010, 011, 012, 015 with 8 digits, optional +20 / 0020 / 20 / 0 prefix)
        if (System.Text.RegularExpressions.Regex.IsMatch(clean, @"^(?:\+?20|0020|0)?1"))
        {
            return System.Text.RegularExpressions.Regex.IsMatch(clean, @"^(?:\+20|0020|20)?0?1[0125]\d{8}$");
        }

        // Generic international format (+ followed by 8 to 15 digits)
        if (clean.StartsWith("+"))
        {
            return System.Text.RegularExpressions.Regex.IsMatch(clean, @"^\+[1-9]\d{7,14}$");
        }

        return clean.Length >= 8 && clean.Length <= 15;
    }
}
