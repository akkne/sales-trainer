"use client";

import { useParams, useRouter } from "next/navigation";
import { useDialogBundles, useDialogModes } from "@/features/dialog/hooks/use-dialog";
import { ModeCard } from "@/features/dialog/components/mode-card";

export default function BundleModesPage() {
    const params = useParams();
    const router = useRouter();
    const bundleId = params.bundleId as string;

    const { data: bundles } = useDialogBundles();
    const { data: modes, isLoading, error } = useDialogModes(bundleId);

    const currentBundle = bundles?.find((bundle) => bundle.id === bundleId);

    if (isLoading) {
        return (
            <div className="p-4">
                <button
                    onClick={() => router.back()}
                    className="mb-4 text-gray-500 hover:text-gray-700"
                >
                    ← Назад
                </button>
                <div className="flex justify-center py-12">
                    <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-[#58CC02]" />
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="p-4">
                <button
                    onClick={() => router.back()}
                    className="mb-4 text-gray-500 hover:text-gray-700"
                >
                    ← Назад
                </button>
                <div className="text-center py-12 text-red-500">
                    Ошибка загрузки: {error.message}
                </div>
            </div>
        );
    }

    return (
        <div className="p-4">
            <button
                onClick={() => router.push("/dialog")}
                className="mb-4 text-gray-500 hover:text-gray-700 flex items-center gap-1"
            >
                ← Назад к навыкам
            </button>

            <div className="flex items-center gap-3 mb-6">
                {currentBundle && (
                    <span className="text-3xl">{currentBundle.iconEmoji}</span>
                )}
                <div>
                    <h1 className="text-2xl font-bold text-gray-800">
                        {currentBundle?.title || "Режимы практики"}
                    </h1>
                    <p className="text-gray-500">
                        Выберите режим для тренировки
                    </p>
                </div>
            </div>

            {(!modes || modes.length === 0) ? (
                <div className="text-center py-12">
                    <p className="text-gray-500">Режимы пока не добавлены</p>
                </div>
            ) : (
                <div className="grid gap-4">
                    {modes.map((mode) => (
                        <ModeCard
                            key={mode.id}
                            bundleId={bundleId}
                            mode={mode}
                        />
                    ))}
                </div>
            )}
        </div>
    );
}
