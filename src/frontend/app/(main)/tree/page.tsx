"use client";

import { SkillNode } from "@/components/ui/SkillNode";
import { StatsWidget } from "@/components/layout/StatsWidget";
import { useSkillTree } from "@/lib/hooks/useSkillTree";

// Zigzag pattern: center → right → right → center → left → left → ...
const ZIGZAG: Array<"node-center" | "node-left" | "node-right"> = [
    "node-center",
    "node-right",
    "node-right",
    "node-center",
    "node-left",
    "node-left",
];

export default function SkillTreePage() {
    const { data: skillTreeData, isLoading, isError } = useSkillTree();

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

    const nodes = skillTreeData.skillNodes;
    const completedCount = nodes.filter((n) => n.status === "completed").length;
    const totalCount = nodes.length;

    return (
        <div className="max-w-4xl mx-auto px-4 py-8 flex gap-8">
            {/* Main path */}
            <div className="flex-1 min-w-0">
                {/* Section header banner */}
                <div
                    className="rounded-3xl px-6 py-5 mb-8 text-white"
                    style={{
                        background: "#58CC02",
                        boxShadow: "0 4px 0 0 #58A700",
                    }}
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
                                {completedCount}/{totalCount}
                            </span>
                        </div>
                    </div>
                    {/* Progress bar */}
                    <div className="mt-4 h-2 bg-white/30 rounded-full overflow-hidden">
                        <div
                            className="h-full bg-white rounded-full transition-all duration-500"
                            style={{ width: `${totalCount > 0 ? (completedCount / totalCount) * 100 : 0}%` }}
                        />
                    </div>
                </div>

                {/* Skill nodes with vertical path line */}
                <div className="relative flex flex-col items-center gap-0">
                    {/* Background path line */}
                    <div
                        className="absolute left-1/2 -translate-x-1/2 top-9 bottom-9 w-1 rounded-full bg-[#E5E5E5]"
                        aria-hidden
                    />

                    {nodes.map((skillNode, nodeIndex) => {
                        const positionClass = ZIGZAG[nodeIndex % ZIGZAG.length];
                        // Compute how far along the path is "active" (filled green)
                        const isPassedOrActive =
                            skillNode.status === "completed" ||
                            skillNode.status === "in_progress" ||
                            skillNode.status === "available";

                        return (
                            <div
                                key={skillNode.skillId}
                                className="relative w-full flex flex-col items-center pb-12"
                            >
                                {/* Active path segment overlay */}
                                {nodeIndex < nodes.length - 1 && isPassedOrActive && (
                                    <div
                                        className="absolute left-1/2 -translate-x-1/2 top-9 w-1 rounded-full bg-[#58CC02]"
                                        style={{ height: "calc(100% - 36px)" }}
                                        aria-hidden
                                    />
                                )}

                                <SkillNode
                                    skillNode={skillNode}
                                    positionClass={positionClass}
                                />
                            </div>
                        );
                    })}
                </div>
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
