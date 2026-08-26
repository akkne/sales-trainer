using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.Identity.Features.Auth.Exceptions;
using Sellevate.Identity.Infrastructure;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// The handler decides what a caller is told went wrong, so the split between 4xx and 5xx is a
/// contract in its own right: a 4xx tells the browser the user's input was at fault and echoes the
/// exception message back to them, while a 5xx says the service failed and withholds the detail.
///
/// The case that forces these tests is the 2026-08-26 login outage. The handler mapped every
/// <see cref="InvalidOperationException"/> to 400, and that is the type Entity Framework raises when
/// the database is unreachable — so with Postgres down, <c>POST /auth/login/start</c> answered
/// "400 Bad request: An exception has been raised that is likely due to a transient failure". The
/// front end read that as rejected input and showed a login error instead of an outage, and the
/// internal failure text was echoed to the caller on the way. Only exception types this service
/// defines may be mapped below 500.
/// </summary>
[TestFixture]
public sealed class GlobalExceptionHandlerStatusMappingTests
{
    [Test]
    public async Task Transient_database_failure_is_reported_as_a_server_error_not_a_bad_request()
    {
        // The exact shape Entity Framework's retrying execution strategy throws when it cannot reach
        // Postgres — the wrapper that used to be caught by the blanket InvalidOperationException arm.
        var transientDatabaseFailure = new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure.");

        var (statusCode, problemDetails) = await HandleAsync(transientDatabaseFailure);

        statusCode.Should().Be(StatusCodes.Status500InternalServerError);
        problemDetails.GetProperty("title").GetString().Should().Be("An unexpected error occurred");
    }

    [Test]
    public async Task Server_errors_do_not_echo_the_internal_failure_text_to_the_caller()
    {
        var transientDatabaseFailure = new InvalidOperationException(
            "Failed to connect to 127.0.0.1:5433");

        var (_, problemDetails) = await HandleAsync(transientDatabaseFailure);

        problemDetails.TryGetProperty("detail", out var detail).Should().BeFalse(
            "a 500 can carry connection strings and internal host names, which belong in the log");
        detail.ValueKind.Should().Be(JsonValueKind.Undefined);
    }

    [Test]
    public async Task Duplicate_registration_is_still_a_bad_request_that_names_the_reason()
    {
        var duplicateRegistration = new EmailAlreadyRegisteredException("taken@test.com");

        var (statusCode, problemDetails) = await HandleAsync(duplicateRegistration);

        statusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.GetProperty("title").GetString().Should().Be("Bad request");
        problemDetails.GetProperty("detail").GetString().Should().Be("Email already registered.",
            "sign-up is the one flow that has to admit the address is taken");
    }

    [Test]
    public async Task Domain_exceptions_keep_the_status_codes_the_front_end_branches_on()
    {
        (await HandleAsync(new UnauthorizedAccessException()))
            .StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        (await HandleAsync(new EmailNotVerifiedException("unproven@test.com")))
            .StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await HandleAsync(new OrganizationSuspendedException(Guid.NewGuid())))
            .StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await HandleAsync(new KeyNotFoundException()))
            .StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    private static async Task<(int StatusCode, JsonElement ProblemDetails)> HandleAsync(
        Exception exception)
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();
        using var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        var wasHandled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        wasHandled.Should().BeTrue();
        responseBody.Position = 0;
        using var writtenProblemDetails = await JsonDocument.ParseAsync(responseBody);
        return (httpContext.Response.StatusCode, writtenProblemDetails.RootElement.Clone());
    }
}
