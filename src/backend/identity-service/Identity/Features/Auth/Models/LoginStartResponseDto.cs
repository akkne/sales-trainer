namespace Sellevate.Identity.Features.Auth.Models;

/// <summary>
/// The answer to step 1 of the login flow: which credential the second step should ask for.
///
/// <para>
/// It carries the method and nothing else — no organization id, no organization name, no "this
/// address is known" flag. Every syntactically valid address gets a <c>200</c>, so an outsider
/// walking a list of addresses learns nothing, exactly the property 40.7 gave the Google endpoint
/// by answering an identical <c>401</c> for "no such account" and "account without a membership".
/// </para>
/// </summary>
public sealed record LoginStartResponseDto(string Method);
