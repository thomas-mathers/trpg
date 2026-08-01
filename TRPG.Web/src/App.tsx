import { useParams } from '@tanstack/react-router';
import { useEffect, useRef, useState } from 'react';

import { Input } from './components/ui/input';
import { Message, MessageContent, MessageHeader } from './components/ui/message';
import {
  MessageScroller,
  MessageScrollerButton,
  MessageScrollerContent,
  MessageScrollerItem,
  MessageScrollerProvider,
  MessageScrollerViewport,
} from './components/ui/message-scroller';
import { useGameHubConnection } from './hooks/useGameHubConnection';

interface ChatMessage {
  id: string;
  role: 'narrator' | 'player';
  content: string;
}

function App() {
  const { sessionId } = useParams({ from: '/session/$sessionId' });
  const { isConnected, streamOpening, streamChat } = useGameHubConnection(sessionId);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [input, setInput] = useState('');
  const startedSessionId = useRef<string | null>(null);

  const appendToken = (id: string, token: string) => {
    setMessages((current) =>
      current.map((m) => (m.id === id ? { ...m, content: m.content + token } : m)),
    );
  };

  useEffect(() => {
    if (!isConnected || startedSessionId.current === sessionId) {
      return;
    }

    startedSessionId.current = sessionId;
    setMessages([]);

    const narratorId = crypto.randomUUID();
    setMessages([{ id: narratorId, role: 'narrator', content: '' }]);
    setIsStreaming(true);
    streamOpening(
      (token) => appendToken(narratorId, token),
      () => setIsStreaming(false),
    );
  }, [isConnected, sessionId, streamOpening]);

  const handleSend = () => {
    const text = input.trim();
    if (!text || isStreaming) {
      return;
    }

    setInput('');

    const playerId = crypto.randomUUID();
    const narratorId = crypto.randomUUID();
    setMessages((current) => [
      ...current,
      { id: playerId, role: 'player', content: text },
      { id: narratorId, role: 'narrator', content: '' },
    ]);

    setIsStreaming(true);
    streamChat(
      text,
      (token) => appendToken(narratorId, token),
      () => setIsStreaming(false),
    );
  };

  return (
    <div className="flex h-screen flex-col">
      <MessageScrollerProvider>
        <MessageScroller className="flex-1">
          <MessageScrollerViewport>
            <MessageScrollerContent className="mx-auto w-full max-w-2xl p-4">
              {messages.map((message) => (
                <MessageScrollerItem key={message.id}>
                  <Message align={message.role === 'player' ? 'end' : 'start'}>
                    <MessageContent>
                      {message.role === 'player' && (
                        <MessageHeader className="justify-end">You</MessageHeader>
                      )}
                      {message.role === 'narrator' ? (
                        <div className="typeset typeset-chat">{message.content}</div>
                      ) : (
                        <p className="text-right">{message.content}</p>
                      )}
                    </MessageContent>
                  </Message>
                </MessageScrollerItem>
              ))}
            </MessageScrollerContent>
          </MessageScrollerViewport>
          <MessageScrollerButton />
        </MessageScroller>
      </MessageScrollerProvider>

      <div className="mx-auto w-full max-w-2xl p-4">
        <Input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              handleSend();
            }
          }}
          disabled={!isConnected || isStreaming}
          placeholder="What do you do?"
        />
      </div>
    </div>
  );
}

export default App;
