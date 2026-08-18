namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Base type for every expected failure of an upstream LLM call (rejected request, quota,
/// auth, unusable response). Controllers catch this base so a new provider failure mode can
/// never slip through as an unhandled 500 just because nobody added a catch clause for it.
/// </summary>
public abstract class OpenAiException(string message) : Exception(message);
