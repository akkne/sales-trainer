using Microsoft.Extensions.Options;
using Sellevate.Identity.Features.Avatars.Services.Abstract;
using Sellevate.Identity.Features.Avatars.Services.Implementation;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Storage.Abstract;
using Sellevate.Identity.Infrastructure.Storage.Implementation;

namespace Sellevate.Identity.Features.Avatars;

/// <summary>
/// Registers avatar storage. <see cref="IObjectStorage"/> is a singleton built from a configuration
/// snapshot: the S3 client is thread-safe and pools connections, so a per-request instance would only
/// churn sockets. It follows that changing <c>Storage:S3</c> needs a restart.
/// </summary>
public static class AvatarsServiceCollectionExtensions
{
    public static IServiceCollection AddAvatarStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<S3Configuration>(
            configuration.GetSection(S3Configuration.SectionName));

        services.AddSingleton<IObjectStorage>(serviceProvider =>
        {
            var s3Configuration = serviceProvider.GetRequiredService<IOptions<S3Configuration>>().Value;
            return new S3ObjectStorage(s3Configuration);
        });

        services.AddScoped<IAvatarService, AvatarService>();
        services.AddScoped<DefaultAvatarSeeder>();

        return services;
    }
}
