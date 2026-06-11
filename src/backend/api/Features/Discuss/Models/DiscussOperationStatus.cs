namespace SalesTrainer.Api.Features.Discuss.Models;

/// <summary>Result status for service operations that can fail in several ways.</summary>
public enum DiscussOperationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict
}
