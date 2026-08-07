import { createContext, useContext } from 'react';

import type { useGameChat } from './hooks/use-game-chat';

export const GameChatContext = createContext<ReturnType<typeof useGameChat> | null>(null);

export function useGameActions() {
  const context = useContext(GameChatContext);
  if (!context) {
    throw new Error('useGameActions must be used within a GameChatContext provider');
  }
  return context;
}
