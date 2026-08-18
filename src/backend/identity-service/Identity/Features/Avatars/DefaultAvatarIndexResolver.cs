namespace Sellevate.Identity.Features.Avatars;

/// <summary>
/// Picks which stock avatar a new user gets, deterministically from their id: the same user always
/// resolves to the same picture, with no counter to keep and nothing to store. Derived from the first
/// four bytes of the identifier, which is enough spread for a catalog of a handful of images.
/// </summary>
public static class DefaultAvatarIndexResolver
{
    public static int Resolve(Guid userId, int catalogSize)
    {
        if (catalogSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(catalogSize), "catalogSize must be greater than zero.");
        }

        var bytes = userId.ToByteArray();
        var value = (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
        return (int)(value % (uint)catalogSize);
    }
}
