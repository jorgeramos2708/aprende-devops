import { HTMLAttributes, forwardRef } from 'react'
import clsx from 'clsx'

interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  variant?: 'primary' | 'success' | 'warning' | 'danger' | 'dark' | 'outline'
  size?: 'sm' | 'md'
}

export const Badge = forwardRef<HTMLSpanElement, BadgeProps>(
  ({ className, variant = 'primary', size = 'md', children, ...props }, ref) => {
    return (
      <span
        ref={ref}
        className={clsx(
          'inline-flex items-center px-2 py-0.5 rounded-full font-medium',
          {
            'text-xs': size === 'sm',
            'text-sm': size === 'md',
            'bg-primary-100 text-primary-800': variant === 'primary',
            'bg-green-100 text-green-800': variant === 'success',
            'bg-yellow-100 text-yellow-800': variant === 'warning',
            'bg-red-100 text-red-800': variant === 'danger',
            'bg-dark-100 text-dark-800': variant === 'dark',
            'border border-dark-300 text-dark-700 bg-transparent': variant === 'outline',
          },
          className
        )}
        {...props}
      >
        {children}
      </span>
    )
  }
)

Badge.displayName = 'Badge'
