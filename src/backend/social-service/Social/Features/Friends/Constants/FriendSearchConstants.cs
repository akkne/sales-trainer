namespace Sellevate.Social.Features.Friends.Constants;

/// <summary>
/// The two bounds on user search, both documented in docs/FRIENDS.md as product rules rather than
/// implementation detail.
///
/// <para>
/// <see cref="MinimumQueryLength"/> is the more interesting one: the directory this search reads is
/// platform-global (see <c>FriendService</c>), so a one-character query would enumerate a large slice
/// of it. Refusing a short query keeps search a lookup rather than a listing, and
/// <see cref="MaximumResultCount"/> caps what a longer one can pull back.
/// </para>
/// </summary>
internal static class FriendSearchConstants
{
    public const int MaximumResultCount = 20;
    public const int MinimumQueryLength = 2;
}
