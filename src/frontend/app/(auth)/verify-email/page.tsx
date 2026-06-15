"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import {
    readPendingVerificationEmail,
    useResendVerificationCode,
    useVerifyEmail,
} from "@/features/auth/hooks/use-auth";
import { ApiError } from "@/shared/api/api-client";
import { Wordmark } from "@/shared/components/wordmark";

const RESEND_COOLDOWN_SECONDS = 60;

export default function VerifyEmailPage() {
    const [email, setEmail] = useState("");
    const [code, setCode] = useState("");
    const [cooldownSeconds, setCooldownSeconds] = useState(0);
    const verifyEmailMutation = useVerifyEmail();
    const resendCodeMutation = useResendVerificationCode();

    useEffect(() => {
        // eslint-disable-next-line react-hooks/set-state-in-effect -- sessionStorage is client-only; reading after mount avoids an SSR hydration mismatch
        setEmail(readPendingVerificationEmail());
    }, []);

    useEffect(() => {
        if (cooldownSeconds <= 0) return;
        const timeoutId = setTimeout(() => setCooldownSeconds((seconds) => seconds - 1), 1000);
        return () => clearTimeout(timeoutId);
    }, [cooldownSeconds]);

    function handleSubmit(event: React.FormEvent) {
        event.preventDefault();
        verifyEmailMutation.mutate({ email, code });
    }

    function handleResend() {
        resendCodeMutation.mutate(email, {
            onSuccess: () => setCooldownSeconds(RESEND_COOLDOWN_SECONDS),
            onError: (error) => {
                const retryAfterSeconds =
                    error instanceof ApiError &&
                    typeof error.payload.retryAfterSeconds === "number"
                        ? error.payload.retryAfterSeconds
                        : RESEND_COOLDOWN_SECONDS;
                setCooldownSeconds(retryAfterSeconds);
            },
        });
    }

    if (!email) {
        return (
            <div className="auth">
                <div className="app-backdrop" />
                <div className="auth-card card fade-up">
                    <div style={{ display: "flex", justifyContent: "center", marginBottom: 6 }}>
                        <Wordmark size={28} />
                    </div>
                    <h2 className="h2" style={{ textAlign: "center", margin: "16px 0 6px" }}>
                        Подтверждение почты
                    </h2>
                    <p className="small" style={{ textAlign: "center", marginBottom: 26 }}>
                        Начни с регистрации — мы пришлём код на твою почту.
                    </p>
                    <Link href="/register" className="btn btn-dark btn-block btn-lg">
                        К регистрации
                    </Link>
                </div>
            </div>
        );
    }

    return (
        <div className="auth">
            <div className="app-backdrop" />
            <div className="auth-card card fade-up">
                <div style={{ display: "flex", justifyContent: "center", marginBottom: 6 }}>
                    <Wordmark size={28} />
                </div>
                <h2 className="h2" style={{ textAlign: "center", margin: "16px 0 6px" }}>
                    Подтверди почту
                </h2>
                <p className="small" style={{ textAlign: "center", marginBottom: 26 }}>
                    Мы отправили код на {email}
                </p>

                <form onSubmit={handleSubmit} className="col gap-3">
                    <input
                        type="text"
                        inputMode="numeric"
                        autoComplete="one-time-code"
                        placeholder="Код из письма"
                        value={code}
                        onChange={(event) => setCode(event.target.value.replace(/\D/g, ""))}
                        required
                        maxLength={6}
                        className="field"
                    />

                    {verifyEmailMutation.isError && (
                        <p className="small" style={{ color: "var(--heart)" }}>
                            {verifyEmailMutation.error?.message ?? "Неверный код"}
                        </p>
                    )}

                    <button
                        type="submit"
                        disabled={verifyEmailMutation.isPending}
                        className="btn btn-dark btn-block btn-lg"
                        style={{ marginTop: 6 }}
                    >
                        {verifyEmailMutation.isPending ? "Проверяем..." : "Подтвердить"}
                    </button>
                </form>

                <p className="small" style={{ textAlign: "center", marginTop: 22 }}>
                    Не пришёл код?{" "}
                    <button
                        type="button"
                        onClick={handleResend}
                        disabled={cooldownSeconds > 0 || resendCodeMutation.isPending}
                        className="link-button"
                        style={{
                            color: "var(--primary)",
                            opacity: cooldownSeconds > 0 ? 0.5 : 1,
                            fontWeight: 700,
                            background: "none",
                            border: "none",
                            cursor: cooldownSeconds > 0 ? "default" : "pointer",
                            padding: 0,
                        }}
                    >
                        {cooldownSeconds > 0
                            ? `Отправить ещё раз (${cooldownSeconds})`
                            : "Отправить ещё раз"}
                    </button>
                </p>

                <p className="small" style={{ textAlign: "center", marginTop: 10 }}>
                    <Link href="/login" style={{ color: "var(--primary)", fontWeight: 700 }}>
                        Вернуться ко входу
                    </Link>
                </p>
            </div>
        </div>
    );
}
