"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useAuthStore } from "@/shared/stores/auth-store";
import { Icon } from "@/shared/components/icon";
import type { IconName } from "@/shared/components/icon";
import { Wordmark } from "@/shared/components/wordmark";

type FeatureTint = "primary" | "success" | "violet" | "amber";

const FEATURE_LIST: {
    icon: IconName;
    tint: FeatureTint;
    title: string;
    description: string;
}[] = [
    {
        icon: "phone",
        tint: "primary",
        title: "Real scenarios",
        description: "The AI prospect argues, doubts, and interrupts — just like a real decision-maker.",
    },
    {
        icon: "mic",
        tint: "success",
        title: "Voice calls",
        description: "Practice cold calls by voice and get a breakdown of every line.",
    },
    {
        icon: "zap",
        tint: "violet",
        title: "XP, streaks & leagues",
        description: "Game mechanics keep you sharp and turn skills into muscle memory.",
    },
    {
        icon: "book",
        tint: "amber",
        title: "Technique guidebook",
        description: "SPIN, price anchoring, objection handling — with examples.",
    },
];

export default function LandingPage() {
    const router = useRouter();
    const { accessToken } = useAuthStore();

    useEffect(() => {
        if (accessToken) {
            router.replace("/tree");
        }
    }, [accessToken, router]);

    return (
        <div className="landing">
            <div className="app-backdrop" />
            <header className="land-top container">
                <Wordmark size={28} />
                <Link href="/login" className="btn btn-ghost">
                    Log in
                </Link>
            </header>

            <div className="container land-hero">
                <span
                    className="badge"
                    style={{
                        background: "var(--primary-soft)",
                        color: "var(--primary)",
                        padding: "7px 14px",
                        fontSize: 13,
                    }}
                >
                    <Icon name="bolt" size={15} />
                    Next-generation sales trainer
                </span>

                <h1 className="display land-title">
                    Level up your sales
                    <br />
                    <span className="grad-text">in 5 minutes</span> a day
                </h1>

                <p
                    className="lead"
                    style={{ maxWidth: 560, margin: "0 auto 32px", textWrap: "pretty" }}
                >
                    Learn from real AI dialogues, practice voice calls, and climb
                    the leagues alongside other sales reps.
                </p>

                <div className="row gap-3 center wrap">
                    <Link href="/register" className="btn btn-dark btn-lg">
                        Get started free
                        <Icon name="arrow-right" size={18} />
                    </Link>
                </div>

                <div className="land-features">
                    {FEATURE_LIST.map((f) => (
                        <div key={f.title} className="card card-pad lift land-feat">
                            <span className={"itile " + f.tint} style={{ width: 50, height: 50 }}>
                                <Icon name={f.icon} size={26} />
                            </span>
                            <h4 className="h4" style={{ margin: "16px 0 8px" }}>
                                {f.title}
                            </h4>
                            <p className="small" style={{ textWrap: "pretty" }}>
                                {f.description}
                            </p>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
}
