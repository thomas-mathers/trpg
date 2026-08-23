import { GiTwoCoins, GiWeight } from 'react-icons/gi';

import { cn } from '@/lib/utils';

export function GoldIcon({ className }: { className?: string }) {
  return <GiTwoCoins aria-hidden className={cn('size-4 shrink-0', className)} />;
}

export function WeightIcon({ className }: { className?: string }) {
  return <GiWeight aria-hidden className={cn('mb-1 size-4 shrink-0', className)} />;
}
