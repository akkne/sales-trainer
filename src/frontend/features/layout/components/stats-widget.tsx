"use client";

import { Icon } from "@/shared/components/icon";

interface StatsWidgetProps {
    currentStreakDayCount: number;
    totalXpAmount: number;
    weeklyXpAmount: number;
    dailyXpGoal?: number;
    dailyXpCurrent?: number;
}

export function StatsWidget({
    currentStreakDayCount,
    totalXpAmount,
    weeklyXpAmount,
    dailyXpGoal = 100,
    dailyXpCurrent = 40,
}: StatsWidgetProps) {
    const remaining = Math.max(0, dailyXpGoal - dailyXpCurrent);

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
            {/* Stats grid */}
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
                <StatTile
                    label="Стрик"
                    value={currentStreakDayCount}
                    unit="дн"
                    icon={<Icon name="flame" size="xs" />}
                    tone="rust"
                />
                <StatTile
                    label="Неделя"
                    value={weeklyXpAmount}
                    unit="XP"
                    icon={<Icon name="bolt" size="xs" />}
                    tone="olive"
                />
                <StatTile
                    label="Всего"
                    value={totalXpAmount.toLocaleString()}
                    unit="XP"
                    icon={<Icon name="trophy" size="xs" />}
                    tone="indigo"
                />
                <StatTile
                    label="Точность"
                    value={87}
                    unit="%"
                    icon={<Icon name="target" size="xs" />}
                />
            </div>

            {/* Daily goal card */}
            <Card
                padding={18}
                style={{
                    background: "var(--ink)",
                    color: "var(--bg)",
                    borderColor: "var(--ink)",
                }}
            >
                <div
                    style={{
                        fontSize: 11,
                        color: "var(--ink-4)",
                        letterSpacing: 1,
                        textTransform: "uppercase",
                        marginBottom: 8,
                        fontFamily: "var(--f-mono)",
                    }}
                >
                    Сегодня
                </div>
                <div
                    style={{
                        fontSize: 20,
                        letterSpacing: -0.3,
                        fontWeight: 500,
                        marginBottom: 12,
                    }}
                >
                    ещё {remaining} XP
                </div>
                <Progress value={dailyXpCurrent} max={dailyXpGoal} tone="indigo" />
                <div
                    style={{
                        marginTop: 10,
                        fontSize: 12,
                        color: "var(--ink-4)",
                        fontFamily: "var(--f-mono)",
                    }}
                >
                    {dailyXpCurrent} / {dailyXpGoal} XP
                </div>
            </Card>

            {/* Tip card */}
            <Card padding={18}>
                <div
                    style={{
                        fontSize: 11,
                        color: "var(--ink-3)",
                        letterSpacing: 1,
                        textTransform: "uppercase",
                        marginBottom: 10,
                        fontWeight: 500,
                        fontFamily: "var(--f-mono)",
                    }}
                >
                    Совет дня
                </div>
                <div
                    style={{
                        fontSize: 14,
                        lineHeight: 1.5,
                        color: "var(--ink-2)",
                    }}
                >
                    Когда клиент говорит «дорого», не называйте скидку. Спросите —
                    <span style={{ color: "var(--indigo)" }}> «дорого по сравнению с чем?»</span>
                </div>
                <div
                    style={{
                        fontSize: 11,
                        color: "var(--ink-3)",
                        marginTop: 12,
                        fontFamily: "var(--f-mono)",
                    }}
                >
                    — Skeptic Sergey
                </div>
            </Card>

            {/* League card */}
            <Card padding={18}>
                <div
                    style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        marginBottom: 12,
                    }}
                >
                    <div
                        style={{
                            fontSize: 11,
                            color: "var(--ink-3)",
                            letterSpacing: 1,
                            textTransform: "uppercase",
                            fontWeight: 500,
                            fontFamily: "var(--f-mono)",
                        }}
                    >
                        Лига
                    </div>
                    <Chip tone="olive" size="sm">
                        Серебро
                    </Chip>
                </div>
                <div style={{ fontSize: 13, color: "var(--ink-2)", marginBottom: 10 }}>
                    Вы на <span className="tnum" style={{ fontWeight: 600 }}>4</span> месте · до
                    повышения{" "}
                    <span className="tnum" style={{ color: "var(--olive)", fontWeight: 600 }}>
                        +120 XP
                    </span>
                </div>
                <Link href="/league" style={{ display: "block" }}>
                    <Button
                        variant="ghost"
                        size="sm"
                        fullWidth
                        iconRightName="chevron-right"
                    >
                        Открыть лигу
                    </Button>
                </Link>
            </Card>
        </div>
    );
}
