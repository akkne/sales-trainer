using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public class AdminOpenQuestionController(AppDbContext db) : ControllerBase
{
    [HttpGet("admin/open-question/global-context")]
    public async Task<ActionResult<OpenQuestionGlobalContextDto>> GetGlobalContext()
    {
        var context = await db.OpenQuestionGlobalContexts.FirstOrDefaultAsync();
        return Ok(new OpenQuestionGlobalContextDto(context?.ContextText ?? ""));
    }

    [HttpPost("admin/open-question/global-context")]
    public async Task<ActionResult<OpenQuestionGlobalContextDto>> UpdateGlobalContext(
        [FromBody] UpdateGlobalContextRequestDto request)
    {
        var context = await db.OpenQuestionGlobalContexts.FirstOrDefaultAsync();

        if (context is null)
        {
            context = new OpenQuestionGlobalContext { ContextText = request.ContextText };
            db.OpenQuestionGlobalContexts.Add(context);
        }
        else
        {
            context.ContextText = request.ContextText;
        }

        await db.SaveChangesAsync();

        return Ok(new OpenQuestionGlobalContextDto(context.ContextText));
    }
}

public record OpenQuestionGlobalContextDto(string ContextText);
public record UpdateGlobalContextRequestDto(string ContextText);
