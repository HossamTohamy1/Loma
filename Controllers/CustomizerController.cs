using AuraCommerce.Application.Common.Models;
using AuraCommerce.Application.Features.Customizer.Dtos;
using AuraCommerce.Application.Features.Customizer.GetSections;
using AuraCommerce.Application.Features.Customizer.PublishDraft;
using AuraCommerce.Application.Features.Customizer.ReorderSections;
using AuraCommerce.Application.Features.Customizer.ToggleSection;
using AuraCommerce.Application.Features.Customizer.UpdateSection;
using Microsoft.AspNetCore.Mvc;

namespace AuraCommerce.Api.Controllers;

public class CustomizerController : BaseApiController
{
    [HttpGet("sections")]
    public async Task<ActionResult<ApiResponse<List<PageSectionDto>>>> GetSections([FromQuery] bool isDraft = false, CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new GetSectionsQuery(isDraft), cancellationToken);
        return Ok(result);
    }

    [HttpPut("sections/{id:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateSection(Guid id, [FromBody] UpdateSectionCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest(ApiResponse<bool>.ErrorResult("URL ID does not match command ID."));
        }

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("sections/{id:guid}/toggle")]
    public async Task<ActionResult<ApiResponse<bool>>> ToggleSection(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ToggleSectionCommand(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("sections/reorder")]
    public async Task<ActionResult<ApiResponse<bool>>> ReorderSections([FromBody] ReorderSectionsCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("sections/publish")]
    public async Task<ActionResult<ApiResponse<bool>>> PublishDraft(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new PublishDraftCommand(), cancellationToken);
        return Ok(result);
    }
}
