import { HubConnectionState } from '@microsoft/signalr';
import { useState } from 'react';

import { CombatDialog } from '@/features/combat/components/combat-dialog';
import { GuardEncounterDialog } from '@/features/encounters/components/guard-encounter-dialog';
import { HostileEncounterDialog } from '@/features/encounters/components/hostile-encounter-dialog';
import { TheftEncounterDialog } from '@/features/encounters/components/theft-encounter-dialog';
import { useHasActiveEncounter } from '@/features/encounters/hooks/use-has-active-encounter';
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub, useGameHubConnection } from '@/features/game/hooks/use-game-hub-connection';
import { useIsInCombat } from '@/features/game/hooks/use-is-in-combat';

import { ChatHistory } from './chat-history';
import { ChatInput } from './chat-input';

export function GameChat() {
  const { messages, isStreaming, submitNarratedTurn } = useGameChat();
  const chatHub = useChatHub();
  const { connectionStatus } = useGameHubConnection();
  const isConnected = connectionStatus === HubConnectionState.Connected;
  const isInCombat = useIsInCombat();
  const hasActiveEncounter = useHasActiveEncounter();
  const [input, setInput] = useState('');

  const handleSend = () => {
    const text = input.trim();
    if (!text || isStreaming) {
      return;
    }

    setInput('');
    submitNarratedTurn(text, chatHub.sendChat(text));
  };

  return (
    <>
      <ChatHistory messages={messages} />
      <GameChatControls
        input={input}
        isConnected={isConnected}
        isInCombat={isInCombat || hasActiveEncounter}
        isStreaming={isStreaming}
        onChange={setInput}
        onSubmit={handleSend}
      />
    </>
  );
}

interface GameChatControlsProps {
  input: string;
  isConnected: boolean;
  isInCombat: boolean;
  isStreaming: boolean;
  onChange: (value: string) => void;
  onSubmit: () => void;
}

function GameChatControls({
  input,
  isConnected,
  isInCombat,
  isStreaming,
  onChange,
  onSubmit,
}: GameChatControlsProps) {
  return (
    <div className="mx-auto w-full max-w-2xl p-4">
      <CombatDialog />
      <HostileEncounterDialog />
      <GuardEncounterDialog />
      <TheftEncounterDialog />
      {!isInCombat && (
        <ChatInput
          value={input}
          disabled={!isConnected || isStreaming}
          onChange={onChange}
          onSubmit={onSubmit}
        />
      )}
    </div>
  );
}
