using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Infrastructure.Configuration;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Auth;

public sealed class SuperAdminSeeder(
    AppDbContext databaseContext,
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

        databaseContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            Role = UserRole.SuperAdmin
        });

        await databaseContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("SuperAdmin user created successfully {Email}", normalizedEmail);
    }
}
