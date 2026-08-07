import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import { PlayerIdContext } from '@/features/game/contexts/scene-context';

interface TestProvidersProps {
  children: ReactNode;
  playerId?: string;
  queryClient: QueryClient;
}

export function TestProviders({
  children,
  playerId = 'player-id',
  queryClient,
}: TestProvidersProps) {
  return (
    <QueryClientProvider client={queryClient}>
      <PlayerIdContext.Provider value={playerId}>{children}</PlayerIdContext.Provider>
    </QueryClientProvider>
  );
}
