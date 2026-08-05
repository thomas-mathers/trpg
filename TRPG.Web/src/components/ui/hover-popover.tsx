import { Popover as PopoverPrimitive, Tooltip as TooltipPrimitive } from 'radix-ui';
import * as React from 'react';

import { cn } from '@/lib/utils';

function useCanHover(): boolean {
  const [canHover, setCanHover] = React.useState(
    () => window.matchMedia('(hover: hover) and (pointer: fine)').matches,
  );

  React.useEffect(() => {
    const query = window.matchMedia('(hover: hover) and (pointer: fine)');
    const listener = (event: MediaQueryListEvent) => setCanHover(event.matches);
    query.addEventListener('change', listener);
    return () => query.removeEventListener('change', listener);
  }, []);

  return canHover;
}

const CanHoverContext = React.createContext(false);

interface HoverPopoverProps {
  children: React.ReactNode;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
}

function HoverPopover({ children, open, onOpenChange }: HoverPopoverProps) {
  const canHover = useCanHover();
  return (
    <CanHoverContext.Provider value={canHover}>
      {canHover ? (
        <TooltipPrimitive.Root open={open} onOpenChange={onOpenChange} delayDuration={150}>
          {children}
        </TooltipPrimitive.Root>
      ) : (
        <PopoverPrimitive.Root open={open} onOpenChange={onOpenChange}>
          {children}
        </PopoverPrimitive.Root>
      )}
    </CanHoverContext.Provider>
  );
}

interface HoverPopoverTriggerProps {
  asChild?: boolean;
  children: React.ReactNode;
}

function HoverPopoverTrigger({ asChild, children }: HoverPopoverTriggerProps) {
  const canHover = React.useContext(CanHoverContext);
  return canHover ? (
    <TooltipPrimitive.Trigger asChild={asChild}>{children}</TooltipPrimitive.Trigger>
  ) : (
    <PopoverPrimitive.Trigger asChild={asChild}>{children}</PopoverPrimitive.Trigger>
  );
}

interface HoverPopoverTextTriggerProps {
  className?: string;
  children: React.ReactNode;
}

function HoverPopoverTextTrigger({ className, children }: HoverPopoverTextTriggerProps) {
  return (
    <HoverPopoverTrigger asChild>
      <button
        type="button"
        className={cn('cursor-help underline decoration-dotted underline-offset-2', className)}
      >
        {children}
      </button>
    </HoverPopoverTrigger>
  );
}

interface HoverPopoverContentProps {
  className?: string;
  side?: 'top' | 'right' | 'bottom' | 'left';
  align?: 'start' | 'center' | 'end';
  sideOffset?: number;
  children: React.ReactNode;
}

function HoverPopoverContent({
  className,
  side,
  align = 'center',
  sideOffset = 4,
  children,
}: HoverPopoverContentProps) {
  const canHover = React.useContext(CanHoverContext);
  const contentClassName = cn(
    'bg-foreground text-background z-50 w-fit max-w-64 rounded-md px-3 py-1.5 text-xs shadow-md outline-hidden data-closed:animate-out data-closed:fade-out-0 data-closed:zoom-out-95 data-open:animate-in data-open:fade-in-0 data-open:zoom-in-95 data-[state=delayed-open]:animate-in data-[state=delayed-open]:fade-in-0 data-[state=delayed-open]:zoom-in-95',
    className,
  );

  if (canHover) {
    return (
      <TooltipPrimitive.Portal>
        <TooltipPrimitive.Content
          side={side}
          align={align}
          sideOffset={sideOffset}
          className={contentClassName}
        >
          {children}
          <TooltipPrimitive.Arrow className="fill-foreground" width={11} height={5} />
        </TooltipPrimitive.Content>
      </TooltipPrimitive.Portal>
    );
  }

  return (
    <PopoverPrimitive.Portal>
      <PopoverPrimitive.Content
        side={side}
        align={align}
        sideOffset={sideOffset}
        className={contentClassName}
      >
        {children}
        <PopoverPrimitive.Arrow className="fill-foreground" width={11} height={5} />
      </PopoverPrimitive.Content>
    </PopoverPrimitive.Portal>
  );
}

export { HoverPopover, HoverPopoverContent, HoverPopoverTextTrigger, HoverPopoverTrigger };
