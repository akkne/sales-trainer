"use client";

import { useRouter } from "next/navigation";
import { Button } from "@/shared/components/button";
import { EmptyState } from "@/shared/components/empty-state";

/**
 * State O0 — platform staff with no membership opened the organization panel
 * (docs/TENANCY/ADMIN_UI_DESIGN.md §1.3).
 *
 * This is not an error and not an empty table. Every screen behind it shows one company's data,
 * and a platform role is deliberately not bound to a company; the way in is impersonation from the
 * registry, which is logged. Recognised by `orgId == null` rather than by a 403, because the 403
 * arrives per request and far too late to explain anything.
 */
export function NoOrganizationState() {
    const router = useRouter();

    return (
        <div className="mx-auto max-w-xl">
            <EmptyState
                icon="briefcase"
                title="Панель организации открывается изнутри"
                description="Эти экраны показывают данные одной компании. У вашей учётной записи нет членства ни в одной — так и задумано: платформенные роли не привязаны к организациям. Чтобы посмотреть панель конкретного заказчика, войдите в его организацию из реестра. Вход записывается в журнал."
                action={
                    <Button onClick={() => router.push("/admin/organizations")}>
                        Открыть реестр организаций
                    </Button>
                }
            />
        </div>
    );
}
