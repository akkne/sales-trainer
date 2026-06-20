namespace Sellevate.Learning.Infrastructure.Ai;

internal sealed record ChatReplyParseResult(string Reply, bool EndCall, bool UsedFallback);
