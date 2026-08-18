"use client";

import { use, useState } from "react";
import Link from "next/link";
import { Icon } from "@/shared/components/icon";
import { useAuthStore } from "@/shared/stores/auth-store";
import {
    useOrganizationQuotaSettings,
    useOrganizationSpendReport,
    usePlatformOrganizationDetail,
    useSaveOrganizationQuota,
    type OrganizationSpendReport,
    type OrganizationQuotaSettings,
} from "@/features/admin/hooks/use-organization-quota";
import {
    buildQuotaWriteModel,
    calculateBatchTokenCeiling,
    calculateRemainingTokens,
    describeEffectiveValue,
    describeQuotaEditability,
    describeZeroLimitEffect,
    formatModelCost,
    formatTotalCost,
    formatWholeNumber,
    hasConfiguredLimit,
    resolveQuotaEditability,
    toFormValues,
    MAXIMUM_BATCH_RESERVE_PERCENT,
    QUOTA_STATE_BADGE_CLASSES,
    QUOTA_STATE_DESCRIPTIONS,
    QUOTA_STATE_LABELS,
    type QuotaFormValues,
} from "@/features/admin/lib/organization-quota-format";

const inputClassName =
    "px-3 py-2 text-sm rounded-xl border border-line bg-surface text-ink w-full max-w-xs disabled:opacity-50";

function FieldHint({ children }: { children: React.ReactNode }) {
    return <span className="block text-xs text-ink-4 mt-1">{children}</span>;
}

function ZeroLimitWarning({ rawText }: { rawText: string }) {
    const warning = describeZeroLimitEffect(rawText);
    if (!warning) return null;
    return (
        <span className="block text-xs text-warn mt-1" role="note">
            {warning}
        </span>
    );
}

/**
 * The editable half. Its state is seeded from the loaded row on mount rather than mirrored from it
 * on every change, so a background refetch never overwrites what the operator is halfway through
 * typing.
 */
function QuotaEditorForm({
    settings,
    canSave,
}: {
    settings: OrganizationQuotaSettings;
    canSave: boolean;
}) {
    const saveQuota = useSaveOrganizationQuota();
    const [formValues, setFormValues] = useState<QuotaFormValues>(() => toFormValues(settings));
    const [validationMessage, setValidationMessage] = useState<string | null>(null);
    const [feedback, setFeedback] = useState<string | null>(null);
    const [errorMessage, setErrorMessage] = useState<string | null>(null);

    const updateField = (field: keyof QuotaFormValues, value: string) => {
        setFormValues((current) => ({ ...current, [field]: value }));
        setValidationMessage(null);
        setFeedback(null);
    };

    const submitQuota = async (submitEvent: React.FormEvent) => {
        submitEvent.preventDefault();
        setFeedback(null);
        setErrorMessage(null);

        const result = buildQuotaWriteModel(formValues);
        if (result.status === "invalid") {
            setValidationMessage(result.validationMessage);
            return;
        }
        setValidationMessage(null);

        try {
            await saveQuota.mutateAsync(result.writeModel);
            setFeedback("Quota saved. The next metered call reads the new numbers.");
        } catch (saveError) {
            setErrorMessage((saveError as Error).message);
        }
    };

    return (
        <form onSubmit={submitQuota} className="mb-8">
            <p className="text-sm text-ink mb-4 font-medium">
                An empty field resets that limit to the platform default. It does not remove the limit.
            </p>

            <div className="grid gap-5 sm:grid-cols-2">
                <label className="flex flex-col gap-1 text-xs text-ink-3">
                    Voice minutes per day
                    <input
                        value={formValues.voiceDailyLimitMinutes}
                        onChange={(changeEvent) =>
                            updateField("voiceDailyLimitMinutes", changeEvent.target.value)
                        }
                        disabled={!canSave}
                        inputMode="numeric"
                        placeholder="platform default"
                        className={inputClassName}
                    />
                    <FieldHint>
                        {describeEffectiveValue(
                            settings.voiceDailyLimitMinutes,
                            settings.effectiveVoiceDailyLimitMinutes,
                            "minutes"
                        )}
                    </FieldHint>
                    <ZeroLimitWarning rawText={formValues.voiceDailyLimitMinutes} />
                </label>

                <label className="flex flex-col gap-1 text-xs text-ink-3">
                    Voice minutes per month
                    <input
                        value={formValues.voiceMonthlyLimitMinutes}
                        onChange={(changeEvent) =>
                            updateField("voiceMonthlyLimitMinutes", changeEvent.target.value)
                        }
                        disabled={!canSave}
                        inputMode="numeric"
                        placeholder="platform default"
                        className={inputClassName}
                    />
                    <FieldHint>
                        {describeEffectiveValue(
                            settings.voiceMonthlyLimitMinutes,
                            settings.effectiveVoiceMonthlyLimitMinutes,
                            "minutes"
                        )}
                    </FieldHint>
                    <ZeroLimitWarning rawText={formValues.voiceMonthlyLimitMinutes} />
                </label>

                <label className="flex flex-col gap-1 text-xs text-ink-3">
                    LLM tokens per month
                    <input
                        value={formValues.llmMonthlyTokenLimit}
                        onChange={(changeEvent) =>
                            updateField("llmMonthlyTokenLimit", changeEvent.target.value)
                        }
                        disabled={!canSave}
                        inputMode="numeric"
                        placeholder="platform default"
                        className={inputClassName}
                    />
                    <FieldHint>
                        {describeEffectiveValue(
                            settings.llmMonthlyTokenLimit,
                            settings.effectiveLlmMonthlyTokenLimit,
                            "tokens"
                        )}
                    </FieldHint>
                    <FieldHint>
                        Prompt plus completion, every model together. This is the number that refuses
                        calls.
                    </FieldHint>
                    <ZeroLimitWarning rawText={formValues.llmMonthlyTokenLimit} />
                </label>

                <label className="flex flex-col gap-1 text-xs text-ink-3">
                    Background reserve, % (0–{MAXIMUM_BATCH_RESERVE_PERCENT})
                    <input
                        value={formValues.batchReservePercent}
                        onChange={(changeEvent) =>
                            updateField("batchReservePercent", changeEvent.target.value)
                        }
                        disabled={!canSave}
                        inputMode="numeric"
                        placeholder="platform default"
                        className={inputClassName}
                    />
                    <FieldHint>
                        {describeEffectiveValue(
                            settings.batchReservePercent,
                            settings.effectiveBatchReservePercent,
                            "%"
                        )}
                    </FieldHint>
                    <FieldHint>
                        The share of the token limit background work may not touch. It moves the
                        background ceiling only; interactive work always runs to 100%.
                    </FieldHint>
                </label>

                <label className="flex flex-col gap-1 text-xs text-ink-3 sm:col-span-2">
                    Note
                    <input
                        value={formValues.note}
                        onChange={(changeEvent) => updateField("note", changeEvent.target.value)}
                        disabled={!canSave}
                        placeholder="Which contract this number came from"
                        className={`${inputClassName} max-w-full`}
                    />
                </label>
            </div>

            {validationMessage && (
                <p className="mt-4 text-sm text-bad" role="alert">
                    {validationMessage}
                </p>
            )}
            {errorMessage && (
                <p className="mt-4 text-sm text-bad" role="alert">
                    {errorMessage}
                </p>
            )}
            {feedback && (
                <p className="mt-4 text-sm text-olive" role="status">
                    {feedback}
                </p>
            )}

            <div className="mt-5 flex items-center gap-3">
                <button
                    type="submit"
                    disabled={!canSave || saveQuota.isPending}
                    className="px-4 py-2 text-sm rounded-xl bg-indigo-soft text-indigo-ink font-medium disabled:opacity-50"
                >
                    Save quota
                </button>
                <span className="text-xs text-ink-4">
                    {settings.updatedAt
                        ? `Last written ${new Date(settings.updatedAt).toLocaleString("en-GB")}`
                        : "Never written"}
                </span>
            </div>
        </form>
    );
}

function TwoCeilings({
    settings,
    report,
    isReportSameScope,
}: {
    settings: OrganizationQuotaSettings;
    report: OrganizationSpendReport | undefined;
    isReportSameScope: boolean;
}) {
    const monthlyTokenLimit = settings.effectiveLlmMonthlyTokenLimit;
    const batchCeiling = calculateBatchTokenCeiling(
        monthlyTokenLimit,
        settings.effectiveBatchReservePercent
    );
    const spentTokens = isReportSameScope ? report?.llmTotalTokens : undefined;

    if (!hasConfiguredLimit(monthlyTokenLimit)) {
        return (
            <p className="text-sm text-warn" role="note">
                The monthly token limit is {formatWholeNumber(monthlyTokenLimit)}, and ai-service
                refuses nothing at or below zero. Both ceilings are off: background and interactive
                work run without a token gate.
            </p>
        );
    }

    return (
        <div className="grid gap-3 sm:grid-cols-2">
            <div className="rounded-2xl border border-line bg-surface px-4 py-3">
                <p className="text-xs uppercase tracking-wider text-ink-4">Background work stops at</p>
                <p className="text-lg font-bold text-ink mt-1">{formatWholeNumber(batchCeiling)} tokens</p>
                <p className="text-xs text-ink-3 mt-1">
                    {100 - settings.effectiveBatchReservePercent}% of the limit. Content generation,
                    batch rewrite and AI review.
                </p>
                {spentTokens !== undefined && (
                    <p className="text-xs text-ink-3 mt-2">
                        Remaining: {formatWholeNumber(calculateRemainingTokens(batchCeiling, spentTokens))}{" "}
                        tokens
                    </p>
                )}
            </div>
            <div className="rounded-2xl border border-line bg-surface px-4 py-3">
                <p className="text-xs uppercase tracking-wider text-ink-4">Interactive work stops at</p>
                <p className="text-lg font-bold text-ink mt-1">
                    {formatWholeNumber(monthlyTokenLimit)} tokens
                </p>
                <p className="text-xs text-ink-3 mt-1">
                    100% of the limit. Dialog turns, graded exercises, anything a person is waiting on.
                </p>
                {spentTokens !== undefined && (
                    <p className="text-xs text-ink-3 mt-2">
                        Remaining:{" "}
                        {formatWholeNumber(calculateRemainingTokens(monthlyTokenLimit, spentTokens))} tokens
                    </p>
                )}
            </div>
        </div>
    );
}

function SpendPanel({
    report,
    isLoading,
    isError,
    scopeCaveat,
}: {
    report: OrganizationSpendReport | undefined;
    isLoading: boolean;
    isError: boolean;
    scopeCaveat: string | null;
}) {
    if (isLoading) {
        return <div className="h-40 rounded-2xl bg-surface border border-line animate-pulse" />;
    }

    if (isError || !report) {
        return (
            <div
                className="bg-bad-soft text-bad rounded-xl px-4 py-3 text-sm flex items-center gap-2"
                role="alert"
            >
                <Icon name="warning" size="sm" />
                Couldn&apos;t load this month&apos;s spend. The quota above is still editable.
            </div>
        );
    }

    return (
        <div>
            {scopeCaveat && (
                <p className="text-xs text-warn mb-3" role="note">
                    {scopeCaveat}
                </p>
            )}

            <div className="flex flex-wrap items-center gap-3 mb-3">
                <span className="text-sm text-ink">{report.periodKey}</span>
                <span
                    className={`inline-block px-2 py-0.5 text-xs rounded-full ${QUOTA_STATE_BADGE_CLASSES[report.quotaState] ?? "bg-bg-2 text-ink-3"}`}
                >
                    {QUOTA_STATE_LABELS[report.quotaState] ?? report.quotaState}
                </span>
                <span className="text-xs text-ink-3">
                    {QUOTA_STATE_DESCRIPTIONS[report.quotaState] ?? ""}
                </span>
            </div>

            <p className="text-sm text-ink-3 mb-4">
                {formatWholeNumber(report.llmTotalTokens)} tokens used ·{" "}
                {formatWholeNumber(report.llmCallCount)} model calls ·{" "}
                {formatWholeNumber(report.speechCharacters)} synthesized characters · voice{" "}
                {formatWholeNumber(report.voiceUsedMinutesToday)} min today,{" "}
                {formatWholeNumber(report.voiceUsedMinutesThisMonth)} min this month
            </p>

            {report.models.length === 0 ? (
                <p className="text-sm text-ink-3">No metered calls this month.</p>
            ) : (
                <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
                    <table className="w-full text-sm min-w-[720px]">
                        <thead>
                            <tr className="border-b border-line text-left text-xs uppercase tracking-wider text-ink-4">
                                <th className="px-4 py-3 font-medium">Model</th>
                                <th className="px-4 py-3 font-medium">Kind</th>
                                <th className="px-4 py-3 font-medium text-right">Prompt</th>
                                <th className="px-4 py-3 font-medium text-right">Completion</th>
                                <th className="px-4 py-3 font-medium text-right">Characters</th>
                                <th className="px-4 py-3 font-medium text-right">Calls</th>
                                <th className="px-4 py-3 font-medium text-right">Cost</th>
                            </tr>
                        </thead>
                        <tbody>
                            {report.models.map((line) => (
                                <tr
                                    key={`${line.kind}:${line.model}`}
                                    className="border-b border-line last:border-b-0"
                                >
                                    <td className="px-4 py-3 text-ink">{line.model}</td>
                                    <td className="px-4 py-3 text-ink-3 text-xs">{line.kind}</td>
                                    <td className="px-4 py-3 text-right text-ink-3">
                                        {formatWholeNumber(line.promptTokens)}
                                    </td>
                                    <td className="px-4 py-3 text-right text-ink-3">
                                        {formatWholeNumber(line.completionTokens)}
                                    </td>
                                    <td className="px-4 py-3 text-right text-ink-3">
                                        {formatWholeNumber(line.speechCharacters)}
                                    </td>
                                    <td className="px-4 py-3 text-right text-ink-3">
                                        {formatWholeNumber(line.callCount)}
                                    </td>
                                    <td
                                        className={`px-4 py-3 text-right ${line.estimatedCost === null ? "text-ink-4 italic" : "text-ink"}`}
                                        data-testid={`model-cost-${line.model}`}
                                    >
                                        {formatModelCost(line.estimatedCost, report.currency)}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            <p className="text-sm text-ink-3 mt-3" data-testid="total-cost">
                {formatTotalCost(report.estimatedCost, report.hasUnpricedModels, report.currency)}
            </p>
        </div>
    );
}

/**
 * `/admin/organizations/[organizationId]/quota` — docs/TENANCY/ADMIN_UI_DESIGN.md §3.2, the block's
 * one addition to the platform panel.
 *
 * Two different kinds of edit meet here and the copy keeps them apart: the token limit is what
 * ai-service refuses calls against, while the money beside it is derived from a price table in
 * ai-service configuration that no endpoint exposes — editing that table re-renders history and
 * moves no limit (docs/AI_QUOTAS.md §3).
 */
export default function AdminOrganizationQuotaPage({
    params,
}: {
    params: Promise<{ organizationId: string }>;
}) {
    const { organizationId } = use(params);
    const { authenticatedUser } = useAuthStore();

    const organizationDetail = usePlatformOrganizationDetail(organizationId);
    const quotaSettings = useOrganizationQuotaSettings();
    const spendReport = useOrganizationSpendReport();

    const settings = quotaSettings.data;

    const editability = resolveQuotaEditability(organizationId, authenticatedUser?.orgId);
    const editabilityMessage = describeQuotaEditability(editability);
    const canSave = editability.status === "editable";

    const scopeCaveat =
        editability.status === "no_organization_in_session"
            ? "Your session carries no organization, so this is the installation-wide total across every tenant, not this organization's."
            : editability.status === "different_organization_in_session"
              ? `This is the spend of organization ${editability.sessionOrganizationId} — the one your session is scoped to — not of the organization in this URL.`
              : null;

    return (
        <div className="max-w-5xl">
            <Link
                href="/admin/organizations"
                className="inline-flex items-center gap-2 text-xs text-ink-3 hover:text-ink mb-4"
            >
                <Icon name="arrow-left" size="sm" />
                Organizations
            </Link>

            <h1 className="text-xl font-bold text-ink">AI Quota</h1>
            <p className="text-sm text-ink-3 mt-1 mb-6">
                {organizationDetail.data
                    ? `${organizationDetail.data.name} · ${organizationDetail.data.slug} · ${organizationDetail.data.status}`
                    : organizationDetail.isLoading
                      ? "Loading the organization..."
                      : organizationId}
            </p>

            {editabilityMessage && (
                <div className="bg-warn-soft text-warn rounded-xl px-4 py-3 text-sm mb-6" role="alert">
                    <p className="font-medium">This screen cannot write to that organization.</p>
                    <p className="mt-1">{editabilityMessage}</p>
                    <p className="mt-2 text-xs">
                        <code>GET</code>/<code>PUT /admin/ai-quota</code> resolve the organization from
                        the session token, never from this URL, and impersonation mints a token with{" "}
                        <code>role: User</code>, which cannot satisfy <code>RequirePlatformAdmin</code>.
                        Reading still works; the numbers below are whatever your own session resolves to.
                    </p>
                </div>
            )}

            <section className="rounded-2xl border border-line bg-bg-2 px-4 py-3 mb-6 text-sm text-ink-3">
                <p>
                    <span className="text-ink font-medium">The token limit gates calls.</span> Lowering
                    it makes ai-service refuse work — background pipelines first, conversations last.
                </p>
                <p className="mt-2">
                    <span className="text-ink font-medium">The price table only changes reports.</span>{" "}
                    Money on this page is derived from <code>AiQuotas:PricePerMillionTokens</code> in
                    ai-service configuration. It is not editable here and no endpoint exposes it;
                    changing it re-renders history and moves no limit.
                </p>
            </section>

            {quotaSettings.isLoading && (
                <div className="space-y-2" data-testid="quota-loading">
                    {[1, 2, 3, 4].map((placeholderIndex) => (
                        <div
                            key={placeholderIndex}
                            className="h-14 rounded-xl bg-surface border border-line animate-pulse"
                        />
                    ))}
                </div>
            )}

            {quotaSettings.isError && (
                <div
                    className="bg-bad-soft text-bad rounded-xl px-4 py-3 text-sm flex items-center gap-2"
                    role="alert"
                >
                    <Icon name="warning" size="sm" />
                    Couldn&apos;t load the quota.
                    <button type="button" onClick={() => quotaSettings.refetch()} className="underline">
                        Try again
                    </button>
                </div>
            )}

            {settings && (
                <>
                    {!settings.isOrganizationSpecific && (
                        <p className="text-sm text-ink-3 mb-4" data-testid="no-quota-row">
                            This organization has no quota row yet. It is not unmetered — every number
                            below is the platform default and is already being enforced. Saving creates
                            the row.
                        </p>
                    )}

                    <QuotaEditorForm settings={settings} canSave={canSave} />

                    <h2 className="text-base font-bold text-ink mb-3">The two ceilings</h2>
                    <div className="mb-8">
                        <TwoCeilings
                            settings={settings}
                            report={spendReport.data}
                            isReportSameScope={canSave}
                        />
                    </div>
                </>
            )}

            <h2 className="text-base font-bold text-ink mb-3">This month</h2>
            <SpendPanel
                report={spendReport.data}
                isLoading={spendReport.isLoading}
                isError={spendReport.isError}
                scopeCaveat={scopeCaveat}
            />
        </div>
    );
}
