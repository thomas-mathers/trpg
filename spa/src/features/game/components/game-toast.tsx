import type { LucideIcon } from 'lucide-react';
import { toast } from 'sonner';

interface GameToastProps {
  toastId: string | number;
  icon: LucideIcon;
  title: string;
  description: string;
}

export function GameToast({ toastId, icon: Icon, title, description }: GameToastProps) {
  return (
    <div className="border-border/80 bg-popover/95 shadow-foreground/10 flex w-full max-w-md gap-2.5 rounded-lg border px-3 py-2.5 shadow-lg backdrop-blur-sm">
      <Icon className="mt-0.5 size-4 shrink-0 text-amber-500" />
      <div className="min-w-0 flex-1">
        <p className="text-muted-foreground text-[10px] font-semibold tracking-wide uppercase">
          {title}
        </p>
        <p className="text-sm leading-snug">{description}</p>
      </div>
      <button
        type="button"
        className="text-muted-foreground hover:text-foreground text-xs"
        onClick={() => toast.dismiss(toastId)}
        aria-label="Dismiss notification"
      >
        ×
      </button>
    </div>
  );
}
