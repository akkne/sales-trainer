namespace Sellevate.Identity.Features.Avatars;

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
