using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AuraCommerce.Application.Common.Interfaces;
using AuraCommerce.Application.Common.Models;
using AuraCommerce.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCommerce.Api.Controllers;

public record CartItemDto(
    Guid Id,
    string ProductId,
    string ProductNameEn,
    string ProductNameAr,
    string? ProductImage,
    string? ProductBrand,
    string? ProductCategory,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice
);

public record CartDto(
    Guid Id,
    string SessionId,
    Guid? UserId,
    string? PromoCode,
    List<CartItemDto> Items,
    decimal Subtotal,
    decimal Discount,
    decimal Shipping,
    decimal Tax,
    decimal Total,
    int ItemCount
);

public record AddToCartRequest(
    string? SessionId,
    Guid? UserId,
    [Required] string ProductId,
    [Required] string ProductNameEn,
    [Required] string ProductNameAr,
    string? ProductImage,
    string? ProductBrand,
    string? ProductCategory,
    [Range(0.01, 1000000)] decimal UnitPrice,
    [Range(1, 1000)] int Quantity = 1
);

public record UpdateCartItemQuantityRequest(
    string? SessionId,
    Guid? UserId,
    [Range(0, 1000)] int Quantity
);

public record ApplyPromoRequest(
    string? SessionId,
    Guid? UserId,
    string? PromoCode
);

public record SyncCartItemDto(
    [Required] string ProductId,
    [Required] string ProductNameEn,
    [Required] string ProductNameAr,
    string? ProductImage,
    string? ProductBrand,
    string? ProductCategory,
    decimal UnitPrice,
    int Quantity
);

public record SyncCartRequest(
    string? SessionId,
    Guid? UserId,
    string? PromoCode,
    List<SyncCartItemDto>? Items
);

[Route("api/[controller]")]
[ApiController]
public class CartController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public CartController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CartDto>>> GetCart(
        [FromQuery] string? sessionId,
        [FromQuery] Guid? userId,
        CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateCartAsync(sessionId, userId, cancellationToken);
        return Ok(ApiResponse<CartDto>.SuccessResult(MapToDto(cart)));
    }

    [HttpGet("user/{userId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CartDto>>> GetUserCart(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateCartAsync(null, userId, cancellationToken);
        return Ok(ApiResponse<CartDto>.SuccessResult(MapToDto(cart)));
    }

    [HttpPost("items")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CartDto>>> AddItem(
        [FromBody] AddToCartRequest request,
        CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateCartAsync(request.SessionId, request.UserId, cancellationToken);

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
        if (existingItem != null)
        {
            existingItem.Quantity += request.Quantity > 0 ? request.Quantity : 1;
            existingItem.UnitPrice = request.UnitPrice;
            existingItem.ProductNameEn = request.ProductNameEn;
            existingItem.ProductNameAr = request.ProductNameAr;
            if (!string.IsNullOrEmpty(request.ProductImage))
            {
                existingItem.ProductImage = request.ProductImage;
            }
        }
        else
        {
            var newItem = new CartItemRecord
            {
                CartId = cart.Id,
                ProductId = request.ProductId,
                ProductNameEn = request.ProductNameEn,
                ProductNameAr = request.ProductNameAr,
                ProductImage = request.ProductImage ?? string.Empty,
                ProductBrand = request.ProductBrand,
                ProductCategory = request.ProductCategory,
                UnitPrice = request.UnitPrice,
                Quantity = request.Quantity > 0 ? request.Quantity : 1
            };
            await _context.CartItems.AddAsync(newItem, cancellationToken);
            cart.Items.Add(newItem);
        }

        cart.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<CartDto>.SuccessResult(MapToDto(cart), "Item added to cart"));
    }

    [HttpPut("items/{productId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CartDto>>> UpdateQuantity(
        string productId,
        [FromBody] UpdateCartItemQuantityRequest request,
        CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateCartAsync(request.SessionId, request.UserId, cancellationToken);

        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            if (request.Quantity <= 0)
            {
                cart.Items.Remove(item);
                _context.CartItems.Remove(item);
            }
            else
            {
                item.Quantity = request.Quantity;
            }

            cart.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(ApiResponse<CartDto>.SuccessResult(MapToDto(cart), "Cart updated"));
    }

    [HttpDelete("items/{productId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CartDto>>> RemoveItem(
        string productId,
        [FromQuery] string? sessionId,
        [FromQuery] Guid? userId,
        CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateCartAsync(sessionId, userId, cancellationToken);

        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            cart.Items.Remove(item);
            _context.CartItems.Remove(item);
            cart.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(ApiResponse<CartDto>.SuccessResult(MapToDto(cart), "Item removed from cart"));
    }

    [HttpDelete]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CartDto>>> ClearCart(
        [FromQuery] string? sessionId,
        [FromQuery] Guid? userId,
        CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateCartAsync(sessionId, userId, cancellationToken);

        if (cart.Items.Any())
        {
            _context.CartItems.RemoveRange(cart.Items);
            cart.Items.Clear();
            cart.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(ApiResponse<CartDto>.SuccessResult(MapToDto(cart), "Cart cleared"));
    }

    [HttpPost("promo")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CartDto>>> ApplyPromo(
        [FromBody] ApplyPromoRequest request,
        CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateCartAsync(request.SessionId, request.UserId, cancellationToken);
        cart.PromoCode = string.IsNullOrWhiteSpace(request.PromoCode) ? null : request.PromoCode.Trim().ToUpperInvariant();
        cart.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<CartDto>.SuccessResult(MapToDto(cart), "Promo code applied"));
    }

    [HttpPost("sync")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CartDto>>> SyncCart(
        [FromBody] SyncCartRequest request,
        CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateCartAsync(request.SessionId, request.UserId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.PromoCode))
        {
            cart.PromoCode = request.PromoCode.Trim().ToUpperInvariant();
        }

        if (request.Items != null)
        {
            // Clear existing and replace with synced items
            if (cart.Items.Any())
            {
                _context.CartItems.RemoveRange(cart.Items);
                cart.Items.Clear();
            }

            foreach (var itemDto in request.Items)
            {
                var record = new CartItemRecord
                {
                    CartId = cart.Id,
                    ProductId = itemDto.ProductId,
                    ProductNameEn = itemDto.ProductNameEn,
                    ProductNameAr = itemDto.ProductNameAr,
                    ProductImage = itemDto.ProductImage ?? string.Empty,
                    ProductBrand = itemDto.ProductBrand,
                    ProductCategory = itemDto.ProductCategory,
                    UnitPrice = itemDto.UnitPrice,
                    Quantity = itemDto.Quantity > 0 ? itemDto.Quantity : 1
                };
                await _context.CartItems.AddAsync(record, cancellationToken);
                cart.Items.Add(record);
            }

            cart.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(ApiResponse<CartDto>.SuccessResult(MapToDto(cart), "Cart synchronized successfully"));
    }

    private Guid? ResolveUserId(Guid? explicitUserId = null)
    {
        // 1. Try JWT Claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value
                          ?? User.FindFirst("id")?.Value;
        if (Guid.TryParse(userIdClaim, out var fromClaim) && fromClaim != Guid.Empty)
        {
            return fromClaim;
        }

        // 2. Try explicit parameter from body or query
        if (explicitUserId.HasValue && explicitUserId.Value != Guid.Empty)
        {
            return explicitUserId.Value;
        }

        // 3. Try X-User-Id request header
        var headerUserId = Request.Headers["X-User-Id"].FirstOrDefault();
        if (Guid.TryParse(headerUserId, out var fromHeader) && fromHeader != Guid.Empty)
        {
            return fromHeader;
        }

        return null;
    }

    private async Task<Cart> GetOrCreateCartAsync(
        string? explicitSessionId,
        Guid? explicitUserId,
        CancellationToken cancellationToken)
    {
        var currentUserId = ResolveUserId(explicitUserId);

        var sessionId = explicitSessionId;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = Request.Headers["X-Session-Id"].FirstOrDefault();
        }

        Cart? cart = null;

        // SCENARIO 1: Authenticated / Identified User
        if (currentUserId.HasValue)
        {
            // Strict query: Find cart belonging strictly to this user
            cart = await _context.Carts
                .Include(c => c.Items)
                .OrderByDescending(c => c.UpdatedAtUtc)
                .FirstOrDefaultAsync(c => c.UserId == currentUserId.Value, cancellationToken);

            // If user has no existing cart, check if there is an anonymous guest cart with this session
            // ONLY claim it if it has NO user (c.UserId == null). NEVER steal or return another user's cart!
            if (cart == null && !string.IsNullOrWhiteSpace(sessionId))
            {
                var guestCart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.SessionId == sessionId && c.UserId == null, cancellationToken);

                if (guestCart != null)
                {
                    guestCart.UserId = currentUserId.Value;
                    guestCart.SessionId = $"user_{currentUserId.Value}_{Guid.NewGuid():N}";
                    guestCart.UpdatedAtUtc = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                    return guestCart;
                }
            }

            // If still no cart, create a fresh cart for this user
            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = currentUserId.Value,
                    SessionId = $"user_{currentUserId.Value}_{Guid.NewGuid():N}",
                    PromoCode = "FALL10",
                    Items = new List<CartItemRecord>()
                };

                await _context.Carts.AddAsync(cart, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return cart;
        }

        // SCENARIO 2: Anonymous Guest
        // A guest MUST NEVER access any cart that has a UserId != null
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sessionId && c.UserId == null, cancellationToken);
        }

        // If no guest cart found or sessionId belongs to a registered user, generate new guest cart
        if (cart == null)
        {
            var newSessionId = (string.IsNullOrWhiteSpace(sessionId) || sessionId.StartsWith("user_"))
                ? "sess_" + Guid.NewGuid().ToString("N")
                : sessionId;

            cart = new Cart
            {
                UserId = null,
                SessionId = newSessionId,
                PromoCode = null,
                Items = new List<CartItemRecord>()
            };

            await _context.Carts.AddAsync(cart, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (cart.PromoCode == "FALL10")
        {
            cart.PromoCode = null;
        }

        return cart;
    }

    private static CartDto MapToDto(Cart cart)
    {
        var itemDtos = cart.Items.Select(i => new CartItemDto(
            i.Id,
            i.ProductId,
            i.ProductNameEn,
            i.ProductNameAr,
            i.ProductImage,
            i.ProductBrand,
            i.ProductCategory,
            i.UnitPrice,
            i.Quantity,
            i.UnitPrice * i.Quantity
        )).ToList();

        var subtotal = itemDtos.Sum(i => i.TotalPrice);
        var discount = 0m;
        var shipping = 0m;
        var tax = 0m;
        var total = subtotal;

        return new CartDto(
            cart.Id,
            cart.SessionId,
            cart.UserId,
            null,
            itemDtos,
            subtotal,
            discount,
            shipping,
            tax,
            total,
            itemDtos.Sum(i => i.Quantity)
        );
    }
}
