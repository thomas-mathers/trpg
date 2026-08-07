import { useState } from 'react';

import { CombatConsole } from '@/features/combat/components/combat-console';
import { useGameActions } from '@/features/game/game-chat-context';
import { useIsInCombat } from '@/features/game/hooks/use-is-in-combat';
import { useSceneQuery } from '@/features/game/hooks/use-scene-query';

import { ChatHistory } from './chat-history';
import { ChatInput } from './chat-input';

interface GameChatProps {
  sessionId: string;
}

export function GameChat({ sessionId }: GameChatProps) {
  const { messages, isConnected, isStreaming, submitChatMessage } = useGameActions();
  const isInCombat = useIsInCombat();
  const sceneQuery = useSceneQuery(sessionId);
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
      <ChatHistory sessionId={sessionId} messages={messages} />
      <div className="mx-auto w-full max-w-2xl p-4">
        <CombatConsole playerId={sceneQuery.data?.playerStatus.id ?? ''} />
        {!isInCombat && (
          <ChatInput
            value={input}
            disabled={!isConnected || isStreaming}
            onChange={setInput}
            onSubmit={handleSend}
          />
        )}
      </div>
    </>
  );
}
