using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Sellevate.Ai.Infrastructure.Learning;

/// <summary>
/// R-15 audit fix. Process-wide cache for learning-service's skill catalog
/// (<see cref="ISkillLookupClient"/>).
///
/// <para>
/// Skills are mostly global content, but <c>Skill.OrganizationId</c> is a real, tenant-scoped
/// column — <c>LearningTenancyModelTests</c> seeds an organization-owned skill — so a single
/// process-wide entry is the wrong shape even though every request today happens to resolve to the
/// same "no organization header on this internal call" catalog. Without this cache, the whole
/// catalog was re-fetched over HTTP on every <c>GET /dialog/bundles</c>,
/// <c>GET /admin/dialog/bundles</c>, and even on the two write routes that only need it to decorate
/// their response.
/// </para>
///
/// <para>
/// <b>R2-11 audit fix: one entry per tenant, not one entry for the whole process.</b>
/// <see cref="TryGet"/>/<see cref="Set"/> key on the caller's own <c>IsPlatformWide</c>/
/// <c>OrganizationId</c>, taken from <see cref="Sellevate.BuildingBlocks.Tenancy.ITenantContext"/>
/// the same way <see cref="AssignmentPracticeContextClient"/> already does for its own outbound
/// call. Today that always resolves to the same "platform" slot, because the internal lookup this
/// caches carries no organization header — but nothing here should depend on that staying true. The
/// day that internal contract gains a header or a platform-wide mode, a tenant-scoped skill can only
/// ever land in its own tenant's cache slot, never a shared one another tenant's request would read
/// back during the TTL.
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
    private const string CacheKeyPrefix = "skill-catalog:";
    private const string PlatformWideCacheKey = CacheKeyPrefix + "platform";

    private static readonly IReadOnlyDictionary<Guid, SkillSummary> EmptyCatalog =
        new Dictionary<Guid, SkillSummary>();

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly TimeSpan _entryLifetime;

    public SkillCatalogCache(IOptions<LearningServiceConfiguration> configurationOptions)
    {
        _entryLifetime = TimeSpan.FromMinutes(configurationOptions.Value.SkillCatalogCacheMinutes);
    }

    /// <summary>Same rule the EF query filter on <c>Skill</c> uses: platform-wide or no
    /// organization both mean "the global catalog", so they share one slot.</summary>
    private static string CacheKeyFor(bool isPlatformWide, Guid? organizationId) =>
        isPlatformWide || organizationId is null
            ? PlatformWideCacheKey
            : CacheKeyPrefix + organizationId.Value;

    public bool TryGet(bool isPlatformWide, Guid? organizationId, out IReadOnlyDictionary<Guid, SkillSummary> catalog)
    {
        var cacheKey = CacheKeyFor(isPlatformWide, organizationId);
        if (_cache.TryGetValue(cacheKey, out IReadOnlyDictionary<Guid, SkillSummary>? cached) && cached is not null)
        {
            catalog = cached;
            return true;
        }

        catalog = EmptyCatalog;
        return false;
    }

    /// <summary>Never caches the empty fallback — a transient learning-service outage should not
    /// pin every dialog bundle to "no skill label" for the rest of the entry's lifetime.</summary>
    public void Set(bool isPlatformWide, Guid? organizationId, IReadOnlyDictionary<Guid, SkillSummary> catalog)
    {
        if (catalog.Count == 0)
        {
            return;
        }

        _cache.Set(CacheKeyFor(isPlatformWide, organizationId), catalog, _entryLifetime);
    }

    public void Dispose() => _cache.Dispose();
}
