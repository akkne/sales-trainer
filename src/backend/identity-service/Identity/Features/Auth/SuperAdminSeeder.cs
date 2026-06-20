using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Auth;

public sealed class SuperAdminSeeder(
    IdentityDbContext databaseContext,
    IUserEventPublisher userEventPublisher,
    IOptions<SuperAdminConfiguration> superAdminOptions,
    ILogger<SuperAdminSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("SuperAdmin seeding started");

        var email = superAdminOptions.Value.Email
            ?? throw new InvalidOperationException("SuperAdmin:Email is not configured.");
        var password = superAdminOptions.Value.Password
            ?? throw new InvalidOperationException("SuperAdmin:Password is not configured.");
        var displayName = superAdminOptions.Value.DisplayName
            ?? throw new InvalidOperationException("SuperAdmin:DisplayName is not configured.");

        var normalizedEmail = email.ToLowerInvariant();

        var existingUser = await databaseContext.Users
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            if (existingUser.Role == UserRole.SuperAdmin)
            {
                logger.LogInformation("SuperAdmin already exists and has correct role {Email}", normalizedEmail);
                return;
            }

            logger.LogInformation("Promoting existing user to SuperAdmin {Email} (was {OldRole})",
                normalizedEmail, existingUser.Role);
            existingUser.Role = UserRole.SuperAdmin;
            await databaseContext.SaveChangesAsync(cancellationToken);
            return;
        }

        logger.LogInformation("Creating new SuperAdmin user {Email} DisplayName={DisplayName}",
            normalizedEmail, displayName);

        var superAdmin = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true,
            Role = UserRole.SuperAdmin
        };
        databaseContext.Users.Add(superAdmin);

        await databaseContext.SaveChangesAsync(cancellationToken);

        // Seed the same user into every other service's replica.
        await userEventPublisher.PublishRegisteredAsync(
            new UserRegisteredEvent(superAdmin.Id, superAdmin.Email, superAdmin.DisplayName, superAdmin.AvatarKey),
            cancellationToken);

        logger.LogInformation("SuperAdmin user created successfully {Email}", normalizedEmail);
    }
}
