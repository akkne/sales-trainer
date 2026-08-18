using System.Net.Http.Json;
using System.Text.Json;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>
/// The request/response half every ai-service client shares: build the absolute URI, POST the
/// payload as web-cased JSON, stamp the tenancy headers, and turn a non-success status or an empty
/// body into an <see cref="InvalidOperationException"/> the controller maps to 503.
///
/// <para>
/// The four clients differ only in their configured path, their log wording and their result type,
/// so everything else lived four times over. The part worth centralising is the failure path: a
/// client that forgot to check <c>IsSuccessStatusCode</c> would deserialize an error body into a
/// default-valued result and hand it to the user as a real answer.
/// </para>
///
/// <para>
/// Log and exception wording stays with each client rather than being generated here, because the
/// message templates are what operators search on.
/// </para>
/// </summary>
internal static class AiServiceCall
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Joins a configured base URL and path into an absolute URI, tolerating a base URL that ends
    /// in a slash — the usual shape of a hand-edited environment override.
    /// </summary>
    public static string BuildRequestUri(string baseUrl, string path)
        => baseUrl.TrimEnd('/') + path;

    /// <summary>
    /// POSTs <paramref name="payload"/> to <paramref name="requestUri"/>. The caller owns the
    /// returned response and must dispose it. The response content is fully buffered before this
    /// returns (default completion option), so the request message is safe to dispose here.
    /// </summary>
    public static async Task<HttpResponseMessage> PostAsync<TPayload>(
        HttpClient httpClient,
        ITenantContext tenantContext,
        string requestUri,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload, options: SerializerOptions),
        };

        AiCallHeaders.Apply(httpRequest, tenantContext);

        return await httpClient.SendAsync(httpRequest, cancellationToken);
    }

    /// <summary>
    /// Reads a successful response body as <typeparamref name="TResult"/>. Throws
    /// <see cref="InvalidOperationException"/> on a non-success status (logging the status and body
    /// through <paramref name="failureLogTemplate"/>, which must take a status and a body) or on a
    /// body that deserializes to null.
    /// </summary>
    public static async Task<TResult> ReadResultAsync<TResult>(
        HttpResponseMessage response,
        ILogger logger,
        string failureLogTemplate,
        string serviceLabel,
        CancellationToken cancellationToken)
        where TResult : class
    {
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(failureLogTemplate, response.StatusCode, responseBody);
            throw new InvalidOperationException($"{serviceLabel} returned {(int)response.StatusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<TResult>(SerializerOptions, cancellationToken);

        if (result is null)
            throw new InvalidOperationException($"{serviceLabel} returned an empty body.");

        return result;
    }
}
