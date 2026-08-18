using Sellevate.Learning.Features.DialogReviews.Services.Abstract;
using Sellevate.Learning.Features.DialogReviews.Services.Implementation;

namespace Sellevate.Learning.Features.DialogReviews;

public static class DialogReviewServiceCollectionExtensions
{
    public static IServiceCollection AddDialogReviewFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IDialogReviewService, DialogReviewService>();

        return services;
    }
}
