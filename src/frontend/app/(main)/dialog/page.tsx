"use client";

import { useDialogBundles } from "@/lib/hooks/useDialog";
import { BundleCard } from "@/components/dialog/BundleCard";
import { Icon } from "@/components/ui/Icon";
import Link from "next/link";

export default function DialogPage() {
    const { data: bundles, isLoading, error } = useDialogBundles();

    if (isLoading) {
        return (
            <div className="max-w-3xl mx-auto px-4 py-8">
                <div className="mb-6">
                    <div className="flex items-center gap-2 mb-2">
                        <Icon name="forum" size="lg" className="text-primary" />
                        <h1 className="font-headline font-bold text-2xl text-on-surface">Диалоги</h1>
                    </div>
                    <p className="text-sm text-on-surface-variant">
                        Интерактивные сценарии для отработки навыков
                    </p>
                </div>
                <div className="flex justify-center py-12">
                    <div className="animate-spin rounded-full h-8 w-8 border-4 border-primary border-t-transparent" />
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="max-w-3xl mx-auto px-4 py-8">
                <div className="mb-6">
                    <div className="flex items-center gap-2 mb-2">
                        <Icon name="forum" size="lg" className="text-primary" />
                        <h1 className="font-headline font-bold text-2xl text-on-surface">Диалоги</h1>
                    </div>
                </div>
                <div className="text-center py-12 bg-error-container rounded-2xl">
                    <Icon name="error" size="xl" className="text-error mx-auto mb-3" />
                    <p className="text-error font-medium">Ошибка загрузки: {error.message}</p>
                </div>
            </div>
        );
    }

    if (!bundles || bundles.length === 0) {
        return (
            <div className="max-w-3xl mx-auto px-4 py-8">
                <div className="mb-6">
                    <div className="flex items-center gap-2 mb-2">
                        <Icon name="forum" size="lg" className="text-primary" />
                        <h1 className="font-headline font-bold text-2xl text-on-surface">Диалоги</h1>
                    </div>
                    <p className="text-sm text-on-surface-variant">
                        Интерактивные сценарии для отработки навыков
                    </p>
                </div>
                <div className="text-center py-16 bg-surface-container rounded-2xl">
                    <div className="w-16 h-16 rounded-full bg-surface-container-high flex items-center justify-center mx-auto mb-4">
                        <Icon name="forum" size="xl" className="text-on-surface-variant" />
                    </div>
                    <p className="font-semibold text-on-surface mb-1">Практика диалогов пока недоступна</p>
                    <p className="text-sm text-on-surface-variant">
                        Функция находится в разработке или не настроена
                    </p>
                </div>
            </div>
        );
    }

    return (
        <div className="max-w-3xl mx-auto px-4 py-8">
            {/* Header */}
            <div className="mb-2">
                <p className="text-sm font-semibold text-primary uppercase tracking-widest mb-1">
                    Навыки диалога
                </p>
                <h1 className="font-headline font-bold text-3xl text-on-surface leading-tight mb-3">
                    Мастерство разговора.
                </h1>
                <p className="text-on-surface-variant mb-6">
                    Интерактивные сценарии для отработки техник продаж. Выбери модуль для симуляции.
                </p>
            </div>

            {/* Bundle cards */}
            <div className="flex flex-col gap-4">
                {bundles.map((bundle) => (
                    <BundleCard key={bundle.id} bundle={bundle} />
                ))}
            </div>

            {/* Challenge Banner */}
            <div className="mt-8 rounded-2xl bg-primary overflow-hidden flex flex-col md:flex-row items-stretch">
                {/* Text side */}
                <div className="flex-1 p-6">
                    <h3 className="font-headline font-bold text-lg text-on-primary mb-2">
                        Готов к живому тесту?
                    </h3>
                    <p className="text-sm text-on-primary/80 mb-4">
                        Подключись к AI-наставнику для 10-минутной симуляции высокого давления.
                    </p>
                    <Link
                        href="/tree"
                        className="inline-flex items-center gap-1 px-5 py-2.5 rounded-full bg-primary-container text-on-primary-container font-semibold text-sm hover:opacity-90 tonal-transition"
                    >
                        Начать испытание
                        <Icon name="bolt" size="sm" />
                    </Link>
                </div>
                {/* Decorative side */}
                <div className="w-full md:w-48 h-32 md:h-auto bg-primary-dim flex items-center justify-center">
                    <Icon name="psychology" size="2xl" className="text-on-primary/30" />
                </div>
            </div>
        </div>
    );
}
