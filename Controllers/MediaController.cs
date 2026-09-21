using AuraCommerce.Application.Common.Models;
using AuraCommerce.Application.Features.Media.Dtos;
using AuraCommerce.Application.Features.Media.UploadImage;
using Microsoft.AspNetCore.Mvc;

namespace AuraCommerce.Api.Controllers;

public class MediaController : BaseApiController
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<FileUploadResponse>>> Upload(
        [FromForm] IFormFile file,
        [FromForm] string? subFolder,
        CancellationToken cancellationToken)
    {
        var targetSubFolder = string.IsNullOrWhiteSpace(subFolder) ? "general" : subFolder.Trim();
        var command = new UploadImageCommand(file, targetSubFolder);
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
