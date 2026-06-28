"use client";

import type { TheoryCardContent } from "@/features/exercise/types/theory-card";

interface TheoryCardViewProps {
    content: TheoryCardContent;
}

/**
 * Renders a single theory card according to its `layout`. Pure presentation —
 * no answer, no grading. The dialogue layout reuses the exact bubble markup/styles
 * from the Guidebook: `.dlg-example` with `.dx .in` (client) / `.dx .out` (me).
 */
export function TheoryCardView({ content }: TheoryCardViewProps) {
    switch (content.layout) {
        case "text":
            return (
                <div className="theory-card">
                    {content.title && <h2 className="h2 theory-card-title">{content.title}</h2>}
                    <div className="theory-card-body">
                        {content.body.split("\n").filter(Boolean).map((paragraph, i) => (
                            <p key={i}>{paragraph}</p>
                        ))}
                    </div>
                </div>
            );

        case "dialogue":
            return (
                <div className="theory-card">
                    {content.title && <h2 className="h2 theory-card-title">{content.title}</h2>}
                    {/* Reused from the Guidebook dialogue renderer */}
                    <div className="dlg-example">
                        {content.turns.map((turn, i) => {
                            const anno = (turn.annotations ?? []).join(" · ");
                            return (
                                <div key={i} className={"dx " + (turn.side === "me" ? "out" : "in")}>
                                    {turn.text}
                                    {anno && <span className="anno">[{anno}]</span>}
                                </div>
                            );
                        })}
                    </div>
                </div>
            );

        case "bullets":
            return (
                <div className="theory-card">
                    {content.title && <h2 className="h2 theory-card-title">{content.title}</h2>}
                    <ul className="theory-card-bullets">
                        {content.items.map((item, i) => (
                            <li key={i}>{item}</li>
                        ))}
                    </ul>
                </div>
            );

        case "quote":
            return (
                <div className="theory-card theory-card-quote">
                    <blockquote>{content.text}</blockquote>
                    {content.author && <cite>— {content.author}</cite>}
                </div>
            );
    }
}
