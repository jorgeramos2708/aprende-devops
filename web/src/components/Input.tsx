import { InputHTMLAttributes, forwardRef } from 'react'
import clsx from 'clsx'

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string
  error?: string
  helperText?: string
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ className, label, error, helperText, id, ...props }, ref) => {
    const inputId = id || `input-${Math.random().toString(36).slice(2)}`
    const errorId = `${inputId}-error`
    const helperId = `${inputId}-helper`

    return (
      <div className="w-full">
        {label && (
          <label htmlFor={inputId} className="block text-sm font-medium text-dark-700 mb-1.5">
            {label}
          </label>
        )}
        <input
          ref={ref}
          id={inputId}
          aria-invalid={error ? 'true' : 'false'}
          aria-describedby={error ? errorId : helperText ? helperId : undefined}
          className={clsx(
            'w-full px-3 py-2 rounded-lg border bg-white text-dark-900 placeholder-dark-400 transition-all duration-200',
            'focus:outline-none focus:ring-2 focus:ring-offset-0',
            error
              ? 'border-red-500 focus:ring-red-500 focus:border-red-500'
              : 'border-dark-300 focus:ring-primary-500 focus:border-primary-500',
            'disabled:bg-dark-100 disabled:cursor-not-allowed',
            className
          )}
          {...props}
        />
        {error && (
          <p id={errorId} className="mt-1.5 text-sm text-red-600" role="alert">{error}</p>
        )}
        {helperText && !error && (
          <p id={helperId} className="mt-1.5 text-sm text-dark-500">{helperText}</p>
        )}
      </div>
    )
  }
)

Input.displayName = 'Input'
