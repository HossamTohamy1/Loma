using AuraCommerce.Application.Common.Models;
using AuraCommerce.Application.Features.Categories.CreateCategory;
using AuraCommerce.Application.Features.Categories.Dtos;
using AuraCommerce.Application.Features.Categories.GetCategories;
using AuraCommerce.Application.Features.Categories.UpdateCategory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace AuraCommerce.Api.Controllers;

public class CategoriesController : BaseApiController
{
    private readonly IMemoryCache _cache;

    public CategoriesController(IMemoryCache cache)
    {
        _cache = cache;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<CategoryDto>>>> GetCategories([FromQuery] bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"categories_list_{activeOnly}";
        if (_cache.TryGetValue(cacheKey, out ApiResponse<List<CategoryDto>>? cached) && cached != null)
        {
            return Ok(cached);
        }

        var result = await Mediator.Send(new GetCategoriesQuery(activeOnly), cancellationToken);
        if (result.Success)
        {
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
        }
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> CreateCategory([FromBody] CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        if (result.Success)
        {
            _cache.Remove("categories_list_true");
            _cache.Remove("categories_list_false");
        }
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateCategory(Guid id, [FromBody] UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest(ApiResponse<bool>.ErrorResult("URL ID does not match command ID."));
        }

        var result = await Mediator.Send(command, cancellationToken);
        if (result.Success)
        {
            _cache.Remove("categories_list_true");
            _cache.Remove("categories_list_false");
        }
        return Ok(result);
    }
}
