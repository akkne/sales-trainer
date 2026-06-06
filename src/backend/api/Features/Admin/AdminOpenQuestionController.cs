using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminOpenQuestionController(AppDbContext database) : ControllerBase
{
    [HttpGet("admin/open-question/global-context")]
    public async Task<ActionResult<OpenQuestionGlobalContextDto>> GetGlobalContext()
    {
        var context = await database.OpenQuestionGlobalContexts.FirstOrDefaultAsync();
        return Ok(new OpenQuestionGlobalContextDto(context?.ContextText ?? ""));
    }

    [HttpPost("admin/open-question/global-context")]
    public async Task<ActionResult<OpenQuestionGlobalContextDto>> UpdateGlobalContext(
        [FromBody] UpdateGlobalContextRequestDto request)
    {
        var context = await database.OpenQuestionGlobalContexts.FirstOrDefaultAsync();

        if (context is null)
        {
            context = new OpenQuestionGlobalContext { ContextText = request.ContextText };
            database.OpenQuestionGlobalContexts.Add(context);
        }
        else
        {
            context.ContextText = request.ContextText;
        }

        await database.SaveChangesAsync();

        return Ok(new OpenQuestionGlobalContextDto(context.ContextText));
    }
}
