namespace Sellevate.Ai.Common.Constants;

/// <summary>
/// Status codes this service emits that <see cref="StatusCodes"/> does not name, so a reader is never
/// left guessing what a bare number on a return statement means.
/// </summary>
public static class NonStandardHttpStatusCodes
{
    public const int ClientClosedRequest = 499;
}
