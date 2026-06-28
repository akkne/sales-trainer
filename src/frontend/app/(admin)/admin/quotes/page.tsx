"use client";

import { useMemo, useState } from "react";
import {
    useAdminDailyQuotes,
    useCreateDailyQuote,
    useUpdateDailyQuote,
    useDeleteDailyQuote,
    type AdminDailyQuote,
} from "@/features/admin/hooks/use-admin";

const MONTH_NAMES = [
    "January", "February", "March", "April", "May", "June",
    "July", "August", "September", "October", "November", "December",
];

const WEEKDAY_NAMES = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

function toIsoDate(year: number, month: number, day: number): string {
    return `${year}-${String(month + 1).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

interface CalendarCell {
    isoDate: string;
    dayNumber: number;
    isCurrentMonth: boolean;
}

function buildCalendarCells(year: number, month: number): CalendarCell[] {
    const firstOfMonth = new Date(year, month, 1);
    // Monday-first offset: JS getDay() is 0=Sunday.
    const leadingDayCount = (firstOfMonth.getDay() + 6) % 7;
    const gridStart = new Date(year, month, 1 - leadingDayCount);

    const cells: CalendarCell[] = [];
    for (let index = 0; index < 42; index++) {
        const cellDate = new Date(
            gridStart.getFullYear(),
            gridStart.getMonth(),
            gridStart.getDate() + index
        );
        cells.push({
            isoDate: toIsoDate(cellDate.getFullYear(), cellDate.getMonth(), cellDate.getDate()),
            dayNumber: cellDate.getDate(),
            isCurrentMonth: cellDate.getMonth() === month,
        });
    }
    return cells;
}

export default function AdminQuotesPage() {
    const now = new Date();
    const [viewYear, setViewYear] = useState(now.getFullYear());
    const [viewMonth, setViewMonth] = useState(now.getMonth());

    const [selectedDate, setSelectedDate] = useState<string | null>(null);
    const [formText, setFormText] = useState("");
    const [formAuthor, setFormAuthor] = useState("");
    const [errorMessage, setErrorMessage] = useState<string | null>(null);

    const cells = useMemo(() => buildCalendarCells(viewYear, viewMonth), [viewYear, viewMonth]);
    const rangeFrom = cells[0].isoDate;
    const rangeTo = cells[cells.length - 1].isoDate;

    const { data: quotes = [], isLoading } = useAdminDailyQuotes({ from: rangeFrom, to: rangeTo });
    const createQuote = useCreateDailyQuote();
    const updateQuote = useUpdateDailyQuote();
    const deleteQuote = useDeleteDailyQuote();

    const quotesByDate = useMemo(() => {
        const map = new Map<string, AdminDailyQuote>();
        for (const quote of quotes) map.set(quote.date, quote);
        return map;
    }, [quotes]);

    const todayIso = toIsoDate(now.getFullYear(), now.getMonth(), now.getDate());
    const selectedQuote = selectedDate ? quotesByDate.get(selectedDate) ?? null : null;

    function shiftMonth(delta: number) {
        const shifted = new Date(viewYear, viewMonth + delta, 1);
        setViewYear(shifted.getFullYear());
        setViewMonth(shifted.getMonth());
    }

    function selectDate(isoDate: string) {
        setSelectedDate(isoDate);
        setErrorMessage(null);
        const existing = quotesByDate.get(isoDate);
        setFormText(existing?.text ?? "");
        setFormAuthor(existing?.author ?? "");
    }

    async function handleSave() {
        if (!selectedDate || !formText.trim()) return;
        setErrorMessage(null);
        const body = { date: selectedDate, text: formText.trim(), author: formAuthor.trim() || null };
        try {
            if (selectedQuote) {
                await updateQuote.mutateAsync({ id: selectedQuote.id, body });
            } else {
                await createQuote.mutateAsync(body);
            }
        } catch (err) {
            setErrorMessage((err as Error).message);
        }
    }

    async function handleDelete() {
        if (!selectedQuote) return;
        setErrorMessage(null);
        try {
            await deleteQuote.mutateAsync(selectedQuote.id);
            setFormText("");
            setFormAuthor("");
        } catch (err) {
            setErrorMessage((err as Error).message);
        }
    }

    const isSaving = createQuote.isPending || updateQuote.isPending;

    return (
        <div>
            <div className="flex flex-wrap items-center justify-between gap-3 mb-6">
                <h1 className="text-xl font-semibold text-ink">Daily Quotes</h1>
                <div className="flex items-center gap-3">
                    <button
                        onClick={() => shiftMonth(-1)}
                        className="px-3 py-1.5 text-sm bg-bg-2 text-ink rounded-md hover:bg-surface-2 transition-colors"
                    >
                        ←
                    </button>
                    <span className="text-sm font-medium text-ink w-36 text-center">
                        {MONTH_NAMES[viewMonth]} {viewYear}
                    </span>
                    <button
                        onClick={() => shiftMonth(1)}
                        className="px-3 py-1.5 text-sm bg-bg-2 text-ink rounded-md hover:bg-surface-2 transition-colors"
                    >
                        →
                    </button>
                </div>
            </div>

            <div className="flex flex-col xl:flex-row gap-6 items-start">
                {/* Calendar */}
                <div className="bg-surface rounded-2xl border border-line p-5 flex-1 min-w-0 w-full">
                    <div className="grid grid-cols-7 gap-1 mb-1">
                        {WEEKDAY_NAMES.map((weekday) => (
                            <div key={weekday} className="text-center text-xs text-ink-3 py-1">
                                {weekday}
                            </div>
                        ))}
                    </div>
                    <div className="grid grid-cols-7 gap-1">
                        {cells.map((cell) => {
                            const quote = quotesByDate.get(cell.isoDate);
                            const isSelected = selectedDate === cell.isoDate;
                            const isToday = cell.isoDate === todayIso;
                            return (
                                <button
                                    key={cell.isoDate}
                                    onClick={() => selectDate(cell.isoDate)}
                                    className={`min-h-20 rounded-lg border p-1.5 text-left flex flex-col gap-1 transition-colors ${
                                        isSelected
                                            ? "border-indigo bg-indigo-soft"
                                            : "border-line hover:bg-bg-2"
                                    } ${cell.isCurrentMonth ? "" : "opacity-40"}`}
                                >
                                    <span
                                        className={`text-xs font-medium self-end ${
                                            isToday
                                                ? "bg-indigo text-white rounded-full w-5 h-5 flex items-center justify-center"
                                                : "text-ink-3"
                                        }`}
                                    >
                                        {cell.dayNumber}
                                    </span>
                                    {quote && (
                                        <span className="text-[11px] leading-tight text-ink-2 line-clamp-3 break-words">
                                            {quote.text}
                                        </span>
                                    )}
                                </button>
                            );
                        })}
                    </div>
                    {isLoading && <p className="text-sm text-ink-3 mt-3">Loading...</p>}
                </div>

                {/* Editor */}
                <div className="bg-surface rounded-2xl border border-line p-5 w-full xl:w-96 shrink-0">
                    {selectedDate ? (
                        <div className="space-y-4">
                            <h2 className="text-sm font-semibold text-ink">
                                {selectedQuote ? "Edit quote" : "New quote"} · {selectedDate}
                            </h2>
                            {errorMessage && (
                                <div className="bg-bad-soft text-bad rounded-lg px-3 py-2 text-sm">
                                    {errorMessage}
                                </div>
                            )}
                            <label className="block">
                                <span className="text-xs text-ink-3">Text</span>
                                <textarea
                                    rows={5}
                                    className="mt-1 w-full border border-line rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30 resize-y"
                                    value={formText}
                                    onChange={(e) => setFormText(e.target.value)}
                                    placeholder="Quote of the day..."
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-ink-3">Author</span>
                                <input
                                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                    value={formAuthor}
                                    onChange={(e) => setFormAuthor(e.target.value)}
                                    placeholder="Skeptic Sergey"
                                />
                            </label>
                            <div className="flex gap-3">
                                <button
                                    onClick={handleSave}
                                    disabled={isSaving || !formText.trim()}
                                    className="px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-50 transition-colors"
                                >
                                    {isSaving ? "Saving..." : selectedQuote ? "Save" : "Create"}
                                </button>
                                {selectedQuote && (
                                    <button
                                        onClick={handleDelete}
                                        disabled={deleteQuote.isPending}
                                        className="px-4 py-2 text-sm bg-bad-soft text-bad rounded-md hover:opacity-90 disabled:opacity-50 transition-colors"
                                    >
                                        {deleteQuote.isPending ? "Deleting..." : "Delete"}
                                    </button>
                                )}
                            </div>
                        </div>
                    ) : (
                        <p className="text-sm text-ink-3">
                            Select a day in the calendar to add or edit its quote.
                        </p>
                    )}
                </div>
            </div>
        </div>
    );
}
