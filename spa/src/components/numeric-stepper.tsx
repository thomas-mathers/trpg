import { MinusIcon, PlusIcon } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';

interface NumericStepperProps {
  value: number;
  onChange: (value: number) => void;
  min?: number;
  max?: number;
  ariaLabel?: string;
  size?: 'default' | 'sm';
}

export function NumericStepper({
  value,
  onChange,
  min = 0,
  max = 99,
  ariaLabel,
  size = 'default',
}: NumericStepperProps) {
  const clamp = (next: number) => Math.min(max, Math.max(min, next));
  const buttonSize = size === 'sm' ? 'icon-sm' : 'icon';

  return (
    <div className="flex items-center gap-1.5">
      <Button
        type="button"
        variant="outline"
        size={buttonSize}
        aria-label={`Decrease ${ariaLabel ?? 'value'}`}
        onClick={() => onChange(clamp(value - 1))}
        disabled={value <= min}
      >
        <MinusIcon />
      </Button>
      <Input
        type="number"
        aria-label={ariaLabel}
        className={cn(
          'bg-card text-center [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none',
          size === 'sm' ? 'h-7 w-14' : 'w-16',
        )}
        value={value}
        onChange={(e) => {
          const parsed = Number.parseInt(e.target.value, 10);
          if (!Number.isNaN(parsed)) {
            onChange(clamp(parsed));
          }
        }}
        min={min}
        max={max}
      />
      <Button
        type="button"
        variant="outline"
        size={buttonSize}
        aria-label={`Increase ${ariaLabel ?? 'value'}`}
        onClick={() => onChange(clamp(value + 1))}
        disabled={value >= max}
      >
        <PlusIcon />
      </Button>
    </div>
  );
}
