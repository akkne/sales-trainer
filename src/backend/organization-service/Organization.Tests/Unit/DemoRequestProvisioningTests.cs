using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Email.Abstract;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Organization.Eventing;
using Sellevate.Organization.Features.DemoRequests.Exceptions;
using Sellevate.Organization.Features.DemoRequests.Models;
using Sellevate.Organization.Features.DemoRequests.Services.Abstract;
using Sellevate.Organization.Features.DemoRequests.Services.Implementation;
using Sellevate.Organization.Features.Organizations.Exceptions;
using Sellevate.Organization.Infrastructure.Data;
using Sellevate.Organization.Infrastructure.Identity;
using Sellevate.Organization.Infrastructure.Identity.Exceptions;
using Sellevate.Organization.Tests.Helpers;
using DemoRequestEntity = Sellevate.Organization.Features.DemoRequests.Models.DemoRequest;

namespace Sellevate.Organization.Tests.Unit;

[TestFixture]
public sealed class DemoRequestProvisioningTests
{
    private static readonly Guid ActorUserId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");

    private OrganizationDbContext _databaseContext = null!;
    private IIdentityOrganizationBootstrapClient _identityOrganizationBootstrapClient = null!;
    private IEventPublisher _eventPublisher = null!;
    private DemoRequestProvisioningService _provisioningService = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseContext = TestOrganizationDatabaseFactory.CreateInMemory();
        _identityOrganizationBootstrapClient = Substitute.For<IIdentityOrganizationBootstrapClient>();
        _eventPublisher = Substitute.For<IEventPublisher>();
        _provisioningService = new DemoRequestProvisioningService(
            _databaseContext,
            _identityOrganizationBootstrapClient,
            _eventPublisher,
            NullLogger<DemoRequestProvisioningService>.Instance);

        _identityOrganizationBootstrapClient
            .BootstrapAdministratorAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new IdentityBootstrapAdminResult(Guid.NewGuid(), "admin@example.com", DateTime.UtcNow.AddDays(7)));
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    private async Task<DemoRequestEntity> SeedDemoRequestAsync(
        string? workEmail = null, string? companyName = null, Guid? id = null)
    {
        var demoRequest = new DemoRequestEntity
        {
            Id = id ?? Guid.NewGuid(),
            FullName = "Jane Doe",
            WorkEmail = workEmail ?? "jane@example.com",
            Phone = "+7 900 123-45-67",
            CompanyName = companyName ?? "Acme Inc",
            SalesTeamSize = SalesTeamSize.SixToTwenty,
            Status = DemoRequestStatus.New,
            ProvisioningState = DemoRequestProvisioningState.NotProvisioned,
            ConsentGivenAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _databaseContext.DemoRequests.Add(demoRequest);
        await _databaseContext.SaveChangesAsync();
        return demoRequest;
    }

    private static ProvisionDemoRequestRequestDto EmptyRequest() => new(null, null, null, null);

    [Test]
    public async Task ProvisionAsync_creates_the_organization_and_the_invite_end_to_end()
    {
        var demoRequest = await SeedDemoRequestAsync(workEmail: "lead@example.com", companyName: "Acme Sales");

        var result = await _provisioningService.ProvisionAsync(demoRequest.Id, EmptyRequest(), ActorUserId);

        result.Should().NotBeNull();
        result!.AlreadyProvisioned.Should().BeFalse();
        result.Status.Should().Be(nameof(DemoRequestStatus.Approved));
        result.ProvisioningState.Should().Be(nameof(DemoRequestProvisioningState.AdminInvited));
        result.Organization.Name.Should().Be("Acme Sales");
        result.Organization.Slug.Should().Be("acme-sales");
        result.InviteEmail.Should().Be("admin@example.com");
        result.InviteExpiresAt.Should().NotBeNull();

        var stored = await _databaseContext.DemoRequests.FindAsync(demoRequest.Id);
        stored!.OrganizationId.Should().Be(result.Organization.Id);
        stored.ProvisioningState.Should().Be(DemoRequestProvisioningState.AdminInvited);
        stored.Status.Should().Be(DemoRequestStatus.Approved);
        stored.BootstrapAdminEmail.Should().Be("lead@example.com");
        stored.BootstrapInviteId.Should().Be(result.InviteId);
        stored.ProvisionedAt.Should().NotBeNull();

        (await _databaseContext.Organizations.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task ProvisionAsync_publishes_organization_created_exactly_once_on_a_first_provision()
    {
        var demoRequest = await SeedDemoRequestAsync();

        await _provisioningService.ProvisionAsync(demoRequest.Id, EmptyRequest(), ActorUserId);

        await _eventPublisher.Received(1).PublishAsync(
            Topics.OrganizationCreated,
            Arg.Any<string>(),
            Topics.OrganizationCreated,
            Arg.Any<OrganizationCreatedEvent>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProvisionAsync_defaults_the_admin_email_to_the_leads_work_email()
    {
        var demoRequest = await SeedDemoRequestAsync(workEmail: "defaulted@example.com");

        await _provisioningService.ProvisionAsync(demoRequest.Id, EmptyRequest(), ActorUserId);

        await _identityOrganizationBootstrapClient.Received(1).BootstrapAdministratorAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), "defaulted@example.com",
            Arg.Any<string?>(), ActorUserId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProvisionAsync_uses_an_explicit_admin_email_override()
    {
        var demoRequest = await SeedDemoRequestAsync(workEmail: "form-submitter@example.com");

        await _provisioningService.ProvisionAsync(
            demoRequest.Id, new ProvisionDemoRequestRequestDto(null, null, "  Chosen@Example.com  ", null), ActorUserId);

        await _identityOrganizationBootstrapClient.Received(1).BootstrapAdministratorAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), "chosen@example.com",
            Arg.Any<string?>(), ActorUserId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProvisionAsync_returns_404_shaped_null_for_an_unknown_lead()
    {
        var result = await _provisioningService.ProvisionAsync(Guid.NewGuid(), EmptyRequest(), ActorUserId);

        result.Should().BeNull();
    }

    [Test]
    public async Task ProvisionAsync_on_slug_collision_throws_with_zero_writes()
    {
        _databaseContext.Organizations.Add(new Sellevate.Organization.Features.Organizations.Models.Organization
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            Slug = "acme-inc",
            Status = Sellevate.Organization.Features.Organizations.Models.OrganizationStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _databaseContext.SaveChangesAsync();

        var demoRequest = await SeedDemoRequestAsync(companyName: "Acme Inc");

        var act = async () => await _provisioningService.ProvisionAsync(demoRequest.Id, EmptyRequest(), ActorUserId);

        await act.Should().ThrowAsync<OrganizationSlugConflictException>();

        (await _databaseContext.Organizations.CountAsync()).Should().Be(1);
        var stored = await _databaseContext.DemoRequests.FindAsync(demoRequest.Id);
        stored!.OrganizationId.Should().BeNull();
        stored.ProvisioningState.Should().Be(DemoRequestProvisioningState.NotProvisioned);
        await _identityOrganizationBootstrapClient.DidNotReceiveWithAnyArgs().BootstrapAdministratorAsync(
            default, default!, default!, default!, default, default, default);
    }

    [Test]
    public async Task ProvisionAsync_succeeds_when_an_explicit_slug_avoids_the_collision()
    {
        _databaseContext.Organizations.Add(new Sellevate.Organization.Features.Organizations.Models.Organization
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            Slug = "acme-inc",
            Status = Sellevate.Organization.Features.Organizations.Models.OrganizationStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _databaseContext.SaveChangesAsync();

        var demoRequest = await SeedDemoRequestAsync(companyName: "Acme Inc");

        var result = await _provisioningService.ProvisionAsync(
            demoRequest.Id, new ProvisionDemoRequestRequestDto(null, "acme-inc-2", null, null), ActorUserId);

        result.Should().NotBeNull();
        result!.Organization.Slug.Should().Be("acme-inc-2");
    }

    [Test]
    public async Task ProvisionAsync_when_identity_call_throws_leaves_the_lead_at_OrganizationCreated_and_never_a_500()
    {
        _identityOrganizationBootstrapClient
            .BootstrapAdministratorAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<IdentityBootstrapAdminResult>(_ => throw new InvalidOperationException("identity-service is unreachable"));

        var demoRequest = await SeedDemoRequestAsync();

        var act = async () => await _provisioningService.ProvisionAsync(demoRequest.Id, EmptyRequest(), ActorUserId);

        await act.Should().ThrowAsync<DemoRequestInviteFailedException>();

        (await _databaseContext.Organizations.CountAsync()).Should().Be(1);
        var stored = await _databaseContext.DemoRequests.FindAsync(demoRequest.Id);
        stored!.OrganizationId.Should().NotBeNull();
        stored.ProvisioningState.Should().Be(DemoRequestProvisioningState.OrganizationCreated);
        stored.Status.Should().Be(DemoRequestStatus.Approved);
    }

    [Test]
    public async Task ProvisionAsync_retry_after_an_invite_failure_converges_without_a_second_organization()
    {
        _identityOrganizationBootstrapClient
            .BootstrapAdministratorAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<IdentityBootstrapAdminResult>(_ => throw new InvalidOperationException("identity-service is unreachable"));

        var demoRequest = await SeedDemoRequestAsync();

        var firstAttempt = async () => await _provisioningService.ProvisionAsync(demoRequest.Id, EmptyRequest(), ActorUserId);
        await firstAttempt.Should().ThrowAsync<DemoRequestInviteFailedException>();

        var firstAttemptOrganizationId = (await _databaseContext.DemoRequests.FindAsync(demoRequest.Id))!.OrganizationId;

        _identityOrganizationBootstrapClient
            .BootstrapAdministratorAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new IdentityBootstrapAdminResult(Guid.NewGuid(), "admin@example.com", DateTime.UtcNow.AddDays(7)));

        var result = await _provisioningService.ProvisionAsync(demoRequest.Id, EmptyRequest(), ActorUserId);

        result.Should().NotBeNull();
        result!.Organization.Id.Should().Be(firstAttemptOrganizationId!.Value);
        result.ProvisioningState.Should().Be(nameof(DemoRequestProvisioningState.AdminInvited));
        (await _databaseContext.Organizations.CountAsync()).Should().Be(1);

        await _eventPublisher.Received(1).PublishAsync(
            Topics.OrganizationCreated,
            Arg.Any<string>(),
            Topics.OrganizationCreated,
            Arg.Any<OrganizationCreatedEvent>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProvisionAsync_a_second_call_after_success_returns_already_provisioned_without_calling_identity_again()
    {
        var demoRequest = await SeedDemoRequestAsync();
        var firstResult = await _provisioningService.ProvisionAsync(demoRequest.Id, EmptyRequest(), ActorUserId);
        _identityOrganizationBootstrapClient.ClearReceivedCalls();

        var secondResult = await _provisioningService.ProvisionAsync(demoRequest.Id, EmptyRequest(), ActorUserId);

        secondResult.Should().NotBeNull();
        secondResult!.AlreadyProvisioned.Should().BeTrue();
        secondResult.Organization.Id.Should().Be(firstResult!.Organization.Id);
        secondResult.InviteId.Should().Be(firstResult.InviteId);
        secondResult.InviteExpiresAt.Should().BeNull();

        (await _databaseContext.Organizations.CountAsync()).Should().Be(1);
        await _identityOrganizationBootstrapClient.DidNotReceiveWithAnyArgs().BootstrapAdministratorAsync(
            default, default!, default!, default!, default, default, default);
    }

    [Test]
    public async Task ProvisionAsync_reports_the_invite_identity_service_says_is_already_pending()
    {
        var existingInviteId = Guid.NewGuid();
        _identityOrganizationBootstrapClient
            .BootstrapAdministratorAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new IdentityBootstrapAdminResult(existingInviteId, "already-invited@example.com", DateTime.UtcNow.AddDays(3)));

        var demoRequest = await SeedDemoRequestAsync();

        var result = await _provisioningService.ProvisionAsync(demoRequest.Id, EmptyRequest(), ActorUserId);

        result.Should().NotBeNull();
        result!.InviteId.Should().Be(existingInviteId);
        result.InviteEmail.Should().Be("already-invited@example.com");
    }

    /// <summary>
    /// Structural guarantee behind "provisioning never sends the plain-approval «Заявку одобрили»
    /// email": <see cref="DemoRequestProvisioningService"/> takes no dependency capable of sending
    /// one. Routing provisioning through <see cref="IDemoRequestService.UpdateStatusAsync"/> instead
    /// would have reintroduced exactly that email — see the class summary on
    /// <see cref="IDemoRequestProvisioningService"/>.
    /// </summary>
    [Test]
    public void DemoRequestProvisioningService_has_no_dependency_capable_of_sending_the_plain_approval_email()
    {
        var constructorParameterTypes = typeof(DemoRequestProvisioningService)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToList();

        constructorParameterTypes.Should().NotContain(typeof(IEmailSender));
        constructorParameterTypes.Should().NotContain(typeof(IDemoRequestNotificationComposer));
    }
}
