import { apiClient } from "@/shared/api/api-client";

const CONTENT_GENERATION_ROUTE = "/admin/content-generation";

export interface StartedContentGenerationRun {
    id: string;
    title: string;
    status: string;
}

/**
 * «Заполнить по материалам» is not a second uploader. It starts an ordinary 40.27 pipeline run and
 * hands the РОП over to the checkpoint screen (O11), which is where the extracted structure can be
 * corrected before it is promoted into the profile — the same structure, reviewed once.
 *
 * The response carries far more than this; only the identifier is needed to navigate, and a screen
 * that declared the whole job document would have to follow that document's every change.
 */
export const profileMaterialRunService = {
    startRun(title: string, material: string): Promise<StartedContentGenerationRun> {
        return apiClient.post<StartedContentGenerationRun>(CONTENT_GENERATION_ROUTE, {
            title,
            material,
        });
    },
};
