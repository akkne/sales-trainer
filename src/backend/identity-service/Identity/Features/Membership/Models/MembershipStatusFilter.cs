namespace Sellevate.Identity.Features.Membership.Models;

/// <summary>
/// What <c>GET /memberships?status=</c> asks for. Separate from <see cref="MembershipStatus"/>
/// because a filter has a third case the state does not: <see cref="All"/>.
///
/// <para>
/// The default is <see cref="Active"/> rather than <see cref="All"/>: the roster screen is a list of
/// people to assign work to, and someone who was offboarded last year is not one of them. Their row
/// is kept forever (deactivation is not deletion), which is exactly why it has to be asked for.
/// </para>
/// </summary>
public enum MembershipStatusFilter
{
    Active = 0,
    Deactivated = 1,
    All = 2
}
