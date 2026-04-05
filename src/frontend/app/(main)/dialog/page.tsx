"use client";

import { useDialogBundles } from "@/lib/hooks/useDialog";
import { BundleCard } from "@/components/dialog/BundleCard";

export default function DialogPage() {
    const { data: bundles, isLoading, error } = useDialogBundles();

    if (isLoading) {
        return (
            <div className="p-4">
                <h1 className="text-2xl font-bold text-gray-800 mb-6">Диалог</h1>
                <div className="flex justify-center py-12">
                    <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-[#58CC02]" />
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="p-4">
                <h1 className="text-2xl font-bold text-gray-800 mb-6">Диалог</h1>
                <div className="text-center py-12 text-red-500">
                    Ошибка загрузки: {error.message}
                </div>
            </div>
        );
    }

    if (!bundles || bundles.length === 0) {
        return (
            <div className="p-4">
                <h1 className="text-2xl font-bold text-gray-800 mb-6">Диалог</h1>
                <div className="text-center py-12">
                    <p className="text-gray-500 text-lg">Практика диалогов пока недоступна</p>
                    <p className="text-gray-400 text-sm mt-2">
                        Функция находится в разработке или не настроена
                    </p>
                </div>
            </div>
        );
    }

    return (
        <div className="p-4">
            <h1 className="text-2xl font-bold text-gray-800 mb-2">Диалог</h1>
            <p className="text-gray-500 mb-6">
                Выберите навык для практики
            </p>

            <div className="grid gap-4">
                {bundles.map((bundle) => (
                    <BundleCard key={bundle.id} bundle={bundle} />
                ))}
            </div>
        </div>
    );
}
