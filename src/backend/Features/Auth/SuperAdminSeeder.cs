using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Auth;

public class SuperAdminSeeder(AppDbContext databaseContext, IConfiguration configuration)
{
    public async Task SeedAsync()
    {
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
            existingUser.Role = UserRole.SuperAdmin;
            await databaseContext.SaveChangesAsync();
            return;
        }

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
    }
}
