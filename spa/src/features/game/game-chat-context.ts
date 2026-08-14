import { createContext, useContext } from 'react';

import type { GameChat } from './hooks/use-game-chat';

export const GameChatContext = createContext<GameChat | null>(null);

function useGameChatContext() {
  const context = useContext(GameChatContext);
  if (!context) {
    throw new Error('useGameChatContext must be used within a GameChatContext provider');
  }
  return context;
}

export function useChatPanel() {
  const { messages, isConnected, isStreaming, submitChatMessage } = useGameChatContext();
  return { messages, isConnected, isStreaming, submitChatMessage };
}

export function useCombatChatActions() {
  const { isStreaming, submitFlee, submitCombatAction } = useGameChatContext();
  return { isStreaming, submitFlee, submitCombatAction };
}

export function useEncounterActions() {
  const { isStreaming, submitEncounterAction } = useGameChatContext();
  return { isStreaming, submitEncounterAction };
}
