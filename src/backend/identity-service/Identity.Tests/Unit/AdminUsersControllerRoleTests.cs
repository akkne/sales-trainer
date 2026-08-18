using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Identity.Features.Admin;
using Sellevate.Identity.Features.Admin.Models;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Avatars.Services.Abstract;
using Sellevate.Identity.Features.Profile.Services.Abstract;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

[TestFixture]
public class AdminUsersControllerRoleTests
{
    /// <summary>
    /// Builds the controller over an in-memory context, with a stand-in <c>ClaimsPrincipal</c> so the
    /// audit-logging calls to <c>User.FindFirstValue</c> have something to read.
    /// </summary>
    private static AdminUsersController BuildController(out Sellevate.Identity.Infrastructure.Data.IdentityDbContext databaseContext)
    {
        databaseContext = InMemoryDbContextFactory.Create();
        var controller = new AdminUsersController(
            databaseContext,
            Substitute.For<IAvatarService>(),
            Substitute.For<IProfileService>(),
            NullLogger<AdminUsersController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                    "test"))
            }
        };
        return controller;
    }

    [Test]
    public async Task ChangeRole_DemoteLastSuperAdmin_ReturnsConflict()
    {
        var controller = BuildController(out var databaseContext);

        var superAdmin = new User
        {
            Id = Guid.NewGuid(),
            Email = "super@test.com",
            DisplayName = "Super Admin",
            Role = UserRole.SuperAdmin,
            CreatedAt = DateTime.UtcNow
        };
        databaseContext.Users.Add(superAdmin);
        await databaseContext.SaveChangesAsync();

        var result = await controller.ChangeRole(
            superAdmin.Id,
            new ChangeUserRoleRequestDto("User"),
            CancellationToken.None);

        result.Result.Should().BeOfType<ConflictObjectResult>(
            "demoting the last SuperAdmin must be blocked with 409 Conflict");
    }

    [Test]
    public async Task ChangeRole_DemoteOneSuperAdminWhenMultipleExist_Succeeds()
    {
        var controller = BuildController(out var databaseContext);

        var superAdmin1 = new User
        {
            Id = Guid.NewGuid(),
            Email = "super1@test.com",
            DisplayName = "Super 1",
            Role = UserRole.SuperAdmin,
            CreatedAt = DateTime.UtcNow
        };
        var superAdmin2 = new User
        {
            Id = Guid.NewGuid(),
            Email = "super2@test.com",
            DisplayName = "Super 2",
            Role = UserRole.SuperAdmin,
            CreatedAt = DateTime.UtcNow
        };
        databaseContext.Users.AddRange(superAdmin1, superAdmin2);
        await databaseContext.SaveChangesAsync();

        var result = await controller.ChangeRole(
            superAdmin1.Id,
            new ChangeUserRoleRequestDto("User"),
            CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>(
            "demoting a SuperAdmin is allowed when at least one other SuperAdmin remains");
    }

    [Test]
    public async Task ChangeRole_PromoteUserToSuperAdmin_AlwaysSucceeds()
    {
        var controller = BuildController(out var databaseContext);

        var regularUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            DisplayName = "Regular User",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
        databaseContext.Users.Add(regularUser);
        await databaseContext.SaveChangesAsync();

        var result = await controller.ChangeRole(
            regularUser.Id,
            new ChangeUserRoleRequestDto("SuperAdmin"),
            CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>("promotion never needs a last-SuperAdmin guard");
    }
}
