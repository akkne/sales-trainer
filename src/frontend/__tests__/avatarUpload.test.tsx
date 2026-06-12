import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";

// Mock apiClient before importing the hook
vi.mock("@/shared/api/api-client", () => ({
    apiClient: {
        postFile: vi.fn(),
    },
}));

// Mock TanStack Query
vi.mock("@tanstack/react-query", () => ({
    useQueryClient: () => ({ invalidateQueries: vi.fn() }),
}));

import { apiClient } from "@/shared/api/api-client";
import { useAvatarUpload } from "@/features/profile/hooks/use-avatar-upload";
import { renderHook, act } from "@testing-library/react";

const mockPostFile = apiClient.postFile as ReturnType<typeof vi.fn>;

describe("useAvatarUpload", () => {
    beforeEach(() => {
        mockPostFile.mockReset();
    });

    it("calls apiClient.postFile with /avatars and FormData containing the file on file selection", async () => {
        mockPostFile.mockResolvedValue({ avatarUrl: "/avatars/user-1" });

        const { result } = renderHook(() => useAvatarUpload());

        const file = new File(["img"], "photo.png", { type: "image/png" });
        const changeEvent = {
            target: { files: [file], value: "" },
        } as unknown as React.ChangeEvent<HTMLInputElement>;

        await act(async () => {
            await result.current.handleFileChange(changeEvent);
        });

        expect(mockPostFile).toHaveBeenCalledOnce();
        const [path, formData] = mockPostFile.mock.calls[0] as [string, FormData];
        expect(path).toBe("/avatars");
        expect(formData.get("file")).toBe(file);
    });

    it("increments version after a successful upload so the avatar src changes", async () => {
        mockPostFile.mockResolvedValue({ avatarUrl: "/avatars/user-1" });

        const { result } = renderHook(() => useAvatarUpload());
        expect(result.current.version).toBe(0);

        const file = new File(["img"], "photo.png", { type: "image/png" });
        const changeEvent = {
            target: { files: [file], value: "" },
        } as unknown as React.ChangeEvent<HTMLInputElement>;

        await act(async () => {
            await result.current.handleFileChange(changeEvent);
        });

        expect(result.current.version).toBeGreaterThan(0);
    });

    it("sets uploadError when postFile rejects", async () => {
        mockPostFile.mockRejectedValue(new Error("Server error"));

        const { result } = renderHook(() => useAvatarUpload());

        const file = new File(["img"], "photo.png", { type: "image/png" });
        const changeEvent = {
            target: { files: [file], value: "" },
        } as unknown as React.ChangeEvent<HTMLInputElement>;

        await act(async () => {
            await result.current.handleFileChange(changeEvent);
        });

        expect(result.current.uploadError).toBe("Server error");
        expect(result.current.version).toBe(0);
    });
});

// Thin render smoke-test: verify that when version > 0 the UserAvatar receives a cache-busted URL
import { UserAvatar } from "@/shared/components/user-avatar";

describe("UserAvatar cache-buster integration", () => {
    it("renders an img whose src contains the ?v= query param when version is appended", () => {
        const baseUrl = "/avatars/user-1";
        const version = 3;
        render(
            <UserAvatar
                avatarUrl={`${baseUrl}?v=${version}`}
                seed="Test User"
                size={88}
                circle
            />
        );
        const img = screen.getByRole("img") as HTMLImageElement;
        expect(img.src).toContain("?v=3");
    });
});
