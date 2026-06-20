namespace Sellevate.Ai.Features.Dialog.Services.Implementation;

internal sealed record ChatReplyParseResult(string Reply, bool EndCall, bool UsedFallback);
