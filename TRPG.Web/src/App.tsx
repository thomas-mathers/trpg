import { useNavigate, useParams } from '@tanstack/react-router';
import { MenuIcon } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';

import { NarrationText } from './components/NarrationText';
import { Button } from './components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from './components/ui/dropdown-menu';
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
import { appendNarrationToken, type NarrationSegment } from './lib/narration-markup';

type ChatMessage =
  | { id: string; role: 'narrator'; segments: NarrationSegment[] }
  | { id: string; role: 'player'; content: string };

function App() {
  const navigate = useNavigate();
  const { sessionId } = useParams({ from: '/session/$sessionId' });
  const { isConnected, streamOpening, streamChat } = useGameHubConnection(sessionId);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [input, setInput] = useState('');
  const startedSessionId = useRef<string | null>(null);

  const appendToken = (id: string, token: string) => {
    setMessages((current) =>
      current.map((m) =>
        m.id === id && m.role === 'narrator'
          ? { ...m, segments: appendNarrationToken(m.segments, token) }
          : m,
      ),
    );
  };

  useEffect(() => {
    if (!isConnected || startedSessionId.current === sessionId) {
      return;
    }

    startedSessionId.current = sessionId;
    setMessages([]);

    const narratorId = crypto.randomUUID();
    setMessages([{ id: narratorId, role: 'narrator', segments: [] }]);
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
      { id: narratorId, role: 'narrator', segments: [] },
    ]);

    setIsStreaming(true);
    streamChat(
      text,
      (token) => appendToken(narratorId, token),
      () => setIsStreaming(false),
    );
  };

  return (
    <div className="relative flex h-screen flex-col">
      <div className="absolute top-2 right-2 z-10">
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon">
              <MenuIcon />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem disabled>Character</DropdownMenuItem>
            <DropdownMenuItem disabled>Inventory</DropdownMenuItem>
            <DropdownMenuItem disabled>Skills</DropdownMenuItem>
            <DropdownMenuItem disabled>Abilities</DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => navigate({ to: '/' })}>
              Exit to Main Menu
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

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
                        <div className="typeset typeset-chat whitespace-pre-line">
                          <NarrationText sessionId={sessionId} segments={message.segments} />
                        </div>
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
