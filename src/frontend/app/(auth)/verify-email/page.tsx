"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import {
    readPendingVerificationEmail,
    useResendVerificationCode,
    useVerifyEmail,
} from "@/features/auth/hooks/use-auth";
import { ApiError } from "@/shared/api/api-client";
import { Wordmark } from "@/shared/components/wordmark";

const RESEND_COOLDOWN_SECONDS = 60;
const CODE_LENGTH = 6;

export default function VerifyEmailPage() {
    const [email, setEmail] = useState("");
    const [digits, setDigits] = useState<string[]>(Array(CODE_LENGTH).fill(""));
    const [cooldownSeconds, setCooldownSeconds] = useState(0);
    const inputRefs = useRef<Array<HTMLInputElement | null>>([]);
    const verifyEmailMutation = useVerifyEmail();
    const resendCodeMutation = useResendVerificationCode();

    const code = digits.join("");

    useEffect(() => {
        // eslint-disable-next-line react-hooks/set-state-in-effect -- sessionStorage is client-only; reading after mount avoids an SSR hydration mismatch
        setEmail(readPendingVerificationEmail());
    }, []);

    useEffect(() => {
        if (cooldownSeconds <= 0) return;
        const timeoutId = setTimeout(() => setCooldownSeconds((s) => s - 1), 1000);
        return () => clearTimeout(timeoutId);
    }, [cooldownSeconds]);

    function handleDigitChange(index: number, value: string) {
        const digit = value.replace(/\D/g, "").slice(-1);
        const next = [...digits];
        next[index] = digit;
        setDigits(next);
        if (digit && index < CODE_LENGTH - 1) {
            inputRefs.current[index + 1]?.focus();
        }
    }

    function handleDigitKeyDown(index: number, event: React.KeyboardEvent<HTMLInputElement>) {
        if (event.key === "Backspace" && !digits[index] && index > 0) {
            inputRefs.current[index - 1]?.focus();
        }
    }

    function handleDigitPaste(event: React.ClipboardEvent<HTMLInputElement>) {
        event.preventDefault();
        const pasted = event.clipboardData.getData("text").replace(/\D/g, "").slice(0, CODE_LENGTH);
        if (!pasted) return;
        const next = Array(CODE_LENGTH).fill("");
        for (let i = 0; i < pasted.length; i++) next[i] = pasted[i];
        setDigits(next);
        const focusIndex = Math.min(pasted.length, CODE_LENGTH - 1);
        inputRefs.current[focusIndex]?.focus();
    }

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
                <div className="auth-card fade-up">
                    <div className="auth-wordmark">
                        <Wordmark size={28} />
                    </div>
                    <h1 className="auth-heading">Подтверждение почты</h1>
                    <p className="auth-sub">
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
            <div className="auth-card fade-up">
                <div className="auth-wordmark">
                    <Wordmark size={28} />
                </div>
                <h1 className="auth-heading">Подтверди почту</h1>
                <p className="auth-sub">
                    Мы отправили код на{" "}
                    <span style={{ color: "var(--ink-2)", fontWeight: 600 }}>{email}</span>
                </p>

                <form onSubmit={handleSubmit}>
                    <div className="auth-code-row" role="group" aria-label="Код подтверждения">
                        {digits.map((d, i) => (
                            <input
                                key={i}
                                ref={(el) => { inputRefs.current[i] = el; }}
                                type="text"
                                inputMode="numeric"
                                autoComplete={i === 0 ? "one-time-code" : "off"}
                                maxLength={1}
                                value={d}
                                onChange={(e) => handleDigitChange(i, e.target.value)}
                                onKeyDown={(e) => handleDigitKeyDown(i, e)}
                                onPaste={i === 0 ? handleDigitPaste : undefined}
                                className={`auth-code-box${d ? " filled" : ""}`}
                                aria-label={`Цифра ${i + 1}`}
                            />
                        ))}
                    </div>

                    {verifyEmailMutation.isError && (
                        <p className="auth-error" style={{ textAlign: "center", marginTop: 10 }}>
                            {verifyEmailMutation.error?.message ?? "Неверный код"}
                        </p>
                    )}

                    <button
                        type="submit"
                        disabled={verifyEmailMutation.isPending || code.length < CODE_LENGTH}
                        className="btn btn-dark btn-block btn-lg"
                        style={{ marginTop: 20 }}
                    >
                        {verifyEmailMutation.isPending ? "Проверяем..." : "Подтвердить"}
                    </button>
                </form>

                <p className="auth-footer" style={{ marginTop: 22 }}>
                    Не пришёл код?{" "}
                    <button
                        type="button"
                        onClick={handleResend}
                        disabled={cooldownSeconds > 0 || resendCodeMutation.isPending}
                        className="auth-link"
                    >
                        {cooldownSeconds > 0
                            ? `Отправить ещё раз (${cooldownSeconds})`
                            : "Отправить ещё раз"}
                    </button>
                </p>

                <p className="auth-footer" style={{ marginTop: 10 }}>
                    <Link href="/login">Вернуться ко входу</Link>
                </p>
            </div>
        </div>
    );
}
