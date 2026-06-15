"use client";

import { FreeTextContent } from "./types";
import { OpenQuestionEditor } from "./open-question-editor";

interface Props {
    content: FreeTextContent;
    onChange: (c: FreeTextContent) => void;
}

/**
 * WrittenAnswerEditor is an alias for OpenQuestionEditor — both edit free_text content.
 * Kept as a separate export for backwards compatibility with exercise pages.
 */
export function WrittenAnswerEditor(props: Props) {
    return <OpenQuestionEditor {...props} />;
}
