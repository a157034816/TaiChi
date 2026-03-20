import * as React from "react";

import { cn } from "@/lib/utils";

type ScrollAreaProps = React.HTMLAttributes<HTMLDivElement>;

interface ScrollBarProps extends React.HTMLAttributes<HTMLDivElement> {
  orientation?: "horizontal" | "vertical";
}

/**
 * Radix ScrollArea currently triggers a callback-ref update loop in this
 * React 19 / Next 16 stack, so the editor falls back to native scrolling while
 * keeping the same component API for callers.
 */
const ScrollArea = React.forwardRef<HTMLDivElement, ScrollAreaProps>(
  ({ className, children, ...props }, ref) => (
    <div
      ref={ref}
      className={cn(
        "relative overflow-auto overscroll-contain rounded-[inherit]",
        "[scrollbar-color:rgba(143,160,188,0.45)_transparent] [scrollbar-width:thin]",
        "[&::-webkit-scrollbar]:h-3 [&::-webkit-scrollbar]:w-3",
        "[&::-webkit-scrollbar-track]:bg-transparent",
        "[&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-border",
        "[&::-webkit-scrollbar-thumb]:border-[3px] [&::-webkit-scrollbar-thumb]:border-solid [&::-webkit-scrollbar-thumb]:border-transparent",
        "[&::-webkit-scrollbar-thumb]:bg-clip-padding",
        className,
      )}
      {...props}
    >
      {children}
    </div>
  ),
);
ScrollArea.displayName = "ScrollArea";

/**
 * Kept as a no-op compatibility export because callers import it from the
 * shared UI module, even though the editor now relies on native scrollbars.
 */
const ScrollBar = React.forwardRef<HTMLDivElement, ScrollBarProps>(
  ({ className, orientation = "vertical", ...props }, ref) => (
    <div
      ref={ref}
      aria-hidden="true"
      className={cn("hidden", className)}
      data-orientation={orientation}
      {...props}
    />
  ),
);
ScrollBar.displayName = "ScrollBar";

export { ScrollArea, ScrollBar };
