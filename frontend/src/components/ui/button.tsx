// shadcn/ui manual-installation pattern (MIT), adapted to the existing application.
import type { ComponentProps } from 'react'
import { Slot } from '@radix-ui/react-slot'
import { cva } from 'class-variance-authority'
import type { VariantProps } from 'class-variance-authority'
import { cn } from '../../lib/utils'

// Outline changes must be immediate: animating outline-color can start a primary
// button's focus indicator at its white currentColor on a white form surface.
const buttonVariants = cva('inline-flex shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded-lg text-sm font-medium transition-[background-color,border-color,color,box-shadow] disabled:pointer-events-none disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring [&_svg]:pointer-events-none [&_svg]:size-4', {
  variants: {
    variant: { default: 'bg-primary text-primary-foreground shadow-xs hover:bg-primary/90', destructive: 'bg-destructive text-white hover:bg-destructive/90',
      outline: 'border border-input bg-background shadow-xs hover:bg-accent hover:text-accent-foreground', secondary: 'bg-secondary text-secondary-foreground hover:bg-secondary/80',
      ghost: 'hover:bg-accent hover:text-accent-foreground', link: 'text-primary underline-offset-4 hover:underline' },
    size: { default: 'h-10 px-4 py-2', sm: 'h-9 px-3', lg: 'h-11 px-6', icon: 'size-10', 'icon-sm': 'size-9' }
  }, defaultVariants: { variant: 'default', size: 'default' }
})
export function Button({ className, variant = 'default', size = 'default', asChild = false, type, ...props }:
  ComponentProps<'button'> & VariantProps<typeof buttonVariants> & { asChild?: boolean }) {
  const Component = asChild ? Slot : 'button'
  return <Component data-slot="button" data-variant={variant} data-size={size} type={asChild ? undefined : type ?? 'button'} className={cn(buttonVariants({ variant, size }), className)} {...props} />
}
