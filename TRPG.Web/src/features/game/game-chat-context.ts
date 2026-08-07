import { createContext, useContext } from 'react';

import type { GameChat } from './hooks/use-game-chat';

export const GameChatContext = createContext<GameChat | null>(null);

export function useGameActions() {
  const context = useContext(GameChatContext);
  if (!context) {
    throw new Error('useGameActions must be used within a GameChatContext provider');
  }
  return context;
}
