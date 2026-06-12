namespace SalesTrainer.Api.Features.Discuss.Models;

public sealed record DiscussPhotoUploadFile(Stream Content, string FileName, long Length);
