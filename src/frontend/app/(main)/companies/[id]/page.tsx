"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { Icon } from "@/shared/components/icon";
import { Skeleton } from "@/shared/components";
import { ApiError } from "@/shared/api/api-client";
import {
    useCompany,
    useUpdateCompany,
    useUpdateCompanyStatus,
    useUpdateCompanyFollowUp,
    useDeleteCompany,
} from "@/features/companies/hooks/use-companies";
import type { CompanyStatus } from "@/features/companies/lib/company-status";
import {
    useCompanyLogs,
    useAddCallLog,
    useUpdateCallLog,
    useDeleteCallLog,
    type CallLogEntry,
    type CallLogPayload,
} from "@/features/companies/hooks/use-company-logs";
import { useCompanyPracticeCalls, useRecentGoals } from "@/features/companies/hooks/use-practice-calls";
import {
    useCompanyContacts,
    useAddCompanyContact,
    useUpdateCompanyContact,
    useDeleteCompanyContact,
    type CompanyContact,
    type CompanyContactPayload,
} from "@/features/companies/hooks/use-company-contacts";
import { CompanyHeader } from "@/features/companies/components/company-header";
import { CompanyDescriptionCard } from "@/features/companies/components/company-description-card";
import { CompanyFollowUpCard } from "@/features/companies/components/company-followup-card";
import { PrecallPanel } from "@/features/companies/components/precall-panel";
import { CompanyContactsCard } from "@/features/companies/components/company-contacts-card";
import { CompanyTimeline } from "@/features/companies/components/company-timeline";
import { CompanyModal } from "@/features/companies/components/company-modal";
import { CallLogModal } from "@/features/companies/components/call-log-modal";
import { ConfirmDeleteModal } from "@/features/companies/components/confirm-delete-modal";

export default function CompanyPage() {
    const params = useParams<{ id: string }>();
    const companyId = params.id;
    const router = useRouter();

    const { data: company, isLoading, error, refetch } = useCompany(companyId);
    const updateCompany = useUpdateCompany();
    const updateCompanyStatus = useUpdateCompanyStatus();
    const updateCompanyFollowUp = useUpdateCompanyFollowUp();
    const deleteCompany = useDeleteCompany();

    const { data: logs } = useCompanyLogs(companyId);
    const addCallLog = useAddCallLog(companyId);
    const updateCallLog = useUpdateCallLog(companyId);
    const deleteCallLog = useDeleteCallLog(companyId);

    const { data: practiceCalls } = useCompanyPracticeCalls(companyId);
    const { data: recentGoals } = useRecentGoals(companyId);

    const { data: contacts } = useCompanyContacts(companyId);
    const addContact = useAddCompanyContact(companyId);
    const updateContact = useUpdateCompanyContact(companyId);
    const deleteContact = useDeleteCompanyContact(companyId);

    const [isEditOpen, setEditOpen] = useState(false);
    const [isDeleteOpen, setDeleteOpen] = useState(false);
    const [isAddingLog, setAddingLog] = useState(false);
    const [editingLog, setEditingLog] = useState<CallLogEntry | null>(null);
    const [deletingLog, setDeletingLog] = useState<CallLogEntry | null>(null);
    const [isAddingContact, setAddingContact] = useState(false);
    const [editingContact, setEditingContact] = useState<CompanyContact | null>(null);
    const [deletingContact, setDeletingContact] = useState<CompanyContact | null>(null);

    if (isLoading) {
        return (
            <div className="co-page">
                <span className="back-link plain">
                    <Icon name="chevron-left" size={16} /> К списку
                </span>
                <div className="co-header">
                    <Skeleton width={64} height={64} rounded={16} />
                    <div className="co-header-body">
                        <Skeleton width={200} height={20} />
                        <Skeleton width={160} height={13} style={{ marginTop: 8 }} />
                    </div>
                </div>
                <Skeleton height={140} rounded={14} />
                <Skeleton height={220} rounded={14} />
            </div>
        );
    }

    const isNotFound = error instanceof ApiError && error.status === 404;
    if (error) {
        if (isNotFound) {
            return (
                <div className="page" style={{ padding: "60px 24px" }}>
                    <div className="empty">
                        <div className="ic">
                            <Icon name="warning" size="lg" />
                        </div>
                        <h3 className="h3" style={{ marginBottom: 8 }}>Компания не найдена</h3>
                        <p className="small">Возможно, она была удалена</p>
                        <Link href="/companies" className="btn btn-ghost" style={{ marginTop: 20 }}>
                            ← К списку
                        </Link>
                    </div>
                </div>
            );
        }

        return (
            <div className="page" style={{ padding: "60px 24px" }}>
                <div className="empty">
                    <div className="ic">
                        <Icon name="warning" size="lg" />
                    </div>
                    <h3 className="h3" style={{ marginBottom: 8 }}>Не удалось загрузить</h3>
                    <p className="small">{error.message}</p>
                    <button className="btn btn-primary" style={{ marginTop: 20 }} onClick={() => refetch()}>
                        Повторить
                    </button>
                </div>
            </div>
        );
    }

    if (!company) return null;

    const handleUpdate = (values: { name: string; description: string }) => {
        updateCompany.mutate(
            { id: companyId, ...values },
            { onSuccess: () => setEditOpen(false) }
        );
    };

    const handleDeleteCompany = () => {
        deleteCompany.mutate(companyId, {
            onSuccess: () => router.push("/companies"),
        });
    };

    const handleStatusChange = (status: CompanyStatus) => {
        updateCompanyStatus.mutate({ id: companyId, status });
    };

    const handleSaveDescription = (description: string) => {
        updateCompany.mutate({ id: companyId, name: company.name, description });
    };

    const handleSaveFollowUp = (nextActionAt: string | null, nextActionNote: string | null) => {
        updateCompanyFollowUp.mutate({ id: companyId, nextActionAt, nextActionNote });
    };

    const handleCall = (goal: string) => {
        if (typeof window !== "undefined") {
            sessionStorage.setItem(`company-call-goal:${companyId}`, goal);
        }
        router.push(`/companies/${companyId}/call/voice?goal=${encodeURIComponent(goal)}`);
    };

    const handleChat = (goal: string) => {
        if (typeof window !== "undefined") {
            sessionStorage.setItem(`company-call-goal:${companyId}`, goal);
        }
        router.push(`/companies/${companyId}/call/chat?goal=${encodeURIComponent(goal)}`);
    };

    const handleAddLog = (payload: CallLogPayload) => {
        addCallLog.mutate(payload, { onSuccess: () => setAddingLog(false) });
    };

    const handleUpdateLog = (payload: CallLogPayload) => {
        if (!editingLog) return;
        updateCallLog.mutate(
            { logId: editingLog.id, ...payload },
            { onSuccess: () => setEditingLog(null) }
        );
    };

    const handleConfirmDeleteLog = () => {
        if (!deletingLog) return;
        deleteCallLog.mutate(deletingLog.id, { onSuccess: () => setDeletingLog(null) });
    };

    const handleAddContact = (payload: CompanyContactPayload) => {
        addContact.mutate(payload, { onSuccess: () => setAddingContact(false) });
    };

    const handleUpdateContact = (payload: CompanyContactPayload) => {
        if (!editingContact) return;
        updateContact.mutate(
            { contactId: editingContact.id, ...payload },
            { onSuccess: () => setEditingContact(null) }
        );
    };

    const handleConfirmDeleteContact = () => {
        if (!deletingContact) return;
        deleteContact.mutate(deletingContact.id, { onSuccess: () => setDeletingContact(null) });
    };

    return (
        <div className="co-page">
            <Link href="/companies" className="back-link plain">
                <Icon name="chevron-left" size={16} /> К списку
            </Link>

            <CompanyHeader
                company={company}
                onEdit={() => setEditOpen(true)}
                onDelete={() => setDeleteOpen(true)}
                onStatusChange={handleStatusChange}
                statusChangeSubmitting={updateCompanyStatus.isPending}
            />

            <PrecallPanel
                hasDescription={company.description.trim().length > 0}
                recentGoals={recentGoals ?? []}
                onCall={handleCall}
                onChat={handleChat}
            />

            <CompanyDescriptionCard
                description={company.description}
                submitting={updateCompany.isPending}
                onSave={handleSaveDescription}
            />

            <CompanyFollowUpCard
                nextActionAt={company.nextActionAt}
                nextActionNote={company.nextActionNote}
                submitting={updateCompanyFollowUp.isPending}
                onSave={handleSaveFollowUp}
            />

            <CompanyContactsCard
                contacts={contacts ?? []}
                addingContact={isAddingContact}
                addContactSubmitting={addContact.isPending}
                editingContact={editingContact}
                updateContactSubmitting={updateContact.isPending}
                onStartAddContact={() => setAddingContact(true)}
                onCancelAddContact={() => setAddingContact(false)}
                onAddContact={handleAddContact}
                onStartEditContact={setEditingContact}
                onCancelEditContact={() => setEditingContact(null)}
                onUpdateContact={handleUpdateContact}
                onDeleteContact={setDeletingContact}
            />

            <CompanyTimeline
                practiceCalls={practiceCalls ?? []}
                logs={logs ?? []}
                contacts={contacts ?? []}
                addingLog={isAddingLog}
                addLogSubmitting={addCallLog.isPending}
                onStartAddLog={() => setAddingLog(true)}
                onCancelAddLog={() => setAddingLog(false)}
                onAddLog={handleAddLog}
                onEditLog={setEditingLog}
                onDeleteLog={setDeletingLog}
            />

            {isEditOpen && (
                <CompanyModal
                    initial={{ name: company.name, description: company.description }}
                    submitting={updateCompany.isPending}
                    onSubmit={handleUpdate}
                    onClose={() => setEditOpen(false)}
                />
            )}

            {isDeleteOpen && (
                <ConfirmDeleteModal
                    title="Удалить компанию?"
                    body={`Компания «${company.name}», её описание, тренировки и журнал звонков будут удалены безвозвратно.`}
                    submitting={deleteCompany.isPending}
                    onConfirm={handleDeleteCompany}
                    onClose={() => setDeleteOpen(false)}
                />
            )}

            {editingLog && (
                <CallLogModal
                    initial={editingLog}
                    contacts={contacts ?? []}
                    submitting={updateCallLog.isPending}
                    onSubmit={handleUpdateLog}
                    onClose={() => setEditingLog(null)}
                />
            )}

            {deletingLog && (
                <ConfirmDeleteModal
                    title="Удалить запись?"
                    body="Запись о звонке будет удалена."
                    submitting={deleteCallLog.isPending}
                    onConfirm={handleConfirmDeleteLog}
                    onClose={() => setDeletingLog(null)}
                />
            )}

            {deletingContact && (
                <ConfirmDeleteModal
                    title="Удалить контакт?"
                    body={`Контакт «${deletingContact.name}» будет удалён. Записи о звонках с этим контактом сохранятся без привязки.`}
                    submitting={deleteContact.isPending}
                    onConfirm={handleConfirmDeleteContact}
                    onClose={() => setDeletingContact(null)}
                />
            )}
        </div>
    );
}
