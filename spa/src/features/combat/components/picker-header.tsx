import { ChevronLeft } from 'lucide-react';

interface PickerHeaderProps {
  onBack: () => void;
  title: string;
  className?: string;
}

export function PickerHeader({ onBack, title, className }: PickerHeaderProps) {
  return (
    <div className={`flex items-center gap-2 px-0.5 ${className ?? ''}`}>
      <button
        type="button"
        onClick={onBack}
        className="text-muted-foreground hover:text-foreground flex items-center gap-1 text-xs"
      >
        <ChevronLeft className="h-3.5 w-3.5" /> Back
      </button>
      <span className="text-muted-foreground text-xs">{title}</span>
    </div>
  );
}
