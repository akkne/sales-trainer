using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Sellevate.Ai.Infrastructure.Learning;

/// <summary>
/// C-3 audit fix. Reads learning-service's global skill catalog so a dialog bundle can be labelled
/// with the skill it belongs to (docs/AUDIT_CONTRACTS.md, finding C-3).
///
/// <para>
/// <b>It degrades rather than fails</b>, the same trade <see cref="AssignmentPracticeContextClient"/>
/// makes: the dialog list is the product, the skill label decorates it, and refusing to show the
/// bundles because learning-service is unreachable would take the whole feature down with the one
/// that names it. Every failure returns an empty map and logs a warning.
/// </para>
/// </summary>
internal sealed class SkillLookupClient : ISkillLookupClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyDictionary<Guid, SkillSummary> Empty =
        new Dictionary<Guid, SkillSummary>();

    private readonly HttpClient _httpClient;
    private readonly LearningServiceConfiguration _configuration;
    private readonly ILogger<SkillLookupClient> _logger;

    public SkillLookupClient(
        HttpClient httpClient,
        IOptions<LearningServiceConfiguration> configurationOptions,
        ILogger<SkillLookupClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configurationOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<Guid, SkillSummary>> GetSkillSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        var requestUri = _configuration.BaseUrl.TrimEnd('/') + _configuration.SkillLookupPath;

        try
        {
            var payload = await _httpClient.GetFromJsonAsync<List<SkillLookupResponse>>(
                requestUri, SerializerOptions, cancellationToken);

            if (payload is null || payload.Count == 0)
            {
                return Empty;
            }

            return payload
                .Where(skill => skill.Id != Guid.Empty)
                .ToDictionary(
                    skill => skill.Id,
                    skill => new SkillSummary(skill.IconicName ?? string.Empty, skill.Title ?? string.Empty));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "The skill catalog could not be read from learning-service; dialog bundles render without a skill label.");

            return Empty;
        }
    }

    /// <summary>
    /// A local copy of learning-service's <c>SkillLookupDto</c> — services agree on a wire shape,
    /// not on a type, exactly as the Kafka contracts do.
    /// </summary>
    private sealed record SkillLookupResponse(Guid Id, string? IconicName, string? Title);
}
