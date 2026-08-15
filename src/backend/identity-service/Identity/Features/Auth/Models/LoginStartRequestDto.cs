using System.ComponentModel.DataAnnotations;

namespace Sellevate.Identity.Features.Auth.Models;

/// <summary>Step 1 of the three-step login flow: the address, and nothing else.</summary>
public sealed record LoginStartRequestDto([Required, EmailAddress] string Email);
