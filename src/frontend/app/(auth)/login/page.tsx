"use client";

import Link from "next/link";
import { useState } from "react";
import { useLogin } from "@/features/auth/hooks/use-auth";
import { GoogleLoginButton } from "@/shared/components/google-login-button";
import { Wordmark } from "@/shared/components/wordmark";

export default function LoginPage() {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const loginMutation = useLogin();

    function handleSubmit(event: React.FormEvent) {
        event.preventDefault();
        loginMutation.mutate({ email, password });
    }

    return (
        <div className="auth">
            <div className="auth-card fade-up">
                <div className="auth-wordmark">
                    <Wordmark size={28} />
                </div>
                <h1 className="auth-heading">Welcome back</h1>
                <p className="auth-sub">Log in and keep building your skills</p>

                <form onSubmit={handleSubmit} className="col gap-3">
                    <input
                        type="email"
                        placeholder="Email"
                        value={email}
                        onChange={(event) => setEmail(event.target.value)}
                        required
                        className="field"
                    />
                    <input
                        type="password"
                        placeholder="Password"
                        value={password}
                        onChange={(event) => setPassword(event.target.value)}
                        required
                        className="field"
                    />

                    {loginMutation.isError && (
                        <p className="auth-error">
                            {loginMutation.error?.message ?? "Login failed"}
                        </p>
                    )}

                    <button
                        type="submit"
                        disabled={loginMutation.isPending}
                        className="btn btn-dark btn-block btn-lg"
                        style={{ marginTop: 4 }}
                    >
                        {loginMutation.isPending ? "Logging in..." : "Log in"}
                    </button>
                </form>

                <div className="auth-or">
                    <span>or</span>
                </div>

                <GoogleLoginButton />

                <p className="auth-footer" style={{ marginTop: 22 }}>
                    No account?{" "}
                    <Link href="/register">Sign up</Link>
                </p>
            </div>
        </div>
    );
}
