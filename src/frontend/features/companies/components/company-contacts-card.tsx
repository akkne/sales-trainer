"use client";

import { useState } from "react";
import { Icon } from "@/shared/components/icon";
import { ContactForm } from "@/features/companies/components/contact-form";
import type { CompanyContact, CompanyContactPayload } from "@/features/companies/hooks/use-company-contacts";

interface CompanyContactsCardProps {
    contacts: CompanyContact[];
    addingContact: boolean;
    addContactSubmitting?: boolean;
    editingContact: CompanyContact | null;
    updateContactSubmitting?: boolean;
    onStartAddContact: () => void;
    onCancelAddContact: () => void;
    onAddContact: (payload: CompanyContactPayload) => void;
    onStartEditContact: (contact: CompanyContact) => void;
    onCancelEditContact: () => void;
    onUpdateContact: (payload: CompanyContactPayload) => void;
    onDeleteContact: (contact: CompanyContact) => void;
}

export function CompanyContactsCard({
    contacts,
    addingContact,
    addContactSubmitting = false,
    editingContact,
    updateContactSubmitting = false,
    onStartAddContact,
    onCancelAddContact,
    onAddContact,
    onStartEditContact,
    onCancelEditContact,
    onUpdateContact,
    onDeleteContact,
}: CompanyContactsCardProps) {
    const [expandedContactId, setExpandedContactId] = useState<string | null>(null);

    return (
        <div className="co-card">
            <div className="co-card-head">
                <span className="eyebrow">КОНТАКТЫ</span>
                {!addingContact && (
                    <button className="btn-link" onClick={onStartAddContact}>+ Добавить контакт</button>
                )}
            </div>

            {addingContact && (
                <ContactForm
                    submitting={addContactSubmitting}
                    onSubmit={onAddContact}
                    onCancel={onCancelAddContact}
                />
            )}

            {contacts.length > 0 ? (
                <div className="co-contact-list">
                    {contacts.map((contact) =>
                        editingContact?.id === contact.id ? (
                            <ContactForm
                                key={contact.id}
                                initial={contact}
                                submitting={updateContactSubmitting}
                                onSubmit={onUpdateContact}
                                onCancel={onCancelEditContact}
                            />
                        ) : (
                            <div className="co-contact-row" key={contact.id}>
                                <button
                                    type="button"
                                    className="co-contact-main"
                                    onClick={() =>
                                        setExpandedContactId(expandedContactId === contact.id ? null : contact.id)
                                    }
                                >
                                    <div className="co-contact-name">{contact.name}</div>
                                    {contact.position && (
                                        <div className="co-contact-position">{contact.position}</div>
                                    )}
                                </button>
                                <div className="co-contact-actions">
                                    <button
                                        className="icon-btn"
                                        onClick={() => onStartEditContact(contact)}
                                        aria-label="Редактировать контакт"
                                    >
                                        <Icon name="edit" size="sm" />
                                    </button>
                                    <button
                                        className="icon-btn"
                                        onClick={() => onDeleteContact(contact)}
                                        aria-label="Удалить контакт"
                                    >
                                        <Icon name="delete" size="sm" />
                                    </button>
                                </div>
                                {expandedContactId === contact.id && contact.notes && (
                                    <p className="co-contact-notes">{contact.notes}</p>
                                )}
                            </div>
                        )
                    )}
                </div>
            ) : (
                !addingContact && (
                    <div className="empty" style={{ padding: "24px 20px" }}>
                        <p className="small">Пока нет контактов — добавьте, с кем вы общаетесь в этой компании</p>
                    </div>
                )
            )}
        </div>
    );
}
