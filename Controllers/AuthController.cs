using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AuraCommerce.Application.Common.Interfaces;
using AuraCommerce.Application.Common.Models;
using AuraCommerce.Domain.Entities;
using AuraCommerce.Domain.Enums;
using AuraCommerce.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCommerce.Api.Controllers;

public record RegisterRequest(
    [Required] string FullName,
    [Required, EmailAddress] string Email,
    [Required] string PhoneNumber,
    [Required, MinLength(6)] string Password,
    string? ShippingCity = null,
    string? ShippingAddress = null
);

public record LoginRequest(
    [Required] string EmailOrPhone,
    [Required] string Password
);

public record AuthResponse(
    string Token,
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    string Role,
    string? ShippingCity,
    string? ShippingAddress
);

public record UpdateProfileRequest(
    string? FullName,
    string? PhoneNumber,
    string? ShippingCity,
    string? ShippingAddress
);

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthController(IApplicationDbContext context, IJwtTokenGenerator jwtTokenGenerator)
    {
        _context = context;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var emailLower = request.Email.Trim().ToLower();
        var phoneTrimmed = request.PhoneNumber.Trim();

        if (!IsValidPhoneNumber(phoneTrimmed))
        {
            return BadRequest(ApiResponse<AuthResponse>.ErrorResult("رقم الهاتف غير مطابق للمواصفات. يرجى إدخال 11 رقماً يبدأ بـ 010 أو 011 أو 012 أو 015."));
        }

        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == emailLower, cancellationToken))
        {
            return BadRequest(ApiResponse<AuthResponse>.ErrorResult("This email address is already registered."));
        }

        if (await _context.Users.AnyAsync(u => u.PhoneNumber == phoneTrimmed, cancellationToken))
        {
            return BadRequest(ApiResponse<AuthResponse>.ErrorResult("This phone number is already registered."));
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = emailLower,
            PhoneNumber = phoneTrimmed,
            PasswordHash = PasswordHasher.HashPassword(request.Password),
            Role = UserRole.Customer, // Every registered user is strictly a Customer
            ShippingCity = request.ShippingCity?.Trim(),
            ShippingAddress = request.ShippingAddress?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user);
        var response = new AuthResponse(
            token,
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.Role.ToString(),
            user.ShippingCity,
            user.ShippingAddress
        );

        return Ok(ApiResponse<AuthResponse>.SuccessResult(response, "Account registered successfully."));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var input = request.EmailOrPhone.Trim().ToLower();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == input || u.PhoneNumber.ToLower() == input, cancellationToken);

        if (user == null || !user.IsActive || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return BadRequest(ApiResponse<AuthResponse>.ErrorResult("Invalid email/phone number or password."));
        }

        var token = _jwtTokenGenerator.GenerateToken(user);
        var response = new AuthResponse(
            token,
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.Role.ToString(),
            user.ShippingCity,
            user.ShippingAddress
        );

        return Ok(ApiResponse<AuthResponse>.SuccessResult(response, "Login successful."));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(ApiResponse<AuthResponse>.ErrorResult("Invalid authorization token."));
        }

        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null || !user.IsActive)
        {
            return NotFound(ApiResponse<AuthResponse>.ErrorResult("User account not found or deactivated."));
        }

        var token = _jwtTokenGenerator.GenerateToken(user);
        var response = new AuthResponse(
            token,
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.Role.ToString(),
            user.ShippingCity,
            user.ShippingAddress
        );

        return Ok(ApiResponse<AuthResponse>.SuccessResult(response));
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(ApiResponse<AuthResponse>.ErrorResult("Invalid authorization token."));
        }

        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
        {
            return NotFound(ApiResponse<AuthResponse>.ErrorResult("User account not found."));
        }

        if (!string.IsNullOrWhiteSpace(request.FullName)) user.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber)) user.PhoneNumber = request.PhoneNumber.Trim();
        if (request.ShippingCity != null) user.ShippingCity = request.ShippingCity.Trim();
        if (request.ShippingAddress != null) user.ShippingAddress = request.ShippingAddress.Trim();

        user.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user);
        var response = new AuthResponse(
            token,
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.Role.ToString(),
            user.ShippingCity,
            user.ShippingAddress
        );

        return Ok(ApiResponse<AuthResponse>.SuccessResult(response, "Profile updated successfully."));
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
