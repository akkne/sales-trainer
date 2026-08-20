using Microsoft.Extensions.DependencyInjection;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Data;
using MembershipEntity = Sellevate.Identity.Features.Membership.Models.Membership;

namespace Sellevate.Identity.Tests.Helpers;

/// <summary>
/// Seeds users straight into the database. Phase 40.7 deleted <c>POST /auth/register</c>, so there
/// is no HTTP route that creates an account any more — every test that needs an existing user
/// either seeds it here or walks the invite acceptance flow, which is the product's only remaining
/// way in.
/// </summary>
public static class TestUserSeeder
{
    public const string DefaultPassword = "Password123!";

    public static async Task<User> SeedUserAsync(
        TestWebApplicationFactory factory,
        string email,
        string displayName = "Test User",
        string password = DefaultPassword,
        Guid? organizationId = null,
        OrgRole organizationRole = OrgRole.Manager,
        MembershipStatus membershipStatus = MembershipStatus.Active,
        UserRole platformRole = UserRole.User)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true,
            Role = platformRole
        };

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        database.Users.Add(user);

        if (organizationId is { } targetOrganizationId)
        {
            database.Memberships.Add(new MembershipEntity
            {
                UserId = user.Id,
                OrganizationId = targetOrganizationId,
                Role = organizationRole,
                Status = membershipStatus,
                JoinedAt = DateTime.UtcNow,
                DeactivatedAt = membershipStatus == MembershipStatus.Deactivated ? DateTime.UtcNow : null
            });
        }

        await database.SaveChangesAsync();
        return user;
    }
}
