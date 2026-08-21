import type { CSSProperties } from "react";
import sanitizeHtml from "sanitize-html";

/**
 * O-2 fix (docs/AUDIT_PROD.md). AI feedback (`feedback.summary` / `feedback.content`) comes back
 * from ai-service as HTML — `<h3>`, `<p>`, `<strong>`, `<ul><li>`, `<br>` — and that HTML is LLM
 * output, not markup this app wrote. It must never reach `dangerouslySetInnerHTML` unsanitized: a
 * crafted transcript could get the model to emit a `<script>` or an `onclick` and have it run in a
 * manager's or a learner's browser. This allowlist matches exactly the tags the feedback renderer
 * already styles for — nothing else has ever been observed in the field.
 */
const FEEDBACK_HTML_OPTIONS: sanitizeHtml.IOptions = {
    allowedTags: ["h3", "p", "strong", "em", "b", "i", "ul", "ol", "li", "br"],
    allowedAttributes: {},
    disallowedTagsMode: "discard",
};

const PLAIN_TEXT_OPTIONS: sanitizeHtml.IOptions = {
    allowedTags: [],
    allowedAttributes: {},
};

/** Boundaries where `sanitize-html` would otherwise glue adjacent words together (R-7). */
const BLOCK_BOUNDARY_TAG_PATTERN = /<\/?(?:h3|p|ul|ol|li|br)\b[^>]*\/?>/gi;

/**
 * `sanitize-html`'s plain-text mode escapes `&`, `<`, `>` in text nodes because its output is
 * meant for `innerHTML` — but `FeedbackTextPreview` renders its result as a React text child,
 * which escapes it a second time (R-8). Undo the HTML escaping so the reader sees the literal
 * characters. Only `&`, `<`, `>` are ever produced by `sanitize-html`'s text escaper here (it
 * does not escape quotes outside of attribute values), and `&amp;` is decoded last so a literal
 * `&lt;` typed by the model (escaped to `&amp;lt;`) round-trips back correctly.
 */
function decodeFeedbackTextEntities(text: string): string {
    return text.replace(/&lt;/g, "<").replace(/&gt;/g, ">").replace(/&amp;/g, "&");
}

/** Strips the LLM's markup down to safe, renderable HTML — never raw model output. */
export function sanitizeFeedbackHtml(html: string): string {
    return sanitizeHtml(html, FEEDBACK_HTML_OPTIONS);
}

/**
 * Reduces the LLM's markup to plain text (e.g. for a line-clamped list row that can't host block
 * markup): tags gone, text kept. This decodes HTML entities back to their literal characters
 * (`&lt;script&gt;` -> `<script>`) so a model-authored `<`/`>`/`&` reads correctly instead of as a
 * visible entity (R-7/R-8) — which means the returned string is, by construction, not something
 * that may ever be safe to hand to `dangerouslySetInnerHTML`, a `title=`, or any other non-React
 * sink: it can contain live-looking markup text. R2-9 found the previous shape of this helper
 * (a plain exported string function) safe only because its one caller happened to render the
 * result as a React text child. Not exported on purpose — go through `FeedbackTextPreview` below,
 * which is typed to only ever be usable as a React child, so misuse is a type error, not a
 * runtime XSS.
 */
function stripFeedbackHtmlToText(html: string): string {
    // Sanitize to the safe allowlist first (strips <script>, event handlers, javascript: hrefs,
    // inline style, etc. — same guarantees as sanitizeFeedbackHtml), then turn block-level
    // boundaries into spaces *before* discarding the remaining tags, so "<h3>Итог</h3><p>..."
    // doesn't collapse into "ИтогПервое...".
    const safeHtml = sanitizeFeedbackHtml(html).replace(BLOCK_BOUNDARY_TAG_PATTERN, " ");
    const textOnly = sanitizeHtml(safeHtml, PLAIN_TEXT_OPTIONS);
    return decodeFeedbackTextEntities(textOnly).replace(/\s+/g, " ").trim();
}

/**
 * A plain-text preview of AI feedback HTML, for a spot that can't host block markup (e.g. a
 * line-clamped list row). Renders as a React text child only — see `stripFeedbackHtmlToText`
 * above for why that string is never exposed directly (R2-9).
 */
export function FeedbackTextPreview({ html }: { html: string }) {
    return <>{stripFeedbackHtmlToText(html)}</>;
}

interface FeedbackHtmlProps {
    html: string;
    className?: string;
    style?: CSSProperties;
}

/** Renders sanitized AI feedback HTML. The one place this app is allowed to use `dangerouslySetInnerHTML`. */
export function FeedbackHtml({ html, className, style }: FeedbackHtmlProps) {
    return (
        <div
            className={className}
            style={style}
            dangerouslySetInnerHTML={{ __html: sanitizeFeedbackHtml(html) }}
        />
    );
}
