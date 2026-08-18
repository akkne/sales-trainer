using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Ai.Features.Dialog.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Seeders;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Features.Dialog.Services.Implementation;
using Sellevate.Ai.Infrastructure.Mongo;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Tests.Unit;

/// <summary>
/// Phase 40.11. Fast, infrastructure-free guards for the tenancy mistakes that are easiest to make
/// in ai-service and hardest to notice. The real isolation boundary is Postgres row-level security
/// (for the dialog library) and <see cref="DialogSessionRepository"/> (for Mongo, which has no
/// RLS); both are exercised against real stores by
/// <c>Integration/AiTenantIsolationIntegrationTests</c>. These are the cheap tripwires that run on
/// every build.
/// </summary>
[TestFixture]
public class AiTenancyModelTests
{
    /// <summary>
    /// The trap docs/TENANCY/TENANCY.md §1.4 names explicitly: EF query filters are NOT inherited
    /// through navigations, so a filter on DialogBundle says nothing about DialogMode even though
    /// every read composes Bundle -> Modes. Rather than listing the entities again here (which
    /// would just repeat whichever omission it is meant to catch), this walks the model and demands
    /// a filter from anything carrying an OrganizationId.
    /// </summary>
    [Test]
    public void Every_entity_with_an_organization_id_has_its_own_query_filter()
    {
        using var databaseContext = AiDbContextFactory.CreateInMemory();

        var entitiesMissingAFilter = databaseContext.Model.GetEntityTypes()
            .Where(entityType => entityType.FindProperty(nameof(ITenantScoped.OrganizationId)) is not null)
            .Where(entityType => entityType.GetQueryFilter() is null)
            .Select(entityType => entityType.ClrType.Name)
            .OrderBy(name => name)
            .ToList();

        entitiesMissingAFilter.Should().BeEmpty();
    }

    /// <summary>
    /// The 2026-08-16 role split widened every filter so validated platform staff read across
    /// organizations. A filter that forgot the branch fails silently in production — Sellevate
    /// staff open the screen, see nothing, and everybody concludes the data is missing rather than
    /// filtered — so a missing branch fails the build here instead.
    /// </summary>
    [Test]
    public void Every_query_filter_admits_platform_staff()
    {
        using var databaseContext = AiDbContextFactory.CreateInMemory();

        var filtersMissingThePlatformBranch = databaseContext.Model.GetEntityTypes()
            .Where(entityType => entityType.GetQueryFilter() is not null)
            .Where(entityType => !entityType.GetQueryFilter()!.ToString()
                .Contains(nameof(ITenantContext.IsPlatformWide), StringComparison.Ordinal))
            .Select(entityType => entityType.ClrType.Name)
            .OrderBy(name => name)
            .ToList();

        filtersMissingThePlatformBranch.Should().BeEmpty();
    }

    /// <summary>
    /// A dialog session belongs to exactly one organization — there is no such thing as a global one —
    /// so it is tenant data. The library is nullable-owner by design, where <c>NULL</c> is the global
    /// content every organization reads, so it cannot implement an interface whose
    /// <c>OrganizationId</c> is non-nullable.
    /// </summary>
    [Test]
    public void Dialog_sessions_are_tenant_data_and_the_dialog_library_is_content()
    {
        typeof(DialogSession).Should().BeAssignableTo<ITenantScoped>();

        typeof(DialogBundle).Should().NotBeAssignableTo<ITenantScoped>();
        typeof(DialogMode).Should().NotBeAssignableTo<ITenantScoped>();
    }

    /// <summary>
    /// The roadmap's explicit call for 40.11: the seeded hidden modes stay global. Plain equality
    /// in the query filter would hand every new customer a practice page with no company-call and
    /// no custom-scenario mode — the two entry points the frontend looks up by key.
    /// </summary>
    [Test]
    public async Task Seeded_hidden_modes_stay_global_and_are_visible_to_every_organization()
    {
        var databaseName = Guid.NewGuid().ToString();
        var organizationAId = Guid.NewGuid();
        var organizationBId = Guid.NewGuid();

        await using (var seedContext = AiDbContextFactory.CreateInMemory(databaseName, organizationAId))
        {
            await CompanyCallModeSeeder.SeedAsync(seedContext);
            await CustomScenarioModeSeeder.SeedAsync(seedContext);

            var seededModes = await seedContext.DialogModes.IgnoreQueryFilters().ToListAsync();
            seededModes.Should().OnlyContain(mode => mode.OrganizationId == null);
        }

        await using var organizationBContext = AiDbContextFactory.CreateInMemory(databaseName, organizationBId);
        var visibleKeys = await organizationBContext.DialogModes
            .Select(mode => mode.Key)
            .ToListAsync();

        visibleKeys.Should().Contain([DialogModeKeys.CompanyCall, DialogModeKeys.CustomScenario]);
    }

    [Test]
    public async Task One_organizations_authored_bundle_is_invisible_to_another()
    {
        var databaseName = Guid.NewGuid().ToString();
        var organizationAId = Guid.NewGuid();
        var organizationBId = Guid.NewGuid();

        await using (var seedContext = AiDbContextFactory.CreateInMemory(databaseName, organizationAId))
        {
            seedContext.DialogBundles.Add(NewBundle("global", organizationId: null));
            seedContext.DialogBundles.Add(NewBundle("owned-by-a", organizationAId));
            seedContext.DialogBundles.Add(NewBundle("owned-by-b", organizationBId));
            await seedContext.SaveChangesAsync();
        }

        await using var organizationAContext = AiDbContextFactory.CreateInMemory(databaseName, organizationAId);
        var visibleToA = await organizationAContext.DialogBundles.Select(bundle => bundle.Title).ToListAsync();

        await using var organizationBContext = AiDbContextFactory.CreateInMemory(databaseName, organizationBId);
        var visibleToB = await organizationBContext.DialogBundles.Select(bundle => bundle.Title).ToListAsync();

        visibleToA.Should().BeEquivalentTo("global", "owned-by-a");
        visibleToB.Should().BeEquivalentTo("global", "owned-by-b");
    }

    /// <summary>
    /// Roadmap 40.14: "an unset tenant is an exception, never a licence". Mongo has no policy to
    /// fall back on, so the repository has to be the thing that refuses — and it has to refuse on
    /// <em>every</em> method, not on the ones somebody remembered. This walks the interface instead
    /// of listing methods, so a method added later without the guard fails the build.
    /// </summary>
    [Test]
    public async Task Every_session_repository_method_refuses_an_unset_tenant()
    {
        var repository = BuildRepositoryWithoutTenant();
        var methods = typeof(IDialogSessionRepository).GetMethods();

        methods.Should().NotBeEmpty();

        foreach (var method in methods)
        {
            var arguments = method.GetParameters().Select(BuildArgument).ToArray();

            var act = async () =>
            {
                var invocationResult = method.Invoke(repository, arguments);
                await (Task)invocationResult!;
            };

            (await act.Should().ThrowAsync<InvalidOperationException>(
                    "{0} must refuse an unset tenant rather than widening to every organization",
                    method.Name))
                .WithMessage("Organization context is not set.");
        }
    }

    /// <summary>
    /// The repository is only a boundary while it is the sole holder of the collection. Nothing in
    /// C# stops a future service from calling <c>GetCollection&lt;DialogSession&gt;</c> itself and
    /// quietly reintroducing an un-scoped read, so this asserts on the source tree: exactly one
    /// production file may name the collection.
    /// </summary>
    [Test]
    public void Only_the_repository_reaches_the_dialog_sessions_collection()
    {
        var serviceRoot = FindAiServiceSourceRoot();
        if (serviceRoot is null)
        {
            Assert.Ignore("ai-service sources are not next to the test assembly; nothing to scan.");
            return;
        }

        var offendingFiles = Directory
            .EnumerateFiles(serviceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path).Contains("GetCollection<DialogSession>", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToList();

        offendingFiles.Should().BeEquivalentTo([$"{nameof(DialogSessionRepository)}.cs"],
            "the tenant filter for Mongo is application-level, so it is only auditable while there is one place to audit");
    }

    private static DialogBundle NewBundle(string title, Guid? organizationId) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        SkillId = Guid.NewGuid(),
        Title = title,
        Description = title,
        IconEmoji = "🎯",
    };

    /// <summary>
    /// A repository over a substituted Mongo driver and an empty <see cref="TenantContext"/>. The
    /// substitutes are never reached: every method under test throws before it issues a command,
    /// which is precisely the property being asserted.
    /// </summary>
    private static IDialogSessionRepository BuildRepositoryWithoutTenant()
    {
        var mongoDatabase = Substitute.For<IMongoDatabase>();
        mongoDatabase
            .GetCollection<DialogSession>(Arg.Any<string>(), Arg.Any<MongoCollectionSettings>())
            .Returns(Substitute.For<IMongoCollection<DialogSession>>());

        var mongoClient = Substitute.For<IMongoClient>();
        mongoClient.GetDatabase(Arg.Any<string>(), Arg.Any<MongoDatabaseSettings>()).Returns(mongoDatabase);

        var mongoContext = new MongoDbContext(mongoClient, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        return new DialogSessionRepository(mongoContext, new TenantContext());
    }

    private static object? BuildArgument(ParameterInfo parameter)
    {
        var parameterType = parameter.ParameterType;

        if (parameterType == typeof(string))
        {
            return "session-id";
        }

        if (parameterType == typeof(DialogSession))
        {
            return new DialogSession { UserId = Guid.NewGuid() };
        }

        if (parameterType == typeof(DialogFeedback))
        {
            return new DialogFeedback();
        }

        if (parameterType == typeof(IReadOnlyCollection<DialogMessage>))
        {
            return new List<DialogMessage>();
        }

        return parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null;
    }

    /// <summary>
    /// Walks up from this file's compile-time path to the ai-service source root. Compile-time,
    /// not runtime, because the test binary lives under bin/ and knows nothing about the tree.
    /// </summary>
    private static string? FindAiServiceSourceRoot([CallerFilePath] string thisFilePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(thisFilePath)!);

        while (directory is not null && directory.Name != "ai-service")
        {
            directory = directory.Parent;
        }

        var serviceRoot = directory is null ? null : Path.Combine(directory.FullName, "Ai");
        return serviceRoot is not null && Directory.Exists(serviceRoot) ? serviceRoot : null;
    }
}
