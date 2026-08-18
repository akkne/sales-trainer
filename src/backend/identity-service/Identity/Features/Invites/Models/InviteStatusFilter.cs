namespace Sellevate.Identity.Features.Invites.Models;

/// <summary>
/// What <c>GET /invites?status=</c> asks for. The default is <see cref="Pending"/>: the screen this
/// serves answers "who have we invited who has not arrived yet", and an accepted invite is a
/// membership now, visible on the roster instead.
/// </summary>
public enum InviteStatusFilter
{
    Pending = 0,
    All = 1
}
