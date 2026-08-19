using System.ComponentModel.DataAnnotations;

namespace Sellevate.Organization.Features.DemoRequests.Models;

public sealed record UpdateDemoRequestStatusRequestDto([Required] DemoRequestStatus Status);
