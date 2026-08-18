"use client";

import { ReactNode } from "react";
import { Button } from "./button";
import { Modal } from "./modal";

export type ConfirmTone = "default" | "danger";

interface ConfirmDialogProps {
    open: boolean;
    title: string;
    body: ReactNode;
    confirmLabel: string;
    tone?: ConfirmTone;
    onConfirm: () => void;
    onCancel: () => void;
    isPending?: boolean;
}

/**
 * The one-question dialog for the organization panel's irreversible verbs — «Взять базу»,
 * «Отключить человека», «Закрыть задание», «Опубликовать».
 *
 * The confirming button is the only primary on screen while it is open, and it carries the verb
 * rather than «ОК»: a person who reads nothing but the button must still know what they agreed to.
 */
export function ConfirmDialog({
    open,
    title,
    body,
    confirmLabel,
    tone = "default",
    onConfirm,
    onCancel,
    isPending = false,
}: ConfirmDialogProps) {
    return (
        <Modal
            open={open}
            onClose={isPending ? () => {} : onCancel}
            title={title}
            size="sm"
            footer={
                <>
                    <Button variant="ghost" onClick={onCancel} disabled={isPending}>
                        Отмена
                    </Button>
                    <Button
                        variant={tone === "danger" ? "destructive" : "primary"}
                        onClick={onConfirm}
                        loading={isPending}
                    >
                        {confirmLabel}
                    </Button>
                </>
            }
        >
            {body}
        </Modal>
    );
}
