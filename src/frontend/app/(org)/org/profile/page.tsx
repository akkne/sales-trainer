"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/shared/components/button";
import { Card, CardContent, CardSkeleton } from "@/shared/components/card";
import { ErrorState } from "@/shared/components/error-state";
import { PageHeader } from "@/shared/components/page-header";
import { InterviewQuestion } from "@/features/org-profile/components/interview-question";
import { MaterialRunDialog } from "@/features/org-profile/components/material-run-dialog";
import { ProfileDraftPreview } from "@/features/org-profile/components/profile-draft-preview";
import { ProfileFullForm } from "@/features/org-profile/components/profile-full-form";
import { ProfileSummary } from "@/features/org-profile/components/profile-summary";
import { ReadinessBanner } from "@/features/org-profile/components/readiness-banner";
import {
    describeProfileWriteFailure,
    useAnswerProfileQuestion,
    useApplyProfileDraft,
    useOrganizationProfile,
    useOrganizationProfileGaps,
    usePreviewProfileDraft,
    useReplaceOrganizationProfile,
    useStartMaterialRun,
} from "@/features/org-profile/hooks/use-organization-profile";
import type { ExtractedProfileDraft } from "@/features/org-profile/types/organization-profile";
import { clearHandedOverDraft, readHandedOverDraft } from "@/features/org-profile/utils/draft-handoff";
import {
    buildPatchForAnswer,
    countRemainingQuestions,
    selectVisibleQuestions,
    type ProfileAnswerDraft,
} from "@/features/org-profile/utils/interview-answers";
import { formatQuestionCount } from "@/features/org-profile/utils/russian-counts";

const EVENTUAL_CONSISTENCY_NOTE =
    "Изменения доходят до уроков и до ИИ-собеседника за несколько секунд.";

type ProfileScreenMode = "interview" | "form" | "draft";

/**
 * O8 · «Профиль компании» (docs/TENANCY/ADMIN_UI_DESIGN.md).
 *
 * The screen is an interview by default and a form only on request. That order is the feature: an
 * unfilled profile is not «неполные настройки», it is the state in which every lesson in the product
 * reads «ваш продукт» and 40.19's substitution does nothing at all — and thirty empty inputs are how
 * a profile stays unfilled. Three questions at a time, one `PATCH` per answer.
 */
export default function OrganizationProfilePage() {
    const router = useRouter();

    const profileQuery = useOrganizationProfile();
    const gapsQuery = useOrganizationProfileGaps();

    const answerQuestion = useAnswerProfileQuestion();
    const replaceProfile = useReplaceOrganizationProfile();
    const previewDraft = usePreviewProfileDraft();
    const applyDraft = useApplyProfileDraft();
    const startMaterialRun = useStartMaterialRun();

    const [mode, setMode] = useState<ProfileScreenMode>("interview");
    const [handedOverDraft, setHandedOverDraft] = useState<ExtractedProfileDraft | null>(null);
    const [skippedGapCodes, setSkippedGapCodes] = useState<string[]>([]);
    const [answeringGapCode, setAnsweringGapCode] = useState<string | null>(null);
    const [answerErrorsByGapCode, setAnswerErrorsByGapCode] = useState<Record<string, string>>({});
    const [isMaterialDialogOpen, setIsMaterialDialogOpen] = useState(false);
    const [materialRunError, setMaterialRunError] = useState<string | null>(null);
    const [formSaveError, setFormSaveError] = useState<string | null>(null);

    const previewDraftMutate = previewDraft.mutate;

    useEffect(() => {
        const draft = readHandedOverDraft();
        if (!draft) return;
        setHandedOverDraft(draft);
        setMode("draft");
        previewDraftMutate(draft);
    }, [previewDraftMutate]);

    const leaveDraftMode = () => {
        clearHandedOverDraft();
        setHandedOverDraft(null);
        previewDraft.reset();
        applyDraft.reset();
        setMode("interview");
    };

    const submitAnswer = async (gapCode: string, draft: ProfileAnswerDraft) => {
        setAnsweringGapCode(gapCode);
        setAnswerErrorsByGapCode((current) => {
            const next = { ...current };
            delete next[gapCode];
            return next;
        });

        try {
            await answerQuestion.mutateAsync(buildPatchForAnswer(gapCode, draft));
        } catch (error) {
            setAnswerErrorsByGapCode((current) => ({
                ...current,
                [gapCode]: describeProfileWriteFailure(error),
            }));
        } finally {
            setAnsweringGapCode(null);
        }
    };

    const isLoading = profileQuery.isLoading || gapsQuery.isLoading;
    const hasLoadFailed = profileQuery.isError || gapsQuery.isError;

    if (isLoading) {
        return (
            <>
                <PageHeader title="Профиль компании" />
                <div className="space-y-4 max-w-3xl">
                    <CardSkeleton lines={2} />
                    <CardSkeleton lines={6} />
                </div>
            </>
        );
    }

    if (hasLoadFailed) {
        return (
            <>
                <PageHeader title="Профиль компании" />
                <ErrorState
                    message="Не удалось загрузить профиль компании. Проверьте подключение и попробуйте снова."
                    onRetry={() => {
                        void profileQuery.refetch();
                        void gapsQuery.refetch();
                    }}
                />
            </>
        );
    }

    const profile = profileQuery.data ?? null;
    const gaps = gapsQuery.data;
    const visibleQuestions = selectVisibleQuestions(gaps, skippedGapCodes);
    const remainingQuestionCount = countRemainingQuestions(gaps, visibleQuestions.length);
    const isProfileComplete = (gaps?.totalGapCount ?? 0) === 0;

    if (mode === "draft") {
        return (
            <>
                <PageHeader
                    title="Что ИИ прочитал в ваших материалах"
                    subtitle="Ничего не изменится, пока вы не нажмёте «Применить»."
                    backHref="/org/profile"
                    backLabel="Профиль"
                />
                <div className="max-w-3xl">
                    <ProfileDraftPreview
                        preview={previewDraft.data ?? null}
                        isLoading={previewDraft.isPending}
                        loadError={
                            previewDraft.isError
                                ? describeProfileWriteFailure(previewDraft.error)
                                : null
                        }
                        isApplying={applyDraft.isPending}
                        applyError={
                            applyDraft.isError
                                ? describeProfileWriteFailure(applyDraft.error)
                                : null
                        }
                        onApply={(acceptedFields) => {
                            if (!handedOverDraft) return;
                            applyDraft.mutate(
                                { draft: handedOverDraft, acceptedFields },
                                { onSuccess: leaveDraftMode }
                            );
                        }}
                        onCancel={leaveDraftMode}
                    />
                    <p className="mt-4 text-xs text-ink-3">{EVENTUAL_CONSISTENCY_NOTE}</p>
                </div>
            </>
        );
    }

    if (mode === "form") {
        return (
            <>
                <PageHeader
                    title="Все поля профиля"
                    subtitle="Сохраняется целиком, одной кнопкой."
                    backHref="/org/profile"
                    backLabel="Профиль"
                />
                <div className="max-w-3xl">
                    <ProfileFullForm
                        profile={profile}
                        isSaving={replaceProfile.isPending}
                        saveError={formSaveError}
                        onSave={(request) => {
                            setFormSaveError(null);
                            replaceProfile.mutate(request, {
                                onSuccess: () => setMode("interview"),
                                onError: (error) =>
                                    setFormSaveError(describeProfileWriteFailure(error)),
                            });
                        }}
                        onClose={() => {
                            setFormSaveError(null);
                            setMode("interview");
                        }}
                    />
                    <p className="mt-4 text-xs text-ink-3">{EVENTUAL_CONSISTENCY_NOTE}</p>
                </div>
            </>
        );
    }

    return (
        <>
            <PageHeader
                title="Профиль компании"
                subtitle="Из него уроки и собеседник-ИИ берут ваш продукт, ваших клиентов и ваши запреты."
            />

            <div className="space-y-4 max-w-3xl">
                <ReadinessBanner
                    isReadyForParameterization={gaps?.isReadyForParameterization ?? false}
                    remainingOptionalGapCount={gaps?.totalGapCount ?? 0}
                />

                {isProfileComplete ? (
                    <Card>
                        <CardContent>
                            <h2 className="text-sm font-medium text-ink mb-3">Профиль заполнен</h2>
                            {profile ? (
                                <ProfileSummary profile={profile} />
                            ) : (
                                <p className="text-sm text-ink-3">
                                    Профиль сохранён, но его не удалось прочитать. Обновите страницу.
                                </p>
                            )}
                        </CardContent>
                    </Card>
                ) : visibleQuestions.length === 0 ? (
                    <Card>
                        <CardContent>
                            <p className="text-sm text-ink-3">
                                На сегодня вопросов не осталось. Остальное можно дописать в полной
                                форме.
                            </p>
                        </CardContent>
                    </Card>
                ) : (
                    <Card>
                        <CardContent>
                            <ul className="divide-y divide-line">
                                {visibleQuestions.map((gap) => (
                                    <InterviewQuestion
                                        key={gap.code}
                                        gap={gap}
                                        isSaving={answeringGapCode === gap.code}
                                        saveError={answerErrorsByGapCode[gap.code] ?? null}
                                        onAnswer={(draft) => submitAnswer(gap.code, draft)}
                                        onSkip={() =>
                                            setSkippedGapCodes((current) => [...current, gap.code])
                                        }
                                    />
                                ))}
                            </ul>
                            {remainingQuestionCount > 0 && (
                                <p className="mt-4 text-xs text-ink-3">
                                    Осталось ещё {formatQuestionCount(remainingQuestionCount)}.
                                </p>
                            )}
                        </CardContent>
                    </Card>
                )}

                <Card>
                    <CardContent className="flex flex-wrap items-center justify-between gap-3">
                        <p className="text-sm text-ink-3 max-w-md">
                            Быстрее: загрузите презентацию продукта и скрипт звонка — ИИ заполнит,
                            что сможет, и спросит только про пробелы.
                        </p>
                        <Button
                            variant="secondary"
                            iconRightName="arrow-right"
                            onClick={() => {
                                setMaterialRunError(null);
                                setIsMaterialDialogOpen(true);
                            }}
                        >
                            Заполнить по материалам
                        </Button>
                    </CardContent>
                </Card>

                <div className="flex flex-wrap items-center justify-between gap-3">
                    <Button variant="ghost" onClick={() => setMode("form")}>
                        Показать все поля профиля
                    </Button>
                    <p className="text-xs text-ink-3">{EVENTUAL_CONSISTENCY_NOTE}</p>
                </div>
            </div>

            <MaterialRunDialog
                open={isMaterialDialogOpen}
                isStarting={startMaterialRun.isPending}
                startError={materialRunError}
                onClose={() => setIsMaterialDialogOpen(false)}
                onStart={(title, material) => {
                    setMaterialRunError(null);
                    startMaterialRun.mutate(
                        { title, material },
                        {
                            onSuccess: (run) => {
                                setIsMaterialDialogOpen(false);
                                router.push(`/org/content/generation/${run.id}`);
                            },
                            onError: (error) =>
                                setMaterialRunError(describeProfileWriteFailure(error)),
                        }
                    );
                }}
            />
        </>
    );
}
