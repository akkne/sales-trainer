/**
 * Serialize a value to pretty JSON and trigger a browser download. Shared by the
 * admin "Export JSON" buttons so every export downloads the same way.
 */
export function downloadJson(data: unknown, filename: string): void {
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
}

/** Current date as `YYYY-MM-DD`, for dated export filenames. */
export function todayStamp(): string {
    return new Date().toISOString().slice(0, 10);
}
