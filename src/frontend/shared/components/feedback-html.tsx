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
    allowedTags: ["h3", "p", "strong", "em", "b", "i", "ul", "li", "br"],
    allowedAttributes: {},
    disallowedTagsMode: "discard",
};

const PLAIN_TEXT_OPTIONS: sanitizeHtml.IOptions = {
    allowedTags: [],
    allowedAttributes: {},
};

/** Strips the LLM's markup down to safe, renderable HTML — never raw model output. */
export function sanitizeFeedbackHtml(html: string): string {
    return sanitizeHtml(html, FEEDBACK_HTML_OPTIONS);
}

/** For previews that can't host block markup (e.g. a line-clamped list row): tags gone, text kept. */
export function stripFeedbackHtml(html: string): string {
    return sanitizeHtml(html, PLAIN_TEXT_OPTIONS).replace(/\s+/g, " ").trim();
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
