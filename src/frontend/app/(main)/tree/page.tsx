"use client";

import { SkillNode } from "@/components/ui/SkillNode";
import { StatsWidget } from "@/components/layout/StatsWidget";
import { useSkillTree } from "@/lib/hooks/useSkillTree";

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

    return (
        <div className="max-w-4xl mx-auto px-4 py-8 flex gap-8">
            <div className="flex-1">
                <h1 className="font-[var(--font-space-grotesk)] text-3xl font-bold text-gray-900 mb-8">
                    Путь мастерства
                </h1>

                <div className="flex flex-col gap-8 relative">
                    {skillTreeData.skillNodes.map((skillNode, nodeIndex) => (
                        <div key={skillNode.skillId} className="relative">
                            {nodeIndex < skillTreeData.skillNodes.length - 1 && (
                                <div
                                    className={`absolute left-[35px] top-[70px] w-1 h-8 rounded-full ${
                                        skillNode.status === "completed"
                                            ? "bg-[#58CC02]"
                                            : "bg-gray-200"
                                    }`}
                                />
                            )}
                            <SkillNode
                                skillNode={skillNode}
                                positionIndex={nodeIndex}
                            />
                        </div>
                    ))}
                </div>
            </div>

            <div className="w-48 hidden md:block pt-16">
                <StatsWidget
                    currentStreakDayCount={skillTreeData.currentStreakDayCount}
                    totalXpAmount={skillTreeData.totalXpAmount}
                    weeklyXpAmount={skillTreeData.weeklyXpAmount}
                />
            </div>
        </div>
    );
}
