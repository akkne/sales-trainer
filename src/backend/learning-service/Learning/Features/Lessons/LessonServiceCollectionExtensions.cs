using Sellevate.Learning.Features.Lessons.Services.Abstract;
using Sellevate.Learning.Features.Lessons.Services.Implementation;

namespace Sellevate.Learning.Features.Lessons;

public static class LessonServiceCollectionExtensions
{
    public static IServiceCollection AddLessonFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<ILessonVersionService, LessonVersionService>();
        services.AddScoped<ILessonVersionBackfill, LessonVersionBackfill>();
        services.AddScoped<ILessonAccuracyService, LessonAccuracyService>();

        return services;
    }
}
