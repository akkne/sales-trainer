using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Reference.Models;
using Sellevate.Learning.Features.Techniques.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Content;

/// <summary>
/// Phase 40.18. Read resolution, the second half of copy-on-write: <b>an override exists → use it;
/// otherwise → the global row</b> (docs/TENANCY/CONTENT_MODEL.md §1).
///
/// <para>
/// The tenancy query filter alone does not do this. It admits "mine or global", which for an
/// organization that has overridden three lessons means it sees those three lessons <em>twice</em> —
/// once as the customer's copy and once as the base. Resolution is the missing half: hide a global
/// row when the caller's own organization has a live override of it.
/// </para>
///
/// <para>
/// <b>Why this is an explicit call and not a query filter.</b> A <c>HasQueryFilter</c> that
/// references its own <c>DbSet</c> is applied recursively to the subquery it contains, and EF has no
/// way to express "the anti-join, but unfiltered". More importantly, the admin panel must see both
/// sides: the review screen's whole job is showing what the base says next to what the organization
/// changed, and a filter that hid the base would make the queue unbuildable. So the rule is: the
/// <b>learner-facing</b> read paths resolve, the authoring paths do not.
/// </para>
///
/// <para>
/// <b>Platform-wide callers do not resolve either</b>, and that is not an oversight. In platform
/// mode the query filter admits every organization's rows at once, so "somebody's override exists"
/// would hide the global lesson from Sellevate staff because one customer edited it. Staff read the
/// library; a customer reads their resolved view of it.
/// </para>
///
/// <para>
/// Cost on the hot path is one <c>NOT EXISTS</c> against <c>IX_Lessons_ParentLessonId</c> /
/// <c>IX_Techniques_ParentTechniqueId</c> / <c>IX_ReferenceMaterials_ParentMaterialId</c>, all of
/// which exist for this and nothing else.
/// </para>
/// </summary>
public static class ContentOverrideResolution
{
    public static IQueryable<Lesson> ResolveOverrides(
        this IQueryable<Lesson> lessons,
        LearningDbContext databaseContext)
    {
        ArgumentNullException.ThrowIfNull(lessons);
        ArgumentNullException.ThrowIfNull(databaseContext);

        var tenantContext = databaseContext.TenantContext;

        var visible = lessons.Where(lesson => !lesson.IsArchived);

        if (tenantContext.OrganizationId is not { } organizationId || tenantContext.IsPlatformWide)
        {
            return visible;
        }

        return visible.Where(lesson =>
            lesson.OrganizationId != null
            || !databaseContext.Lessons.Any(candidate =>
                candidate.ParentLessonId == lesson.Id
                && candidate.OrganizationId == organizationId
                && !candidate.IsArchived));
    }

    public static IQueryable<Technique> ResolveOverrides(
        this IQueryable<Technique> techniques,
        LearningDbContext databaseContext)
    {
        ArgumentNullException.ThrowIfNull(techniques);
        ArgumentNullException.ThrowIfNull(databaseContext);

        var tenantContext = databaseContext.TenantContext;

        var visible = techniques.Where(technique => !technique.IsArchived);

        if (tenantContext.OrganizationId is not { } organizationId || tenantContext.IsPlatformWide)
        {
            return visible;
        }

        return visible.Where(technique =>
            technique.OrganizationId != null
            || !databaseContext.Techniques.Any(candidate =>
                candidate.ParentTechniqueId == technique.Id
                && candidate.OrganizationId == organizationId
                && !candidate.IsArchived));
    }

    public static IQueryable<ReferenceMaterial> ResolveOverrides(
        this IQueryable<ReferenceMaterial> materials,
        LearningDbContext databaseContext)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(databaseContext);

        var tenantContext = databaseContext.TenantContext;

        var visible = materials.Where(material => !material.IsArchived);

        if (tenantContext.OrganizationId is not { } organizationId || tenantContext.IsPlatformWide)
        {
            return visible;
        }

        return visible.Where(material =>
            material.OrganizationId != null
            || !databaseContext.ReferenceMaterials.Any(candidate =>
                candidate.ParentMaterialId == material.Id
                && candidate.OrganizationId == organizationId
                && !candidate.IsArchived));
    }
}
