import { useMutation } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { toast } from "@/features/notifications/store/toast-store";

export interface ParsedCallLog {
    contactName: string | null;
    subject: string;
    outcome: string;
    occurredAt: string | null;
}

export function useParseCallLog(companyId: string | null) {
    return useMutation({
        mutationFn: (rawText: string) =>
            apiClient.post<ParsedCallLog>(`/companies/${companyId}/logs/parse`, { rawText }),
        onError: (error: Error) => {
            toast.error(`Не удалось распознать заметки: ${error.message}`);
        },
    });
}
