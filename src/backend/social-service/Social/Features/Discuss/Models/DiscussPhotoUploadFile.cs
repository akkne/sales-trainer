namespace Sellevate.Social.Features.Discuss.Models;

public sealed record DiscussPhotoUploadFile(Stream Content, string FileName, long Length);
