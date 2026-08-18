using System.Security.Claims;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Identity;
using Sellevate.Gateway;

namespace Sellevate.Gateway.Tests;

/// <summary>
/// Pins the anti-spoofing contract of <see cref="IdentityForwarding"/>: whatever identity
/// headers a client sends are always dropped, and only headers derived from the validated
/// principal reach a downstream service.
/// </summary>
[TestFixture]
public class IdentityForwardingTests
{
    private static HttpRequestMessage RequestWithSpoofedHeaders()
    {
        var request = new HttpRequestMessage();
        request.Headers.Add(IdentityHeaders.UserId, "spoofed-by-client");
        request.Headers.Add(IdentityHeaders.UserRole, "SuperAdmin");
        request.Headers.Add(IdentityHeaders.OrganizationId, "spoofed-organization");
        return request;
    }

    [Test]
    public void Authenticated_request_gets_headers_set_from_claims()
    {
        var request = new HttpRequestMessage();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim("sub", "user-42"),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("org_id", "organization-42"),
            },
            authenticationType: "jwt"));

        IdentityForwarding.Apply(request.Headers, user);

        request.Headers.GetValues(IdentityHeaders.UserId).Should().ContainSingle().Which.Should().Be("user-42");
        request.Headers.GetValues(IdentityHeaders.UserRole).Should().ContainSingle().Which.Should().Be("Admin");
        request.Headers.GetValues(IdentityHeaders.OrganizationId).Should().ContainSingle().Which.Should().Be("organization-42");
    }

    [Test]
    public void Client_supplied_identity_headers_are_always_stripped()
    {
        var request = RequestWithSpoofedHeaders();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("sub", "real-user"), new Claim("org_id", "real-organization") },
            authenticationType: "jwt"));

        IdentityForwarding.Apply(request.Headers, user);

        request.Headers.GetValues(IdentityHeaders.UserId).Should().ContainSingle().Which.Should().Be("real-user");
        request.Headers.Contains(IdentityHeaders.UserRole).Should().BeFalse();
        request.Headers.GetValues(IdentityHeaders.OrganizationId).Should().ContainSingle().Which.Should().Be("real-organization");
    }

    [Test]
    public void Client_supplied_organization_header_is_dropped_when_the_token_carries_no_org_id()
    {
        var request = RequestWithSpoofedHeaders();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("sub", "real-user") }, authenticationType: "jwt"));

        IdentityForwarding.Apply(request.Headers, user);

        request.Headers.Contains(IdentityHeaders.OrganizationId).Should().BeFalse();
    }

    [Test]
    public void Anonymous_request_carries_no_identity_headers()
    {
        var request = RequestWithSpoofedHeaders();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        IdentityForwarding.Apply(request.Headers, anonymous);

        request.Headers.Contains(IdentityHeaders.UserId).Should().BeFalse();
        request.Headers.Contains(IdentityHeaders.UserRole).Should().BeFalse();
        request.Headers.Contains(IdentityHeaders.OrganizationId).Should().BeFalse();
    }
}
