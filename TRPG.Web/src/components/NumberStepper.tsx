import { MinusIcon, PlusIcon } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

interface NumberStepperProps {
  value: number;
  onChange: (value: number) => void;
  min?: number;
  max?: number;
}

export function NumberStepper({ value, onChange, min = 0, max = 99 }: NumberStepperProps) {
  const clamp = (next: number) => Math.min(max, Math.max(min, next));

  return (
    <div className="flex items-center gap-2">
      <Button
        type="button"
        variant="outline"
        size="icon"
        onClick={() => onChange(clamp(value - 1))}
        disabled={value <= min}
      >
        <MinusIcon />
      </Button>
      <Input
        type="number"
        className="w-16 text-center"
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
        size="icon"
        onClick={() => onChange(clamp(value + 1))}
        disabled={value >= max}
      >
        <PlusIcon />
      </Button>
    </div>
  );
}
