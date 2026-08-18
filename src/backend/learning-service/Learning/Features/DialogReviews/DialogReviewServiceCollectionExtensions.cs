using Sellevate.Learning.Features.DialogReviews.Services.Abstract;
using Sellevate.Learning.Features.DialogReviews.Services.Implementation;

namespace Sellevate.Learning.Features.DialogReviews;

/// <summary>
/// Registers the dialog-review feedback loop. <c>Scoped</c> because it reads and writes a tenant-scoped
/// <c>LearningDbContext</c> (CODESTYLE §4).
/// </summary>
public static class DialogReviewServiceCollectionExtensions
{
    public static IServiceCollection AddDialogReviewFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IDialogReviewService, DialogReviewService>();

        return services;
    }
}
