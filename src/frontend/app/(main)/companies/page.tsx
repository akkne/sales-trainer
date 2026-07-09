"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Icon } from "@/shared/components/icon";
import { Skeleton, ErrorState } from "@/shared/components";
import { useCompanies, useCreateCompany } from "@/features/companies/hooks/use-companies";
import { CompanyListToolbar } from "@/features/companies/components/company-list-toolbar";
import { CompanyRow } from "@/features/companies/components/company-row";
import { CompanyModal } from "@/features/companies/components/company-modal";

export default function CompaniesPage() {
    const [search, setSearch] = useState("");
    const [isCreateOpen, setCreateOpen] = useState(false);
    const { data: companies, isLoading, error, refetch } = useCompanies(search);
    const createCompany = useCreateCompany();
    const router = useRouter();

    const handleCreate = (values: { name: string; description: string }) => {
        createCompany.mutate(values, {
            onSuccess: (company) => {
                setCreateOpen(false);
                router.push(`/companies/${company.id}`);
            },
        });
    };

    // ── Loading skeleton ──────────────────────────────────────────────────────
    if (isLoading) {
        return (
            <div className="page">
                <div className="container">
                    <div className="practice-header co-list-head">
                        <div>
                            <Skeleton width={120} height={20} />
                            <Skeleton width={280} height={14} style={{ marginTop: 6 }} />
                        </div>
                    </div>
                    <div className="co-list">
                        {[1, 2, 3, 4, 5].map((i) => (
                            <div key={i} className="co-row" style={{ pointerEvents: "none" }}>
                                <Skeleton width={40} height={40} rounded={11} />
                                <div className="co-row-body">
                                    <Skeleton width={160} height={13} style={{ marginBottom: 6 }} />
                                    <Skeleton width={220} height={11} />
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            </div>
        );
    }

    // ── Error state ───────────────────────────────────────────────────────────
    if (error) {
        return (
            <div className="page" style={{ padding: "60px 24px" }}>
                <ErrorState
                    title="Не удалось загрузить"
                    message={error.message}
                    onRetry={() => refetch()}
                    retryLabel="Повторить"
                />
            </div>
        );
    }

    const list = companies ?? [];
    const hasAnyCompanies = list.length > 0 || search.trim().length > 0;

    return (
        <div className="page">
            <div className="container">
                {/* ── Header ── */}
                <div className="practice-header co-list-head">
                    <div>
                        <h1 className="practice-title">Компании</h1>
                        <p className="practice-subtitle">
                            Ваши реальные клиенты: описание, тренировки перед звонком и журнал встреч
                        </p>
                    </div>
                    <button className="btn btn-primary" onClick={() => setCreateOpen(true)}>
                        <Icon name="plus" size={16} />
                        Добавить компанию
                    </button>
                </div>

                {hasAnyCompanies && (
                    <CompanyListToolbar search={search} onSearchChange={setSearch} count={list.length} />
                )}

                {/* ── List / empty states ── */}
                {list.length > 0 ? (
                    <div className="co-list" role="list">
                        {list.map((company) => (
                            <CompanyRow key={company.id} company={company} />
                        ))}
                    </div>
                ) : search.trim().length > 0 ? (
                    <div className="empty">
                        <p className="small">Ничего не найдено по запросу «{search.trim()}»</p>
                    </div>
                ) : (
                    <div className="empty">
                        <div className="ic">
                            <Icon name="briefcase" size="lg" />
                        </div>
                        <h3 className="h3" style={{ marginBottom: 8 }}>Пока нет ни одной компании</h3>
                        <p className="small">
                            Добавьте компанию, которой планируете позвонить — и потренируйтесь перед реальным разговором
                        </p>
                        <button
                            className="btn btn-primary"
                            style={{ marginTop: 20 }}
                            onClick={() => setCreateOpen(true)}
                        >
                            Добавить первую компанию
                        </button>
                    </div>
                )}

                <div style={{ height: 48 }} />
            </div>

            {isCreateOpen && (
                <CompanyModal
                    submitting={createCompany.isPending}
                    onSubmit={handleCreate}
                    onClose={() => setCreateOpen(false)}
                />
            )}
        </div>
    );
}
