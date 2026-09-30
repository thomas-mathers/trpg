import type { IconType } from 'react-icons';

import { cn } from '@/lib/utils';

export interface StatBarProps {
  icon: IconType;
  colorClass: string;
  fillClass: string;
  current: number;
  max: number;
  delta?: number | null;
  hideDelta?: boolean;
  hideValue?: boolean;
}

export function StatBar({
  icon: Icon,
  colorClass,
  fillClass,
  current,
  max,
  delta = null,
  hideDelta = false,
  hideValue = false,
}: StatBarProps) {
  const pct = max > 0 ? Math.max(0, Math.min(100, Math.round((current / max) * 100))) : 0;

  return (
    <div
      className="relative mt-1 flex items-center gap-1.5 first:mt-0"
      title={hideValue ? `${current}/${max}` : undefined}
    >
      <Icon className={cn('h-[11px] w-[11px] shrink-0', colorClass)} />
      <span className="bg-muted h-[5px] flex-1 overflow-hidden rounded-full">
        <span
          className={cn(
            'block h-full rounded-full transition-[width] duration-500 ease-out',
            fillClass,
          )}
          style={{ width: `${pct}%` }}
        />
      </span>
      {!hideValue && (
        <span className="text-muted-foreground w-10 shrink-0 text-right text-[10px] tabular-nums">
          {current}/{max}
        </span>
      )}
      {!hideDelta && delta !== null && delta !== 0 && (
        <span
          className={cn(
            'combat-float-up pointer-events-none absolute -top-0.5 right-0 text-[13px] font-bold tabular-nums [text-shadow:0_1px_2px_rgba(0,0,0,0.5)]',
            delta > 0 ? 'text-heal' : colorClass,
          )}
        >
          {delta > 0 ? `+${delta}` : delta}
        </span>
      )}
    </div>
  );
}
