import type { AmountType } from '@/api/client';

export function formatAmount(amount: number, amountType: AmountType): string {
  const sign = amount > 0 ? '+' : '';
  const unit = amountType === 'Percent' ? '%' : '';
  return `${sign}${amount}${unit}`;
}
