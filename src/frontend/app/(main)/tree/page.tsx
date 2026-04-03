"use client";

import Link from "next/link";
import { SkillNode } from "@/components/ui/SkillNode";
import { LessonPath } from "@/components/ui/LessonPath";
import { StatsWidget } from "@/components/layout/StatsWidget";
import { useSkillTree } from "@/lib/hooks/useSkillTree";
import { useLessonsForSkill } from "@/lib/hooks/useLesson";
import { useSelectedSkillStore } from "@/lib/store/selectedSkillStore";

// Zigzag pattern for the full skill tree view
const ZIGZAG: Array<"node-center" | "node-left" | "node-right"> = [
    "node-center",
    "node-right",
    "node-right",
    "node-center",
    "node-left",
    "node-left",
];

function SkillLessonView({ skillSlug, skillTitle, skillIcon }: {
    skillSlug: string;
    skillTitle: string;
    skillIcon: string;
}) {
    const { data: lessons, isLoading } = useLessonsForSkill(skillSlug);

    if (isLoading) {
        return (
            <div className="flex items-center justify-center py-20">
                <div className="w-10 h-10 rounded-full border-4 border-[#58CC02] border-t-transparent animate-spin" />
            </div>
        );
    }

    const sorted = (lessons ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder);
    const completedCount = sorted.filter((l) => l.status === "completed").length;

    return (
        <>
            {/* Skill header banner */}
            <div
                className="rounded-3xl px-6 py-5 mb-8 text-white"
                style={{ background: "#58CC02", boxShadow: "0 4px 0 0 #58A700" }}
            >
                <div className="flex items-center justify-between">
                    <div className="flex items-center gap-3">
                        <span className="text-3xl">{skillIcon || "📚"}</span>
                        <div>
                            <p className="text-sm font-semibold opacity-80 uppercase tracking-wider mb-0.5">
                                Текущий навык
                            </p>
                            <h1 className="text-xl font-extrabold">{skillTitle}</h1>
                        </div>
                    </div>
                    <div className="bg-white/20 rounded-2xl px-3 py-2 text-center shrink-0">
                        <span className="text-xl font-extrabold">
                            {completedCount}/{sorted.length}
                        </span>
                    </div>
                </div>
                <div className="mt-4 h-2 bg-white/30 rounded-full overflow-hidden">
                    <div
                        className="h-full bg-white rounded-full transition-all duration-500"
                        style={{
                            width: `${sorted.length > 0 ? (completedCount / sorted.length) * 100 : 0}%`,
                        }}
                    />
                </div>
                <div className="mt-3 flex items-center justify-between">
                    <Link
                        href="/profile"
                        className="text-white/70 text-xs font-semibold hover:text-white transition-colors"
                    >
                        Сменить навык →
                    </Link>
                </div>
            </div>

            <LessonPath lessons={sorted} />
        </>
    );
}

export default function SkillTreePage() {
    const { data: skillTreeData, isLoading, isError } = useSkillTree();
    const { selectedSkill } = useSelectedSkillStore();

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-screen">
                <div className="w-10 h-10 rounded-full border-4 border-[#58CC02] border-t-transparent animate-spin" />
            </div>
        );
    }

    if (isError || !skillTreeData) {
        return (
            <div className="flex items-center justify-center min-h-screen text-gray-500">
                Не удалось загрузить дерево навыков
            </div>
        );
    }

    return (
        <div className="max-w-4xl mx-auto px-4 py-8 flex gap-8">
            <div className="flex-1 min-w-0">
                {selectedSkill ? (
                    <SkillLessonView
                        skillSlug={selectedSkill.slug}
                        skillTitle={selectedSkill.title}
                        skillIcon={selectedSkill.iconName}
                    />
                ) : (
                    <>
                        {/* Full skill tree */}
                        <div
                            className="rounded-3xl px-6 py-5 mb-8 text-white"
                            style={{ background: "#58CC02", boxShadow: "0 4px 0 0 #58A700" }}
                        >
                            <div className="flex items-center justify-between">
                                <div>
                                    <p className="text-sm font-semibold opacity-80 uppercase tracking-wider mb-1">
                                        Путь мастерства
                                    </p>
                                    <h1 className="text-2xl font-extrabold">Навыки продаж</h1>
                                </div>
                                <div className="bg-white/20 rounded-2xl px-4 py-2 text-center">
                                    <span className="text-2xl font-extrabold">
                                        {skillTreeData.skillNodes.filter(n => n.status === "completed").length}/
                                        {skillTreeData.skillNodes.length}
                                    </span>
                                </div>
                            </div>
                            <div className="mt-4 h-2 bg-white/30 rounded-full overflow-hidden">
                                <div
                                    className="h-full bg-white rounded-full transition-all duration-500"
                                    style={{
                                        width: `${skillTreeData.skillNodes.length > 0
                                            ? (skillTreeData.skillNodes.filter(n => n.status === "completed").length / skillTreeData.skillNodes.length) * 100
                                            : 0}%`,
                                    }}
                                />
                            </div>
                            <p className="mt-3 text-white/70 text-xs font-semibold">
                                Выбери навык в профиле, чтобы сфокусироваться →
                            </p>
                        </div>

                        <div className="relative flex flex-col items-center gap-0">
                            <div
                                className="absolute left-1/2 -translate-x-1/2 top-9 bottom-9 w-1 rounded-full bg-[#E5E5E5]"
                                aria-hidden
                            />
                            {skillTreeData.skillNodes.map((skillNode, nodeIndex) => {
                                const positionClass = ZIGZAG[nodeIndex % ZIGZAG.length];
                                const isPassedOrActive =
                                    skillNode.status === "completed" ||
                                    skillNode.status === "in_progress" ||
                                    skillNode.status === "available";

                                return (
                                    <div
                                        key={skillNode.skillId}
                                        className="relative w-full flex flex-col items-center pb-12"
                                    >
                                        {nodeIndex < skillTreeData.skillNodes.length - 1 && isPassedOrActive && (
                                            <div
                                                className="absolute left-1/2 -translate-x-1/2 top-9 w-1 rounded-full bg-[#58CC02]"
                                                style={{ height: "calc(100% - 36px)" }}
                                                aria-hidden
                                            />
                                        )}
                                        <SkillNode skillNode={skillNode} positionClass={positionClass} />
                                    </div>
                                );
                            })}
                        </div>
                    </>
                )}
            </div>

            {/* Right sidebar */}
            <div className="w-52 hidden md:block pt-4 shrink-0">
                <StatsWidget
                    currentStreakDayCount={skillTreeData.currentStreakDayCount}
                    totalXpAmount={skillTreeData.totalXpAmount}
                    weeklyXpAmount={skillTreeData.weeklyXpAmount}
                />
            </div>
        </div>
    );
}
