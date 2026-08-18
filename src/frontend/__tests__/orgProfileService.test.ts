import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/shared/api/api-client";

const apiClientMock = vi.hoisted(() => ({
    get: vi.fn(),
    patch: vi.fn(),
    put: vi.fn(),
    post: vi.fn(),
}));

vi.mock("@/shared/api/api-client", async () => {
    const actual = await vi.importActual<typeof import("@/shared/api/api-client")>(
        "@/shared/api/api-client"
    );
    return { ...actual, apiClient: apiClientMock };
});

const { organizationProfileService } = await import(
    "@/features/org-profile/services/organization-profile-service"
);
const { describeProfileWriteFailure } = await import(
    "@/features/org-profile/hooks/use-organization-profile"
);

describe("organization profile service", () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it("reads a 404 as «профиля ещё нет», not as an error", async () => {
        apiClientMock.get.mockRejectedValueOnce(new ApiError(404, {}));
        await expect(organizationProfileService.getProfile()).resolves.toBeNull();
    });

    it("still fails on a real error", async () => {
        apiClientMock.get.mockRejectedValueOnce(new ApiError(500, { message: "boom" }));
        await expect(organizationProfileService.getProfile()).rejects.toBeInstanceOf(ApiError);
    });

    it("asks for three questions by default, the cap the interview is built around", async () => {
        apiClientMock.get.mockResolvedValueOnce({
            questions: [],
            totalGapCount: 0,
            blockingGapCount: 0,
            isReadyForParameterization: true,
        });

        await organizationProfileService.getGaps();
        expect(apiClientMock.get).toHaveBeenCalledWith("/organizations/profile/gaps?limit=3");
    });

    it("answers one question with PATCH and replaces the row with PUT", async () => {
        apiClientMock.patch.mockResolvedValueOnce({});
        apiClientMock.put.mockResolvedValueOnce({});

        await organizationProfileService.patchProfile({ product: "СРМ" });
        expect(apiClientMock.patch).toHaveBeenCalledWith("/organizations/profile", {
            product: "СРМ",
        });

        await organizationProfileService.replaceProfile({
            product: null,
            icp: null,
            tone: null,
            objections: [],
            scriptStages: [],
            glossary: {},
            bannedClaims: [],
        });
        expect(apiClientMock.put).toHaveBeenCalledWith(
            "/organizations/profile",
            expect.objectContaining({ bannedClaims: [] })
        );
    });

    it("previews a draft without writing and applies it on a separate route", async () => {
        apiClientMock.post.mockResolvedValue({});

        await organizationProfileService.previewDraft({ product: "СРМ" });
        expect(apiClientMock.post).toHaveBeenCalledWith("/organizations/profile/draft", {
            product: "СРМ",
        });

        await organizationProfileService.applyDraft({
            draft: { product: "СРМ" },
            acceptedFields: ["product"],
        });
        expect(apiClientMock.post).toHaveBeenCalledWith("/organizations/profile/draft/apply", {
            draft: { product: "СРМ" },
            acceptedFields: ["product"],
        });
    });
});

/**
 * Reading the profile is open to every member of the organization and writing it is not, so a 403
 * on a save is a fact about the reader's role, not a fault to retry.
 */
describe("write failures the screen has to explain", () => {
    it("explains a 403 as a role, not as an error", () => {
        expect(describeProfileWriteFailure(new ApiError(403, {}))).toMatch(
            /только администратор организации/
        );
    });

    it("passes the server's own message through when it has one", () => {
        expect(
            describeProfileWriteFailure(new ApiError(400, { message: "Черновик пуст" }))
        ).toBe("Черновик пуст");
    });

    it("falls back to a retryable sentence for anything else", () => {
        expect(describeProfileWriteFailure(new Error("network"))).toMatch(/Попробуйте ещё раз/);
    });
});
