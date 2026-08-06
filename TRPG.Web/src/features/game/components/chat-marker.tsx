import { Separator } from '@/components/ui/separator';
import { cn } from '@/lib/utils';

export type ChatMarkerVariant =
  | 'location'
  | 'reconnecting'
  | 'reconnected'
  | 'disconnected'
  | 'combat-start'
  | 'combat-end';

interface ChatMarkerProps {
  text: string;
  variant?: ChatMarkerVariant;
}

const VARIANT_CLASSES: Record<ChatMarkerVariant, string> = {
  location: 'text-muted-foreground',
  reconnecting: 'text-amber-500',
  reconnected: 'text-green-500',
  disconnected: 'text-destructive',
  'combat-start': 'text-destructive',
  'combat-end': 'text-muted-foreground',
};

export function ChatMarker({ text, variant = 'location' }: ChatMarkerProps) {
  const colorClass = VARIANT_CLASSES[variant];

  return (
    <div className={cn('flex items-center gap-3 py-2 text-xs', colorClass)}>
      <Separator className={cn('flex-1 bg-current opacity-30', colorClass)} />
      <span className="shrink-0 font-medium">{text}</span>
      <Separator className={cn('flex-1 bg-current opacity-30', colorClass)} />
    </div>
  );
}
