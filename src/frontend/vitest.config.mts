import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import tsconfigPaths from "vite-tsconfig-paths";

export default defineConfig({
    plugins: [tsconfigPaths(), react()],
    resolve: {
        alias: {
            // vite-tsconfig-paths fails to resolve "@/..." under vitest 3 —
            // pin the alias explicitly to the frontend root.
            "@": fileURLToPath(new URL(".", import.meta.url)),
        },
    },
    test: {
        environment: "jsdom",
        globals: true,
        setupFiles: ["./vitest.setup.ts"],
    },
});
