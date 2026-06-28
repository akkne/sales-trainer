"use client";

import { useState } from "react";
import type { TheoryCardContent } from "@/features/exercise/types/theory-card";
import { TheoryCardView } from "@/features/exercise/components/theory-card-view";
import { Icon } from "@/shared/components/icon";

// Above this many cards, segmented stories bars become too thin to read — switch
// to a single solid progress bar with a compact "N / M" counter instead.
const MAX_SEGMENTS = 8;

interface TheoryLessonPlayerProps {
    cards: TheoryCardContent[];
    /** Called once when the learner finishes the last card ("Finish"). */
    onComplete: () => void;
    /** True while the completion submission is in flight. */
    isCompleting: boolean;
    onExit: () => void;
}

/**
 * Stories-style player for a theory lesson: swipe/tap through the cards, a progress
 * indicator on top, and a "Finish" button on the last card. Reaching the end is
 * what marks the lesson complete (handled by the caller via onComplete).
 */
export function TheoryLessonPlayer({ cards, onComplete, isCompleting, onExit }: TheoryLessonPlayerProps) {
    const [currentIndex, setCurrentIndex] = useState(0);

    const total = cards.length;
    const isLast = currentIndex >= total - 1;
    const useSegments = total <= MAX_SEGMENTS;

    function goPrev() {
        setCurrentIndex((prev) => Math.max(0, prev - 1));
    }

    function goNext() {
        if (isLast) {
            if (!isCompleting) onComplete();
            return;
        }
        setCurrentIndex((prev) => Math.min(total - 1, prev + 1));
    }

    return (
        <div className="session theory-session">
            {/* Header: exit + progress indicator */}
            <div className="session-top">
                <button className="icon-btn" onClick={onExit} aria-label="Exit">
                    <Icon name="close" size={22} />
                </button>

                <div className="grow">
                    {useSegments ? (
                        <div className="theory-segments" role="progressbar" aria-valuenow={currentIndex + 1} aria-valuemax={total}>
                            {cards.map((_, i) => (
                                <span
                                    key={i}
                                    className={"theory-segment" + (i <= currentIndex ? " filled" : "")}
                                />
                            ))}
                        </div>
                    ) : (
                        <div className="theory-progress-compact">
                            <div className="theory-progress-track">
                                <div
                                    className="theory-progress-fill"
                                    style={{ width: `${((currentIndex + 1) / total) * 100}%` }}
                                />
                            </div>
                            <span className="theory-progress-count num">
                                {currentIndex + 1} / {total}
                            </span>
                        </div>
                    )}
                </div>
            </div>

            {/* Card body with tap zones (left = back, right = forward) */}
            <div className="session-body theory-body">
                <button className="theory-tap theory-tap-left" onClick={goPrev} aria-label="Back" disabled={currentIndex === 0} />
                <button className="theory-tap theory-tap-right" onClick={goNext} aria-label="Next" />

                <div key={currentIndex} className="exercise fade-up theory-card-wrap">
                    <TheoryCardView content={cards[currentIndex]} />
                </div>
            </div>

            {/* Footer: prev arrow + next/finish */}
            <div className="theory-footer">
                <button
                    className="btn btn-ghost"
                    onClick={goPrev}
                    disabled={currentIndex === 0}
                    aria-label="Back"
                >
                    <Icon name="arrow-left" size={18} />
                </button>
                <button className="btn btn-primary btn-lg grow" onClick={goNext} disabled={isCompleting}>
                    {isLast ? (isCompleting ? "Saving…" : "Finish") : "Next"}
                    {!isLast && <Icon name="arrow-right" size={18} />}
                    {isLast && !isCompleting && <Icon name="check" size={18} />}
                </button>
            </div>
        </div>
    );
}
