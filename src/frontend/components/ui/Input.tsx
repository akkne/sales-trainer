"use client";

import { forwardRef, InputHTMLAttributes, TextareaHTMLAttributes, SelectHTMLAttributes, ReactNode } from "react";
import { Icon, IconName } from "./Icon";

/**
 * Base input wrapper with label and error support
 */
interface InputWrapperProps {
    label?: string;
    error?: string;
    hint?: string;
    required?: boolean;
    children: ReactNode;
    className?: string;
}

export function InputWrapper({
    label,
    error,
    hint,
    required,
    children,
    className = "",
}: InputWrapperProps) {
    return (
        <div className={`flex flex-col gap-1.5 ${className}`}>
            {label && (
                <label className="text-sm font-medium text-ink">
                    {label}
                    {required && <span className="text-bad ml-0.5">*</span>}
                </label>
            )}
            {children}
            {hint && !error && (
                <p className="text-xs text-ink-3">{hint}</p>
            )}
            {error && (
                <p className="text-xs text-bad flex items-center gap-1">
                    <Icon name="warning" size="sm" />
                    {error}
                </p>
            )}
        </div>
    );
}

/**
 * Text Input component
 */
interface TextInputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "size"> {
    /** Left icon */
    iconLeft?: IconName;
    /** Right icon */
    iconRight?: IconName;
    /** Input label */
    label?: string;
    /** Error message */
    error?: string;
    /** Hint text */
    hint?: string;
    /** Size variant */
    inputSize?: "sm" | "md" | "lg";
}

const INPUT_SIZE_CLASSES = {
    sm: "py-2 px-3 text-sm",
    md: "py-3 px-4 text-base",
    lg: "py-4 px-5 text-base",
};

export const TextInput = forwardRef<HTMLInputElement, TextInputProps>(
    (
        {
            iconLeft,
            iconRight,
            label,
            error,
            hint,
            required,
            inputSize = "md",
            className = "",
            ...props
        },
        ref
    ) => {
        const hasError = !!error;
        const paddingLeft = iconLeft ? "pl-11" : "";
        const paddingRight = iconRight ? "pr-11" : "";

        const input = (
            <div className="relative">
                {iconLeft && (
                    <span className="absolute left-3 top-1/2 -translate-y-1/2 text-ink-3">
                        <Icon name={iconLeft} size="md" />
                    </span>
                )}
                <input
                    ref={ref}
                    required={required}
                    className={`
                        w-full rounded-xl bg-surface text-ink
                        placeholder:text-ink-4
                        border-2 border-transparent
                        focus:outline-none focus:border-indigo focus:ring-2 focus:ring-indigo/30/20
                        disabled:opacity-60 disabled:cursor-not-allowed
                        transition-colors
                        ${INPUT_SIZE_CLASSES[inputSize]}
                        ${paddingLeft}
                        ${paddingRight}
                        ${hasError ? "border-bad focus:border-bad focus:ring-bad/30/20" : ""}
                        ${className}
                    `.trim()}
                    {...props}
                />
                {iconRight && (
                    <span className="absolute right-3 top-1/2 -translate-y-1/2 text-ink-3">
                        <Icon name={iconRight} size="md" />
                    </span>
                )}
            </div>
        );

        if (label || error || hint) {
            return (
                <InputWrapper label={label} error={error} hint={hint} required={required}>
                    {input}
                </InputWrapper>
            );
        }

        return input;
    }
);

TextInput.displayName = "TextInput";

/**
 * Search Input with search icon
 */
interface SearchInputProps extends Omit<TextInputProps, "iconLeft" | "type"> {
    /** Show clear button when value exists */
    onClear?: () => void;
}

export const SearchInput = forwardRef<HTMLInputElement, SearchInputProps>(
    ({ onClear, value, ...props }, ref) => {
        return (
            <div className="relative">
                <TextInput
                    ref={ref}
                    type="search"
                    iconLeft="search"
                    value={value}
                    className="rounded-full pr-10"
                    {...props}
                />
                {onClear && value && (
                    <button
                        type="button"
                        onClick={onClear}
                        className="absolute right-3 top-1/2 -translate-y-1/2 p-1 rounded-full hover:bg-bg-2 transition-colors text-ink-3"
                    >
                        <Icon name="close" size="sm" />
                    </button>
                )}
            </div>
        );
    }
);

SearchInput.displayName = "SearchInput";

/**
 * Textarea component
 */
interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
    /** Input label */
    label?: string;
    /** Error message */
    error?: string;
    /** Hint text */
    hint?: string;
    /** Size variant */
    inputSize?: "sm" | "md" | "lg";
}

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(
    (
        {
            label,
            error,
            hint,
            required,
            inputSize = "md",
            className = "",
            ...props
        },
        ref
    ) => {
        const hasError = !!error;

        const textarea = (
            <textarea
                ref={ref}
                required={required}
                className={`
                    w-full rounded-xl bg-surface text-ink
                    placeholder:text-ink-4
                    border-2 border-transparent
                    focus:outline-none focus:border-indigo focus:ring-2 focus:ring-indigo/30/20
                    disabled:opacity-60 disabled:cursor-not-allowed
                    transition-colors resize-y min-h-[100px]
                    ${INPUT_SIZE_CLASSES[inputSize]}
                    ${hasError ? "border-bad focus:border-bad focus:ring-bad/30/20" : ""}
                    ${className}
                `.trim()}
                {...props}
            />
        );

        if (label || error || hint) {
            return (
                <InputWrapper label={label} error={error} hint={hint} required={required}>
                    {textarea}
                </InputWrapper>
            );
        }

        return textarea;
    }
);

Textarea.displayName = "Textarea";

/**
 * Select component
 */
interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
    /** Input label */
    label?: string;
    /** Error message */
    error?: string;
    /** Hint text */
    hint?: string;
    /** Size variant */
    inputSize?: "sm" | "md" | "lg";
    /** Options */
    children: ReactNode;
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(
    (
        {
            label,
            error,
            hint,
            required,
            inputSize = "md",
            className = "",
            children,
            ...props
        },
        ref
    ) => {
        const hasError = !!error;

        const select = (
            <div className="relative">
                <select
                    ref={ref}
                    required={required}
                    className={`
                        w-full rounded-xl bg-surface text-ink
                        border-2 border-transparent appearance-none
                        focus:outline-none focus:border-indigo focus:ring-2 focus:ring-indigo/30/20
                        disabled:opacity-60 disabled:cursor-not-allowed
                        transition-colors pr-10
                        ${INPUT_SIZE_CLASSES[inputSize]}
                        ${hasError ? "border-bad focus:border-bad focus:ring-bad/30/20" : ""}
                        ${className}
                    `.trim()}
                    {...props}
                >
                    {children}
                </select>
                <span className="absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none text-ink-3">
                    <Icon name="chevron-down" size="md" />
                </span>
            </div>
        );

        if (label || error || hint) {
            return (
                <InputWrapper label={label} error={error} hint={hint} required={required}>
                    {select}
                </InputWrapper>
            );
        }

        return select;
    }
);

Select.displayName = "Select";

/**
 * Toggle Switch component
 */
interface ToggleProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "type"> {
    /** Label text */
    label?: string;
    /** Description text below label */
    description?: string;
}

export const Toggle = forwardRef<HTMLInputElement, ToggleProps>(
    ({ label, description, className = "", ...props }, ref) => {
        return (
            <label className={`flex items-start gap-3 cursor-pointer ${className}`}>
                <div className="relative mt-0.5">
                    <input
                        ref={ref}
                        type="checkbox"
                        className="peer sr-only"
                        {...props}
                    />
                    <div className="w-11 h-6 bg-surface-2 rounded-full peer-checked:bg-ink transition-colors" />
                    <div className="absolute left-0.5 top-0.5 w-5 h-5 bg-surface rounded-full shadow transition-transform peer-checked:translate-x-5 peer-checked:bg-bg" />
                </div>
                {(label || description) && (
                    <div className="flex flex-col">
                        {label && (
                            <span className="text-sm font-medium text-ink">{label}</span>
                        )}
                        {description && (
                            <span className="text-xs text-ink-3">{description}</span>
                        )}
                    </div>
                )}
            </label>
        );
    }
);

Toggle.displayName = "Toggle";

/**
 * Checkbox component
 */
interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "type"> {
    /** Label text */
    label?: string;
}

export const Checkbox = forwardRef<HTMLInputElement, CheckboxProps>(
    ({ label, className = "", ...props }, ref) => {
        return (
            <label className={`flex items-center gap-2.5 cursor-pointer ${className}`}>
                <div className="relative">
                    <input
                        ref={ref}
                        type="checkbox"
                        className="peer sr-only"
                        {...props}
                    />
                    <div className="w-5 h-5 rounded-md bg-surface-2 border-2 border-line peer-checked:bg-ink peer-checked:border-indigo transition-colors flex items-center justify-center">
                        <Icon
                            name="check"
                            size="sm"
                            className="text-bg opacity-0 peer-checked:opacity-100"
                        />
                    </div>
                </div>
                {label && (
                    <span className="text-sm text-ink">{label}</span>
                )}
            </label>
        );
    }
);

Checkbox.displayName = "Checkbox";
