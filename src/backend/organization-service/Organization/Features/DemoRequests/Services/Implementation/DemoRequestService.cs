using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Email.Abstract;
using Sellevate.Organization.Features.DemoRequests.Exceptions;
using Sellevate.Organization.Features.DemoRequests.Models;
using Sellevate.Organization.Features.DemoRequests.Services.Abstract;
using Sellevate.Organization.Infrastructure.Configuration;
using Sellevate.Organization.Infrastructure.Data;
using DemoRequestEntity = Sellevate.Organization.Features.DemoRequests.Models.DemoRequest;

namespace Sellevate.Organization.Features.DemoRequests.Services.Implementation;

/// <summary>
/// Accepts public "Request a demo" submissions and serves the platform-staff list/status-update
/// surface over them.
///
/// <para>
/// <b>The honeypot never fails visibly.</b> <see cref="CreateDemoRequestRequestDto.Website"/> is a
/// hidden field no human fills in; when a submission arrives with it non-empty, nothing is persisted
/// and no email is sent, but the response is still a freshly minted
/// <see cref="DemoRequestAcceptedDto"/> indistinguishable from a real submission — telling the caller
/// apart from a genuine lead would tell whatever filled the field in that it was detected, which is
/// exactly the information a honeypot exists to withhold.
/// </para>
///
/// <para>
/// <b>Cooldown is per normalized work email</b>, modeled on identity-service's
/// <c>EmailVerificationService</c>: a second submission from the same address inside
/// <see cref="DemoRequestConfiguration.SubmissionCooldownSeconds"/> throws
/// <see cref="DemoRequestCooldownException"/> naming the whole seconds remaining, rather than being
/// silently accepted or silently dropped.
/// </para>
///
/// <para>
/// <b>Notification failure never fails the request.</b> The lead is persisted before the email is
/// attempted, and an exception from <see cref="IEmailSender"/> is caught and logged, not rethrown — a
/// sales inbox outage must not turn into a failed submission for a visitor filling out a form. The
/// notification also never reaches the submitter's own address: see
/// <see cref="Abstract.IDemoRequestNotificationComposer"/>.
/// </para>
/// </summary>
internal sealed class DemoRequestService(
    OrganizationDbContext databaseContext,
    IEmailSender emailSender,
    IDemoRequestNotificationComposer notificationComposer,
    IOptions<DemoRequestConfiguration> demoRequestOptions,
    ILogger<DemoRequestService> logger) : IDemoRequestService
{
    public async Task<DemoRequestAcceptedDto> SubmitAsync(
        CreateDemoRequestRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedWorkEmail = request.WorkEmail.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(request.Website))
        {
            logger.LogWarning(
                "Demo request honeypot triggered for {WorkEmail}; discarding without persisting",
                normalizedWorkEmail);
            return new DemoRequestAcceptedDto(Guid.NewGuid(), DateTime.UtcNow);
        }

        var configuration = demoRequestOptions.Value;

        var mostRecentSubmission = await databaseContext.DemoRequests
            .Where(demoRequest => demoRequest.WorkEmail == normalizedWorkEmail)
            .OrderByDescending(demoRequest => demoRequest.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (mostRecentSubmission is not null)
        {
            var secondsSinceLastSubmission = (DateTime.UtcNow - mostRecentSubmission.CreatedAt).TotalSeconds;
            if (secondsSinceLastSubmission < configuration.SubmissionCooldownSeconds)
            {
                var retryAfterSeconds = (int)Math.Ceiling(
                    configuration.SubmissionCooldownSeconds - secondsSinceLastSubmission);
                logger.LogWarning(
                    "Demo request submitted during cooldown for {WorkEmail}; {RetryAfterSeconds}s remaining",
                    normalizedWorkEmail,
                    retryAfterSeconds);
                throw new DemoRequestCooldownException(retryAfterSeconds);
            }
        }

        var now = DateTime.UtcNow;
        var demoRequest = new DemoRequestEntity
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            WorkEmail = normalizedWorkEmail,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            CompanyName = request.CompanyName.Trim(),
            JobTitle = string.IsNullOrWhiteSpace(request.JobTitle) ? null : request.JobTitle.Trim(),
            SalesTeamSize = request.SalesTeamSize!.Value,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            Status = DemoRequestStatus.New,
            ConsentGivenAt = now,
            MarketingConsentGivenAt = request.MarketingConsentGiven ? now : null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        databaseContext.DemoRequests.Add(demoRequest);
        await databaseContext.SaveChangesAsync(cancellationToken);

        await SendNotificationAsync(demoRequest, configuration, cancellationToken);

        return new DemoRequestAcceptedDto(demoRequest.Id, demoRequest.CreatedAt);
    }

    public async Task<IReadOnlyList<DemoRequestDto>> ListDemoRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        var demoRequests = await databaseContext.DemoRequests
            .AsNoTracking()
            .OrderByDescending(demoRequest => demoRequest.CreatedAt)
            .ToListAsync(cancellationToken);

        return demoRequests.Select(ToDto).ToList();
    }

    public async Task<DemoRequestDto?> UpdateStatusAsync(
        Guid demoRequestId, DemoRequestStatus status, CancellationToken cancellationToken = default)
    {
        var demoRequest = await databaseContext.DemoRequests
            .FirstOrDefaultAsync(candidate => candidate.Id == demoRequestId, cancellationToken);

        if (demoRequest is null)
        {
            return null;
        }

        demoRequest.Status = status;
        demoRequest.UpdatedAt = DateTime.UtcNow;
        await databaseContext.SaveChangesAsync(cancellationToken);

        return ToDto(demoRequest);
    }

    private async Task SendNotificationAsync(
        DemoRequestEntity demoRequest, DemoRequestConfiguration configuration, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration.NotificationEmail))
        {
            logger.LogWarning(
                "No demo request notification email configured; skipping notification for {DemoRequestId}",
                demoRequest.Id);
            return;
        }

        try
        {
            var message = notificationComposer.Compose(
                demoRequest, configuration.NotificationEmail, configuration.NotificationRecipientName);
            await emailSender.SendEmailAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to send demo request notification for {DemoRequestId}",
                demoRequest.Id);
        }
    }

    private static DemoRequestDto ToDto(DemoRequestEntity demoRequest) => new(
        demoRequest.Id,
        demoRequest.FullName,
        demoRequest.WorkEmail,
        demoRequest.Phone,
        demoRequest.CompanyName,
        demoRequest.JobTitle,
        demoRequest.SalesTeamSize.ToString(),
        demoRequest.Comment,
        demoRequest.Status.ToString(),
        demoRequest.ConsentGivenAt,
        demoRequest.MarketingConsentGivenAt,
        demoRequest.CreatedAt,
        demoRequest.UpdatedAt);
}
