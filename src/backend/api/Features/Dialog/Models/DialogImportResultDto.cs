namespace SalesTrainer.Api.Features.Dialog.Models;

/// <summary>
/// Result of a dialog bundle import (bundles with nested modes in one file).
/// Idempotent upsert: bundles by (SkillId, Title), modes by (BundleId, Key).
/// </summary>
public record DialogImportResultDto(
    int BundlesCreated,
    int BundlesUpdated,
    int ModesCreated,
    int ModesUpdated,
    List<string> Errors);
