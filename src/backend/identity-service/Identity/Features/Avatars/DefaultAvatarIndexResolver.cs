namespace Sellevate.Identity.Features.Avatars;

public static class DefaultAvatarIndexResolver
{
    /// <summary>
    /// Derives a stable, non-negative avatar index from a user's Guid.
    /// Uses the first 4 bytes of the Guid as a uint so the result is
    /// always non-negative and deterministic regardless of platform.
    /// </summary>
    public static int Resolve(Guid userId, int catalogSize)
    {
        if (catalogSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(catalogSize), "catalogSize must be greater than zero.");

        var bytes = userId.ToByteArray();
        var value = (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
        return (int)(value % (uint)catalogSize);
    }
}
