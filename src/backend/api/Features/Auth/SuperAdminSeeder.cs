using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Auth;

public class SuperAdminSeeder(
    AppDbContext databaseContext,
    IConfiguration configuration,
    ILogger<SuperAdminSeeder> logger)
{
    public async Task SeedAsync()
    {
        logger.LogInformation("SuperAdmin seeding started");

        var email = configuration["SuperAdmin:Email"]
            ?? throw new InvalidOperationException("SuperAdmin:Email is not configured.");
        var password = configuration["SuperAdmin:Password"]
            ?? throw new InvalidOperationException("SuperAdmin:Password is not configured.");
        var displayName = configuration["SuperAdmin:DisplayName"]
            ?? throw new InvalidOperationException("SuperAdmin:DisplayName is not configured.");

        var normalizedEmail = email.ToLowerInvariant();

        var existingUser = await databaseContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

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
            await databaseContext.SaveChangesAsync();
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

        await databaseContext.SaveChangesAsync();
        logger.LogInformation("SuperAdmin user created successfully {Email}", normalizedEmail);
    }
}
