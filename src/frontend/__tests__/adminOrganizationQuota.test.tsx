import { describe, it, expect, vi, beforeEach } from "vitest";
import { Suspense } from "react";
import { act, render, renderHook, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

vi.mock("@/shared/api/api-client", () => ({
    apiClient: {
        get: vi.fn(),
        post: vi.fn(),
        put: vi.fn(),
        delete: vi.fn(),
    },
}));

import { apiClient } from "@/shared/api/api-client";
import { useAuthStore } from "@/shared/stores/auth-store";
import AdminOrganizationQuotaPage from "@/app/(admin)/admin/organizations/[organizationId]/quota/page";
import {
    useOrganizationQuotaSettings,
    useOrganizationSpendReport,
    useSaveOrganizationQuota,
    type OrganizationQuotaSettings,
    type OrganizationSpendReport,
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
    hasConfiguredLimit,
    resolveQuotaEditability,
    toFormValues,
    NO_PRICE_LABEL,
    TOTAL_COST_UNAVAILABLE_LABEL,
    type QuotaFormValues,
} from "@/features/admin/lib/organization-quota-format";

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;
const mockPut = apiClient.put as ReturnType<typeof vi.fn>;

const ORGANIZATION_ID = "8b1b6f3a-0000-4000-8000-000000000001";
const OTHER_ORGANIZATION_ID = "8b1b6f3a-0000-4000-8000-000000000002";

function createWrapper() {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const TestQueryWrapper = ({ children }: { children: ReactNode }) => (
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
    TestQueryWrapper.displayName = "TestQueryWrapper";
    return TestQueryWrapper;
}

function buildSettings(overrides: Partial<OrganizationQuotaSettings> = {}): OrganizationQuotaSettings {
    return {
        voiceDailyLimitMinutes: null,
        voiceMonthlyLimitMinutes: null,
        llmMonthlyTokenLimit: null,
        batchReservePercent: null,
        note: null,
        isOrganizationSpecific: false,
        effectiveVoiceDailyLimitMinutes: 600,
        effectiveVoiceMonthlyLimitMinutes: 6000,
        effectiveLlmMonthlyTokenLimit: 20_000_000,
        effectiveBatchReservePercent: 10,
        updatedAt: null,
        ...overrides,
    };
}

function buildReport(overrides: Partial<OrganizationSpendReport> = {}): OrganizationSpendReport {
    return {
        periodKey: "2026-08",
        currency: "RUB",
        quotaState: "ok",
        llmPromptTokens: 612_340,
        llmCompletionTokens: 180_905,
        llmTotalTokens: 793_245,
        llmMonthlyTokenLimit: 20_000_000,
        llmCallCount: 1841,
        llmEstimatedCallCount: 12,
        speechCharacters: 418_220,
        voiceUsedMinutesToday: 37,
        voiceDailyLimitMinutes: 600,
        voiceUsedMinutesThisMonth: 612,
        voiceMonthlyLimitMinutes: 6000,
        estimatedCost: null,
        hasUnpricedModels: true,
        models: [],
        ...overrides,
    };
}

const EMPTY_FORM: QuotaFormValues = {
    voiceDailyLimitMinutes: "",
    voiceMonthlyLimitMinutes: "",
    llmMonthlyTokenLimit: "",
    batchReservePercent: "",
    note: "",
};

describe("organization quota hooks", () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it("reads the allowance from the platform-only quota route", async () => {
        const settings = buildSettings();
        mockGet.mockResolvedValueOnce(settings);

        const { result } = renderHook(() => useOrganizationQuotaSettings(), { wrapper: createWrapper() });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockGet).toHaveBeenCalledWith("/admin/ai-quota");
        expect(result.current.data).toEqual(settings);
    });

    it("reads the spend report with no parameters at all — the endpoint has none", async () => {
        mockGet.mockResolvedValueOnce(buildReport());

        const { result } = renderHook(() => useOrganizationSpendReport(), { wrapper: createWrapper() });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockGet).toHaveBeenCalledWith("/admin/ai-usage");
    });

    it("writes the allowance without ever sending an organization id in the body", async () => {
        mockPut.mockResolvedValueOnce(buildSettings({ isOrganizationSpecific: true }));

        const { result } = renderHook(() => useSaveOrganizationQuota(), { wrapper: createWrapper() });
        await result.current.mutateAsync({
            voiceDailyLimitMinutes: 300,
            voiceMonthlyLimitMinutes: null,
            llmMonthlyTokenLimit: 5_000_000,
            batchReservePercent: 20,
            note: "contract SL-14",
        });

        expect(mockPut).toHaveBeenCalledWith("/admin/ai-quota", {
            voiceDailyLimitMinutes: 300,
            voiceMonthlyLimitMinutes: null,
            llmMonthlyTokenLimit: 5_000_000,
            batchReservePercent: 20,
            note: "contract SL-14",
        });

        const [, requestBody] = mockPut.mock.calls[0];
        expect(Object.keys(requestBody as object)).not.toContain("organizationId");
    });
});

/**
 * The rule slice 9 settled and this screen must not undo: an unpriced model reports `null`, and
 * `null` is words, never a zero (docs/AI_QUOTAS.md §3, backend review 40.34).
 */
describe("cost formatting", () => {
    it("renders a null model cost as an explicit no-price state, never as zero", () => {
        expect(formatModelCost(null, "RUB")).toBe(NO_PRICE_LABEL);
        expect(formatModelCost(null, "RUB")).not.toContain("0");
    });

    it("renders a priced model normally", () => {
        expect(formatModelCost(543.6789, "RUB")).toBe("543.68 ₽");
    });

    it("refuses to print a total while any model is unpriced, even when an amount is present", () => {
        expect(formatTotalCost(1200, true, "RUB")).toBe(TOTAL_COST_UNAVAILABLE_LABEL);
        expect(formatTotalCost(null, false, "RUB")).toBe(TOTAL_COST_UNAVAILABLE_LABEL);
        expect(formatTotalCost(1200, false, "RUB")).toBe("Estimated cost this month: 1,200.00 ₽");
    });

    it("falls back to the raw currency code it has no symbol for", () => {
        expect(formatModelCost(10, "XTS")).toBe("10.00 XTS");
    });
});

describe("the two ceilings", () => {
    it("matches the backend's BatchTokenCeiling, integer truncation included", () => {
        expect(calculateBatchTokenCeiling(20_000_000, 10)).toBe(18_000_000);
        expect(calculateBatchTokenCeiling(1001, 10)).toBe(901);
        expect(calculateBatchTokenCeiling(20_000_000, 0)).toBe(20_000_000);
    });

    it("clamps the reserve to 90 the way ResolvedAiQuota does", () => {
        expect(calculateBatchTokenCeiling(1_000_000, 200)).toBe(calculateBatchTokenCeiling(1_000_000, 90));
    });

    it("never reports a negative remainder for an organization past its ceiling", () => {
        expect(calculateRemainingTokens(18_000_000, 19_000_000)).toBe(0);
        expect(calculateRemainingTokens(18_000_000, 1_000_000)).toBe(17_000_000);
    });

    it("treats a zero limit as no ceiling, because ai-service gates on limit > 0", () => {
        expect(hasConfiguredLimit(0)).toBe(false);
        expect(hasConfiguredLimit(1)).toBe(true);
    });

    it("says out loud that typing 0 removes the ceiling rather than closing it", () => {
        expect(describeZeroLimitEffect("0")).toContain("removes the ceiling");
        expect(describeZeroLimitEffect("600")).toBeNull();
        expect(describeZeroLimitEffect("")).toBeNull();
    });
});

describe("effective value captions", () => {
    it("names the platform default when the organization set nothing", () => {
        expect(describeEffectiveValue(null, 6000, "minutes")).toBe(
            "In effect now: 6,000 minutes (platform default)"
        );
    });

    it("names the organization's own value when it set one", () => {
        expect(describeEffectiveValue(300, 300, "minutes")).toBe(
            "In effect now: 300 minutes (this organization's own value)"
        );
    });
});

/**
 * `PUT /admin/ai-quota` writes to `X-Organization-Id`, which the gateway derives from the token and
 * from nothing a caller can set — so the `[organizationId]` in the URL is a claim to be checked, not
 * a parameter to be sent.
 */
describe("editability against the session's own organization", () => {
    it("allows editing only when the session is scoped to the organization in the URL", () => {
        expect(resolveQuotaEditability(ORGANIZATION_ID, ORGANIZATION_ID)).toEqual({ status: "editable" });
        expect(resolveQuotaEditability(ORGANIZATION_ID, ORGANIZATION_ID.toUpperCase())).toEqual({
            status: "editable",
        });
    });

    it("refuses when the session carries no organization at all", () => {
        expect(resolveQuotaEditability(ORGANIZATION_ID, null)).toEqual({
            status: "no_organization_in_session",
        });
        expect(describeQuotaEditability({ status: "no_organization_in_session" })).toContain(
            "platform defaults"
        );
    });

    it("refuses when the session is scoped to a different organization", () => {
        const editability = resolveQuotaEditability(ORGANIZATION_ID, OTHER_ORGANIZATION_ID);
        expect(editability).toEqual({
            status: "different_organization_in_session",
            sessionOrganizationId: OTHER_ORGANIZATION_ID,
        });
        expect(describeQuotaEditability(editability)).toContain(OTHER_ORGANIZATION_ID);
    });

    it("has nothing to say when the session matches", () => {
        expect(describeQuotaEditability({ status: "editable" })).toBeNull();
    });
});

describe("building the write model", () => {
    it("clears an empty field to null, which resets it to the platform default", () => {
        const result = buildQuotaWriteModel(EMPTY_FORM);
        expect(result).toEqual({
            status: "valid",
            writeModel: {
                voiceDailyLimitMinutes: null,
                voiceMonthlyLimitMinutes: null,
                llmMonthlyTokenLimit: null,
                batchReservePercent: null,
                note: null,
            },
        });
    });

    it("passes whole numbers through and trims the note", () => {
        const result = buildQuotaWriteModel({
            voiceDailyLimitMinutes: "300",
            voiceMonthlyLimitMinutes: "3000",
            llmMonthlyTokenLimit: "5000000",
            batchReservePercent: "0",
            note: "  contract SL-14  ",
        });

        expect(result).toEqual({
            status: "valid",
            writeModel: {
                voiceDailyLimitMinutes: 300,
                voiceMonthlyLimitMinutes: 3000,
                llmMonthlyTokenLimit: 5_000_000,
                batchReservePercent: 0,
                note: "contract SL-14",
            },
        });
    });

    it("refuses a negative number rather than letting the backend silently reset the field", () => {
        const result = buildQuotaWriteModel({ ...EMPTY_FORM, voiceDailyLimitMinutes: "-5" });
        expect(result.status).toBe("invalid");
        if (result.status === "invalid") {
            expect(result.validationMessage).toContain("Voice minutes per day");
        }
    });

    it("refuses anything that is not a whole number", () => {
        expect(buildQuotaWriteModel({ ...EMPTY_FORM, llmMonthlyTokenLimit: "5e6" }).status).toBe("invalid");
        expect(buildQuotaWriteModel({ ...EMPTY_FORM, llmMonthlyTokenLimit: "1.5" }).status).toBe("invalid");
    });

    it("refuses a reserve above 90 rather than letting ai-service clamp it silently", () => {
        const result = buildQuotaWriteModel({ ...EMPTY_FORM, batchReservePercent: "95" });
        expect(result.status).toBe("invalid");
        if (result.status === "invalid") {
            expect(result.validationMessage).toContain("90%");
        }
        expect(buildQuotaWriteModel({ ...EMPTY_FORM, batchReservePercent: "90" }).status).toBe("valid");
    });

    it("shows an unset field as empty rather than pre-filling the platform default as if it were set", () => {
        expect(toFormValues(buildSettings())).toEqual(EMPTY_FORM);
        expect(
            toFormValues(buildSettings({ voiceDailyLimitMinutes: 0, note: "contract SL-14" }))
        ).toEqual({ ...EMPTY_FORM, voiceDailyLimitMinutes: "0", note: "contract SL-14" });
    });
});

async function renderQuotaPage() {
    const Wrapper = createWrapper();
    const routeParameters = Promise.resolve({ organizationId: ORGANIZATION_ID });
    let renderResult!: ReturnType<typeof render>;
    await act(async () => {
        renderResult = render(
            <Wrapper>
                <Suspense fallback={<div>suspense</div>}>
                    <AdminOrganizationQuotaPage params={routeParameters} />
                </Suspense>
            </Wrapper>
        );
        await routeParameters;
    });
    return renderResult;
}

function answerByPath(answers: Record<string, unknown>) {
    mockGet.mockImplementation((path: string) => {
        const answer = Object.entries(answers).find(([prefix]) => path.startsWith(prefix));
        return answer ? Promise.resolve(answer[1]) : Promise.reject(new Error(`unstubbed ${path}`));
    });
}

describe("the quota screen", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        useAuthStore.setState({
            accessToken: "platform-token",
            authenticatedUser: {
                id: "staff-1",
                email: "staff@sellevate.com",
                displayName: "Staff",
                isOnboardingCompleted: true,
                role: "SuperAdmin",
                orgId: ORGANIZATION_ID,
                orgRole: null,
            },
        });
    });

    it("shows a loading state before either read answers", async () => {
        mockGet.mockImplementation(() => new Promise(() => {}));

        await renderQuotaPage();

        await waitFor(() => expect(screen.getByTestId("quota-loading")).toBeTruthy());
    });

    it("reports a failed quota read without pretending the numbers are zero", async () => {
        mockGet.mockRejectedValue(new Error("gateway is down"));

        await renderQuotaPage();

        await waitFor(() => expect(screen.getByText(/Couldn't load the quota/)).toBeTruthy());
    });

    it("says an organization with no row of its own is metered against the defaults, not unmetered", async () => {
        answerByPath({
            "/admin/ai-quota": buildSettings(),
            "/admin/ai-usage": buildReport(),
            "/organizations/": { id: ORGANIZATION_ID, name: "Acme Sales", slug: "acme-sales", status: "Active", createdAt: "", updatedAt: "" },
        });

        await renderQuotaPage();

        await waitFor(() => expect(screen.getByTestId("no-quota-row")).toBeTruthy());
        expect(screen.getByTestId("no-quota-row").textContent).toContain("not unmetered");
        expect(screen.getByText(/In effect now: 6,000 minutes \(platform default\)/)).toBeTruthy();
    });

    it("renders an unpriced model as words and never as a zero amount", async () => {
        answerByPath({
            "/admin/ai-quota": buildSettings(),
            "/admin/ai-usage": buildReport({
                models: [
                    {
                        model: "gpt-4o",
                        kind: "llm",
                        promptTokens: 612_340,
                        completionTokens: 180_905,
                        callCount: 1610,
                        speechCharacters: 0,
                        estimatedCost: null,
                    },
                    {
                        model: "yandex-tts",
                        kind: "tts",
                        promptTokens: 0,
                        completionTokens: 0,
                        callCount: 231,
                        speechCharacters: 418_220,
                        estimatedCost: 543.6789,
                    },
                ],
                estimatedCost: null,
                hasUnpricedModels: true,
            }),
            "/organizations/": { id: ORGANIZATION_ID, name: "Acme Sales", slug: "acme-sales", status: "Active", createdAt: "", updatedAt: "" },
        });

        await renderQuotaPage();

        await waitFor(() => expect(screen.getByTestId("model-cost-gpt-4o")).toBeTruthy());
        expect(screen.getByTestId("model-cost-gpt-4o").textContent).toBe(NO_PRICE_LABEL);
        expect(screen.getByTestId("model-cost-yandex-tts").textContent).toBe("543.68 ₽");
        expect(screen.getByTestId("total-cost").textContent).toBe(TOTAL_COST_UNAVAILABLE_LABEL);
        expect(screen.queryByText("0.00 ₽")).toBeNull();
    });

    it("separates the limit that gates calls from the price table that only moves reports", async () => {
        answerByPath({
            "/admin/ai-quota": buildSettings(),
            "/admin/ai-usage": buildReport(),
            "/organizations/": { id: ORGANIZATION_ID, name: "Acme Sales", slug: "acme-sales", status: "Active", createdAt: "", updatedAt: "" },
        });

        await renderQuotaPage();

        await waitFor(() => expect(screen.getByText(/The token limit gates calls/)).toBeTruthy());
        expect(screen.getByText(/The price table only changes reports/)).toBeTruthy();
        expect(screen.getByText(/Background work stops at/)).toBeTruthy();
        expect(screen.getByText("18,000,000 tokens")).toBeTruthy();
        expect(screen.getByText("20,000,000 tokens")).toBeTruthy();
    });

    it("disables saving and says why when the session is scoped to no organization", async () => {
        useAuthStore.setState({
            authenticatedUser: {
                id: "staff-1",
                email: "staff@sellevate.com",
                displayName: "Staff",
                isOnboardingCompleted: true,
                role: "SuperAdmin",
                orgId: null,
                orgRole: null,
            },
        });
        answerByPath({
            "/admin/ai-quota": buildSettings(),
            "/admin/ai-usage": buildReport(),
            "/organizations/": { id: ORGANIZATION_ID, name: "Acme Sales", slug: "acme-sales", status: "Active", createdAt: "", updatedAt: "" },
        });

        await renderQuotaPage();

        await waitFor(() =>
            expect(screen.getByText(/This screen cannot write to that organization/)).toBeTruthy()
        );
        expect(screen.getByRole("button", { name: "Save quota" })).toHaveProperty("disabled", true);
        expect(screen.getByText(/installation-wide total/)).toBeTruthy();
    });

    it("shows no gamification of any kind", async () => {
        answerByPath({
            "/admin/ai-quota": buildSettings(),
            "/admin/ai-usage": buildReport(),
            "/organizations/": { id: ORGANIZATION_ID, name: "Acme Sales", slug: "acme-sales", status: "Active", createdAt: "", updatedAt: "" },
        });

        const { container } = await renderQuotaPage();

        await waitFor(() => expect(screen.getByText(/The token limit gates calls/)).toBeTruthy());
        const renderedText = container.textContent ?? "";
        expect(renderedText).not.toMatch(/\bXP\b|streak|league/i);
    });
});
