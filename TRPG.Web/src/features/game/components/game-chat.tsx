import { useState } from 'react';

import { CombatConsole } from '@/features/combat/components/combat-console';
import { useGameActions } from '@/features/game/game-chat-context';
import { useIsInCombat } from '@/features/game/hooks/use-is-in-combat';

import { ChatHistory } from './chat-history';
import { ChatInput } from './chat-input';

export function GameChat() {
  const { messages, isConnected, isStreaming, submitChatMessage } = useGameActions();
  const isInCombat = useIsInCombat();
  const [input, setInput] = useState('');

  const handleSend = () => {
    const text = input.trim();
    if (!text || isStreaming) {
      return;
    }

    setInput('');
    submitChatMessage(text);
  };

  return (
    <>
      <ChatHistory messages={messages} />
      <GameChatControls
        input={input}
        isConnected={isConnected}
        isInCombat={isInCombat}
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
      <CombatConsole />
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
