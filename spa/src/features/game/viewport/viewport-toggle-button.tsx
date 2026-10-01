import { BoxIcon } from 'lucide-react';

import { Toggle } from '@/components/ui/toggle';

interface ViewportToggleButtonProps {
  pressed: boolean;
  onPressedChange: (pressed: boolean) => void;
}

export function ViewportToggleButton({ pressed, onPressedChange }: ViewportToggleButtonProps) {
  return (
    <Toggle
      size="default"
      className="size-8 p-0"
      pressed={pressed}
      onPressedChange={onPressedChange}
      aria-label="3D viewport"
    >
      <BoxIcon />
    </Toggle>
  );
}
