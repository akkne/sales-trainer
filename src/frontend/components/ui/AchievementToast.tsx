"use client";

import { useEffect, useState } from "react";
import { Icon } from "@/components/ui/Icon";

export interface AchievementToastData {
    key: string;
    iconEmoji: string;
    title: string;
    description: string;
}

interface AchievementToastProps {
    achievement: AchievementToastData;
    onDismiss: () => void;
}

export function AchievementToast({ achievement, onDismiss }: AchievementToastProps) {
    const [visible, setVisible] = useState(false);

    useEffect(() => {
        const showTimer = requestAnimationFrame(() => setVisible(true));

        const dismissTimer = setTimeout(() => {
            setVisible(false);
            setTimeout(onDismiss, 350);
        }, 4000);

        return () => {
            cancelAnimationFrame(showTimer);
            clearTimeout(dismissTimer);
        };
    }, [onDismiss]);

    return (
        <div
            onClick={() => {
                setVisible(false);
                setTimeout(onDismiss, 350);
            }}
            style={{
                display: "flex",
                alignItems: "center",
                gap: 16,
                background: "var(--surface)",
                border: "2px solid var(--olive)",
                borderRadius: 16,
                padding: "14px 18px",
                boxShadow: "var(--sh-2)",
                cursor: "pointer",
                userSelect: "none",
                transition: "all 300ms cubic-bezier(0.5, 1.6, 0.4, 1)",
                opacity: visible ? 1 : 0,
                transform: visible ? "translateY(0) scale(1)" : "translateY(-20px) scale(0.95)",
            }}
            role="alert"
            aria-live="polite"
        >
            <div
                style={{
                    width: 48,
                    height: 48,
                    borderRadius: 12,
                    background: "var(--olive-soft)",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    flexShrink: 0,
                    fontSize: 24,
                }}
            >
                {achievement.iconEmoji}
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
                <div
                    style={{
                        display: "flex",
                        alignItems: "center",
                        gap: 4,
                        fontSize: 10,
                        fontWeight: 600,
                        textTransform: "uppercase",
                        letterSpacing: 1,
                        color: "var(--olive)",
                        marginBottom: 2,
                        fontFamily: "var(--f-mono)",
                    }}
                >
                    <Icon name="trophy" size="xs" />
                    Достижение разблокировано!
                </div>
                <p
                    style={{
                        margin: 0,
                        fontWeight: 600,
                        fontSize: 14,
                        lineHeight: 1.2,
                        whiteSpace: "nowrap",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                    }}
                >
                    {achievement.title}
                </p>
                <p
                    style={{
                        margin: 0,
                        fontSize: 12,
                        color: "var(--ink-3)",
                        whiteSpace: "nowrap",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                    }}
                >
                    {achievement.description}
                </p>
            </div>
        </div>
    );
}

interface AchievementToastQueueProps {
    queue: AchievementToastData[];
    onDismiss: (key: string) => void;
}

export function AchievementToastQueue({ queue, onDismiss }: AchievementToastQueueProps) {
    if (queue.length === 0) return null;
    const current = queue[0];

    return (
        <div
            style={{
                position: "fixed",
                top: 16,
                left: 0,
                right: 0,
                zIndex: 50,
                display: "flex",
                justifyContent: "center",
                padding: "0 16px",
                pointerEvents: "none",
            }}
        >
            <div style={{ pointerEvents: "auto", width: "100%", maxWidth: 360 }}>
                <AchievementToast
                    key={current.key}
                    achievement={current}
                    onDismiss={() => onDismiss(current.key)}
                />
            </div>
        </div>
    );
}
