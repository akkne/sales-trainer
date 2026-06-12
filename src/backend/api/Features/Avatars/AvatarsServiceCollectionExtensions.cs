using Microsoft.Extensions.Options;
using SalesTrainer.Api.Features.Avatars.Services.Abstract;
using SalesTrainer.Api.Features.Avatars.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Configuration;
using SalesTrainer.Api.Infrastructure.Storage.Abstract;
using SalesTrainer.Api.Infrastructure.Storage.Implementation;

namespace SalesTrainer.Api.Features.Avatars;

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
