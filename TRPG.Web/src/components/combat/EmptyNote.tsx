import type { ReactNode } from 'react';

export function EmptyNote({ children }: { children: ReactNode }) {
  return <p className="text-muted-foreground py-3.5 text-center text-xs">{children}</p>;
}
