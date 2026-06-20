using Microsoft.Extensions.Options;
using Sellevate.Identity.Features.Avatars.Services.Abstract;
using Sellevate.Identity.Features.Avatars.Services.Implementation;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Storage.Abstract;
using Sellevate.Identity.Infrastructure.Storage.Implementation;

namespace Sellevate.Identity.Features.Avatars;

public static class AvatarsServiceCollectionExtensions
{
    public static IServiceCollection AddAvatarStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<S3Configuration>(
            configuration.GetSection(S3Configuration.SectionName));

        services.AddSingleton<IObjectStorage>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<S3Configuration>>().Value;
            return new S3ObjectStorage(config);
        });

        services.AddScoped<IAvatarService, AvatarService>();
        services.AddScoped<DefaultAvatarSeeder>();

        return services;
    }
}
