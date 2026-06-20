using Sellevate.Social.Infrastructure.Configuration;
using Sellevate.Social.Infrastructure.Storage.Abstract;
using Sellevate.Social.Infrastructure.Storage.Implementation;

namespace Sellevate.Social.Infrastructure.Storage;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddSocialObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<S3Configuration>(configuration.GetSection(S3Configuration.SectionName));
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        return services;
    }
}
