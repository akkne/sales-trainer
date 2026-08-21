using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Sellevate.Ai.Infrastructure.Learning;

/// <summary>
/// R-15 audit fix. Process-wide cache for learning-service's skill catalog
/// (<see cref="ISkillLookupClient"/>).
///
/// <para>
/// Skills are global content — no <c>OrganizationId</c>, phase 40.10 — so one cache entry, shared
/// across every request and every tenant, is exactly the right shape. Without this, the whole
/// catalog was re-fetched over HTTP on every <c>GET /dialog/bundles</c>,
/// <c>GET /admin/dialog/bundles</c>, and even on the two write routes that only need it to decorate
/// their response.
/// </para>
///
/// <para>
/// Mirrors <see cref="Sellevate.Ai.Features.Voice.Services.Implementation.TtsAudioCache"/> — the
/// only other cache in this codebase: its own <see cref="MemoryCache"/> instance rather than the
/// ASP.NET Core DI <c>IMemoryCache</c>, registered as a singleton and injected into the
/// (per-request) typed HTTP client that reads through it.
/// </para>
/// </summary>
internal sealed class SkillCatalogCache : IDisposable
{
    private const string CacheKey = "skill-catalog";

    private static readonly IReadOnlyDictionary<Guid, SkillSummary> EmptyCatalog =
        new Dictionary<Guid, SkillSummary>();

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly TimeSpan _entryLifetime;

    public SkillCatalogCache(IOptions<LearningServiceConfiguration> configurationOptions)
    {
        _entryLifetime = TimeSpan.FromMinutes(configurationOptions.Value.SkillCatalogCacheMinutes);
    }

    public bool TryGet(out IReadOnlyDictionary<Guid, SkillSummary> catalog)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyDictionary<Guid, SkillSummary>? cached) && cached is not null)
        {
            catalog = cached;
            return true;
        }

        catalog = EmptyCatalog;
        return false;
    }

    /// <summary>Never caches the empty fallback — a transient learning-service outage should not
    /// pin every dialog bundle to "no skill label" for the rest of the entry's lifetime.</summary>
    public void Set(IReadOnlyDictionary<Guid, SkillSummary> catalog)
    {
        if (catalog.Count == 0)
        {
            return;
        }

        _cache.Set(CacheKey, catalog, _entryLifetime);
    }

    public void Dispose() => _cache.Dispose();
}
