import { describe, it, expect, vi } from "vitest";

vi.mock("@/config/environment", () => ({
    EnvironmentConfiguration: { apiBaseUrl: "https://api.sellevate.site" },
}));

import { resolveAvatarUrl } from "@/shared/utils/resolve-avatar-url";

describe("resolveAvatarUrl", () => {
    it("prefixes a relative avatar path with the API origin", () => {
        expect(resolveAvatarUrl("/avatars/123")).toBe(
            "https://api.sellevate.site/avatars/123"
        );
    });

    it("preserves a cache-bust query when prefixing", () => {
        expect(resolveAvatarUrl("/avatars/123?v=2")).toBe(
            "https://api.sellevate.site/avatars/123?v=2"
        );
    });

    it("leaves absolute http(s) URLs untouched", () => {
        expect(resolveAvatarUrl("https://cdn.example.com/a.png")).toBe(
            "https://cdn.example.com/a.png"
        );
        expect(resolveAvatarUrl("http://cdn.example.com/a.png")).toBe(
            "http://cdn.example.com/a.png"
        );
    });
});
