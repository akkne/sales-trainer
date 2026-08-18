using System.Text.Json.Serialization;

namespace Sellevate.Ai.Common.Extensions;

/// <summary>
/// Registers the MVC layer: controllers, the JSON contract they speak, problem details, and Swagger.
///
/// <para>
/// <b>Enum values arrive as strings and must be accepted as such.</b> company-service serializes the
/// persona <c>Difficulty</c> enum as a name rather than a number, so without the string enum converter
/// the default numeric-only binding fails and <c>[ApiController]</c> auto-returns 400 before the
/// controller runs — a 400 with no clue in it, because model binding failed rather than validation.
/// </para>
/// </summary>
public static class AiPresentationServiceCollectionExtensions
{
    public static IServiceCollection AddAiPresentation(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(jsonOptions =>
                jsonOptions.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddProblemDetails();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }
}
