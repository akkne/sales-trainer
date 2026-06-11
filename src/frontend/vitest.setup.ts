import "@testing-library/jest-dom";

// jsdom does not implement scrollIntoView; stub it so components that auto-scroll
// (chat/dialog views) don't throw during tests.
if (!Element.prototype.scrollIntoView) {
    Element.prototype.scrollIntoView = () => {};
}
