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
 * meant for `innerHTML` — but `stripFeedbackHtml`'s result is rendered as a React text child,
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

/** For previews that can't host block markup (e.g. a line-clamped list row): tags gone, text kept. */
export function stripFeedbackHtml(html: string): string {
    // Sanitize to the safe allowlist first (strips <script>, event handlers, javascript: hrefs,
    // inline style, etc. — same guarantees as sanitizeFeedbackHtml), then turn block-level
    // boundaries into spaces *before* discarding the remaining tags, so "<h3>Итог</h3><p>..."
    // doesn't collapse into "ИтогПервое...".
    const safeHtml = sanitizeFeedbackHtml(html).replace(BLOCK_BOUNDARY_TAG_PATTERN, " ");
    const textOnly = sanitizeHtml(safeHtml, PLAIN_TEXT_OPTIONS);
    return decodeFeedbackTextEntities(textOnly).replace(/\s+/g, " ").trim();
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
