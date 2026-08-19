"use client";

import Link from "next/link";
import { useState } from "react";
import type { FormEvent } from "react";
import { Icon } from "@/shared/components/icon";
import type { IconName } from "@/shared/components/icon";
import { Wordmark } from "@/shared/components/wordmark";
import { SALES_TEAM_SIZE_OPTIONS } from "@/features/demo/constants/sales-team-size-options";
import { describeDemoRequestFailure, useDemoRequest } from "@/features/demo/hooks/use-demo-request";
import type { SalesTeamSize } from "@/features/demo/types";

type StepTint = "primary" | "success" | "violet";

const NEXT_STEPS: { icon: IconName; tint: StepTint; title: string; description: string }[] = [
    {
        icon: "clock",
        tint: "primary",
        title: "Созвон-знакомство на 30 минут",
        description: "Обсудим ваши задачи и особенности процесса продаж.",
    },
    {
        icon: "target",
        tint: "success",
        title: "Разбор ваших сценариев продаж",
        description: "Покажем, как ИИ-тренажёр отрабатывает именно ваши кейсы.",
    },
    {
        icon: "sparkle",
        tint: "violet",
        title: "Доступ к пробному пространству",
        description: "Дадим команде попробовать платформу до принятия решения.",
    },
];

/**
 * Demo request page. Sits directly under the root layout, addressed to a company
 * decision-maker in the formal «вы»-form (same register as the organization panel),
 * unlike the informal «ты» used across the rest of the learner app.
 */
export default function DemoRequestPage() {
    const [fullName, setFullName] = useState("");
    const [workEmail, setWorkEmail] = useState("");
    const [phone, setPhone] = useState("");
    const [companyName, setCompanyName] = useState("");
    const [jobTitle, setJobTitle] = useState("");
    const [salesTeamSize, setSalesTeamSize] = useState<SalesTeamSize | "">("");
    const [comment, setComment] = useState("");
    const [consentGiven, setConsentGiven] = useState(false);
    const [marketingConsentGiven, setMarketingConsentGiven] = useState(false);
    const [website, setWebsite] = useState("");

    const demoRequestMutation = useDemoRequest();

    function handleSubmit(event: FormEvent) {
        event.preventDefault();
        if (!salesTeamSize) return;

        demoRequestMutation.mutate({
            fullName,
            workEmail,
            phone,
            companyName,
            jobTitle,
            salesTeamSize,
            comment,
            consentGiven,
            marketingConsentGiven,
            website,
        });
    }

    return (
        <div className="demo-page">
            <div className="app-backdrop" />
            <div className="demo-shell">
                <div className="demo-value">
                    <Link href="/" className="demo-back">
                        <Icon name="arrow-left" size={16} />
                        На главную
                    </Link>

                    <div className="demo-value-wordmark">
                        <Wordmark size={28} />
                    </div>

                    <h1 className="demo-heading">Посмотрите, как Sellevate прокачивает продажи</h1>
                    <p className="demo-lead">
                        Оставьте заявку — мы покажем платформу на примере ваших сценариев и
                        ответим на вопросы вашей команды.
                    </p>

                    <div className="demo-steps">
                        {NEXT_STEPS.map((step) => (
                            <div key={step.title} className="demo-step">
                                <span className={"itile " + step.tint}>
                                    <Icon name={step.icon} size={20} />
                                </span>
                                <div>
                                    <p className="h4" style={{ margin: "0 0 2px" }}>
                                        {step.title}
                                    </p>
                                    <p className="small">{step.description}</p>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>

                {demoRequestMutation.isSuccess ? (
                    <div className="card demo-form-card demo-success fade-up">
                        <span className="itile success demo-success-icon">
                            <Icon name="check" size={32} />
                        </span>
                        <h2 className="demo-success-heading">Отлично, мы с вами свяжемся</h2>
                        <p className="small demo-success-text">
                            Мы написали вашу заявку и свяжемся с вами по адресу{" "}
                            <strong>{workEmail}</strong> в течение одного рабочего дня.
                        </p>
                        <Link href="/" className="btn btn-outline btn-lg">
                            Вернуться на главную
                        </Link>
                    </div>
                ) : (
                    <form onSubmit={handleSubmit} className="card demo-form-card demo-form fade-up">
                        <label className="demo-label" htmlFor="demo-full-name">
                            Имя и фамилия
                        </label>
                        <input
                            id="demo-full-name"
                            type="text"
                            className="field"
                            maxLength={120}
                            required
                            value={fullName}
                            onChange={(event) => setFullName(event.target.value)}
                        />

                        <label className="demo-label" htmlFor="demo-work-email">
                            Рабочий email
                        </label>
                        <input
                            id="demo-work-email"
                            type="email"
                            className="field"
                            maxLength={200}
                            required
                            value={workEmail}
                            onChange={(event) => setWorkEmail(event.target.value)}
                        />

                        <label className="demo-label" htmlFor="demo-phone">
                            Телефон
                        </label>
                        <input
                            id="demo-phone"
                            type="tel"
                            className="field"
                            maxLength={40}
                            value={phone}
                            onChange={(event) => setPhone(event.target.value)}
                        />

                        <label className="demo-label" htmlFor="demo-company-name">
                            Компания
                        </label>
                        <input
                            id="demo-company-name"
                            type="text"
                            className="field"
                            maxLength={200}
                            required
                            value={companyName}
                            onChange={(event) => setCompanyName(event.target.value)}
                        />

                        <label className="demo-label" htmlFor="demo-job-title">
                            Должность
                        </label>
                        <input
                            id="demo-job-title"
                            type="text"
                            className="field"
                            maxLength={120}
                            value={jobTitle}
                            onChange={(event) => setJobTitle(event.target.value)}
                        />

                        <label className="demo-label" htmlFor="demo-sales-team-size">
                            Размер отдела продаж
                        </label>
                        <select
                            id="demo-sales-team-size"
                            className="field"
                            required
                            value={salesTeamSize}
                            onChange={(event) =>
                                setSalesTeamSize(event.target.value as SalesTeamSize)
                            }
                        >
                            <option value="" disabled>
                                Выберите размер команды
                            </option>
                            {SALES_TEAM_SIZE_OPTIONS.map((option) => (
                                <option key={option.value} value={option.value}>
                                    {option.label}
                                </option>
                            ))}
                        </select>

                        <label className="demo-label" htmlFor="demo-comment">
                            Комментарий
                        </label>
                        <textarea
                            id="demo-comment"
                            className="field co-textarea"
                            maxLength={2000}
                            value={comment}
                            onChange={(event) => setComment(event.target.value)}
                        />

                        <div className="demo-honeypot" aria-hidden="true">
                            <label htmlFor="demo-website">Не заполняйте это поле</label>
                            <input
                                id="demo-website"
                                name="website"
                                type="text"
                                tabIndex={-1}
                                autoComplete="off"
                                value={website}
                                onChange={(event) => setWebsite(event.target.value)}
                            />
                        </div>

                        <label className="demo-consent">
                            <input
                                type="checkbox"
                                className="demo-checkbox"
                                required
                                checked={consentGiven}
                                onChange={(event) => setConsentGiven(event.target.checked)}
                            />
                            <span>
                                Даю согласие на обработку персональных данных в соответствии с
                                политикой конфиденциальности.
                            </span>
                        </label>

                        <label className="demo-consent">
                            <input
                                type="checkbox"
                                className="demo-checkbox"
                                checked={marketingConsentGiven}
                                onChange={(event) =>
                                    setMarketingConsentGiven(event.target.checked)
                                }
                            />
                            <span>
                                Согласен получать информационные и рекламные материалы.
                            </span>
                        </label>

                        {demoRequestMutation.isError && (
                            <p className="auth-error">
                                {describeDemoRequestFailure(demoRequestMutation.error)}
                            </p>
                        )}

                        <button
                            type="submit"
                            className="btn btn-primary btn-lg btn-block"
                            disabled={demoRequestMutation.isPending}
                        >
                            {demoRequestMutation.isPending ? "Отправляем..." : "Отправить заявку"}
                            {!demoRequestMutation.isPending && <Icon name="send" size={16} />}
                        </button>
                    </form>
                )}
            </div>
        </div>
    );
}
