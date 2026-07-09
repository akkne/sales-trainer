import "@testing-library/jest-dom";

// jsdom does not implement scrollIntoView; stub it so components that auto-scroll
// (chat/dialog views) don't throw during tests.
if (!Element.prototype.scrollIntoView) {
    Element.prototype.scrollIntoView = () => {};
}

// jsdom does not implement matchMedia; stub it so importing the shared component
// barrel (which pulls in ThemeToggle -> theme-store, evaluated at module load)
// doesn't throw during tests.
if (!window.matchMedia) {
    window.matchMedia = (query: string) => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: () => {},
        removeListener: () => {},
        addEventListener: () => {},
        removeEventListener: () => {},
        dispatchEvent: () => false,
    }) as unknown as MediaQueryList;
}
