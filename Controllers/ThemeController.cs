using AuraCommerce.Application.Common.Models;
using AuraCommerce.Application.Features.Theme.Dtos;
using AuraCommerce.Application.Features.Theme.GetTheme;
using AuraCommerce.Application.Features.Theme.UpdateTheme;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace AuraCommerce.Api.Controllers;

public class ThemeController : BaseApiController
{
    private readonly IMemoryCache _cache;

    public ThemeController(IMemoryCache cache)
    {
        _cache = cache;
    }

    private static string GetCacheKey(bool isDraft) => $"theme_setting_{isDraft.ToString().ToLowerInvariant()}";

    [HttpGet]
    public async Task<ActionResult<ApiResponse<ThemeSettingDto>>> GetTheme([FromQuery] bool isDraft = false, CancellationToken cancellationToken = default)
    {
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        var cacheKey = GetCacheKey(isDraft);
        if (_cache.TryGetValue(cacheKey, out ApiResponse<ThemeSettingDto>? cached) && cached != null)
        {
            return Ok(cached);
        }

        var result = await Mediator.Send(new GetThemeQuery(isDraft), cancellationToken);
        if (result.Success)
        {
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        }
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateTheme([FromBody] UpdateThemeCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        if (result.Success)
        {
            _cache.Remove(GetCacheKey(true));
            _cache.Remove(GetCacheKey(false));
            _cache.Remove("theme_setting_true");
            _cache.Remove("theme_setting_false");
            _cache.Remove("theme_setting_True");
            _cache.Remove("theme_setting_False");
        }
        return Ok(result);
    }
}
