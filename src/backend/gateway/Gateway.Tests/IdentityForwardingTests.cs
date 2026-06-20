using System.Net.Http;
using System.Security.Claims;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Identity;
using Sellevate.Gateway;

namespace Sellevate.Gateway.Tests;

[TestFixture]
public class IdentityForwardingTests
{
    private static HttpRequestMessage RequestWithSpoofedHeaders()
    {
        var request = new HttpRequestMessage();
        request.Headers.Add(IdentityHeaders.UserId, "spoofed-by-client");
        request.Headers.Add(IdentityHeaders.UserRole, "SuperAdmin");
        return request;
    }

    [Test]
    public void Authenticated_request_gets_headers_set_from_claims()
    {
        var request = new HttpRequestMessage();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("sub", "user-42"), new Claim(ClaimTypes.Role, "Admin") },
            authenticationType: "jwt"));

        IdentityForwarding.Apply(request.Headers, user);

        request.Headers.GetValues(IdentityHeaders.UserId).Should().ContainSingle().Which.Should().Be("user-42");
        request.Headers.GetValues(IdentityHeaders.UserRole).Should().ContainSingle().Which.Should().Be("Admin");
    }

    [Test]
    public void Client_supplied_identity_headers_are_always_stripped()
    {
        var request = RequestWithSpoofedHeaders();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("sub", "real-user") }, authenticationType: "jwt"));

        IdentityForwarding.Apply(request.Headers, user);

        // The spoofed values must be gone; only the validated identity remains.
        request.Headers.GetValues(IdentityHeaders.UserId).Should().ContainSingle().Which.Should().Be("real-user");
        request.Headers.Contains(IdentityHeaders.UserRole).Should().BeFalse();
    }

    [Test]
    public void Anonymous_request_carries_no_identity_headers()
    {
        var request = RequestWithSpoofedHeaders();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        IdentityForwarding.Apply(request.Headers, anonymous);

        request.Headers.Contains(IdentityHeaders.UserId).Should().BeFalse();
        request.Headers.Contains(IdentityHeaders.UserRole).Should().BeFalse();
    }
}
