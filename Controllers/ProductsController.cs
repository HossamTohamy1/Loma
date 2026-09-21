using AuraCommerce.Application.Common.Models;
using AuraCommerce.Application.Features.Products.CreateProduct;
using AuraCommerce.Application.Features.Products.DeleteProduct;
using AuraCommerce.Application.Features.Products.Dtos;
using AuraCommerce.Application.Features.Products.GetProductById;
using AuraCommerce.Application.Features.Products.GetProducts;
using AuraCommerce.Application.Features.Products.UpdateProduct;
using Microsoft.AspNetCore.Mvc;

namespace AuraCommerce.Api.Controllers;

public class ProductsController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedList<ProductDto>>>> GetProducts(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? categorySlug,
        [FromQuery] string? search,
        [FromQuery] double? minRating,
        [FromQuery] bool? inStockOnly,
        [FromQuery] bool? featuredOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? sortBy = "displayOrder",
        CancellationToken cancellationToken = default)
    {
        var query = new GetProductsQuery
        {
            CategoryId = categoryId,
            CategorySlug = categorySlug,
            SearchTerm = search,
            IsFeatured = featuredOnly,
            InStockOnly = inStockOnly,
            PageNumber = page,
            PageSize = pageSize,
            SortBy = sortBy
        };

        var result = await Mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetProductById(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetProductByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> CreateProduct([FromBody] CreateProductCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetProductById), new { id = result.Data }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateProduct(Guid id, [FromBody] UpdateProductCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest(ApiResponse<bool>.ErrorResult("URL ID does not match command ID."));
        }

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteProduct(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteProductCommand(id), cancellationToken);
        return Ok(result);
    }
}
