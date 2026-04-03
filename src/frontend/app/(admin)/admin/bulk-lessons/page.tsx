"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

export default function BulkLessonsRedirect() {
    const router = useRouter();
    useEffect(() => { router.replace("/admin/content"); }, [router]);
    return null;
}
