import { useNavigate, useParams } from '@tanstack/react-router';
import { MenuIcon } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';

import { NarrationText } from './components/NarrationText';
import { NearbySidebar } from './components/NearbySidebar';
import { NearbyToggleButton } from './components/NearbyToggleButton';
import { StatusBar } from './components/StatusBar';
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
import { SidebarInset, SidebarProvider } from './components/ui/sidebar';
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
  const [turnCount, setTurnCount] = useState(0);
  const [isNearbyOpen, setIsNearbyOpen] = useState(true);
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
      () => {
        setIsStreaming(false);
        setTurnCount((count) => count + 1);
      },
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
      () => {
        setIsStreaming(false);
        setTurnCount((count) => count + 1);
      },
    );
  };

  return (
    <SidebarProvider
      open={isNearbyOpen}
      onOpenChange={setIsNearbyOpen}
      className="h-screen flex-col"
    >
      <div className="flex items-center gap-4 border-b px-4 py-2">
        <StatusBar sessionId={sessionId} turnCount={turnCount} />
        <NearbyToggleButton />
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" className="ml-auto">
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

      {/* will-change-transform makes this the containing block for the sidebar's fixed
          positioning, scoping it below the topbar instead of the full viewport. overflow-hidden
          keeps the collapsed sidebar (which still sits just off the right edge) from expanding
          this container's scrollable area and producing a horizontal scrollbar. */}
      <div className="relative flex min-h-0 flex-1 overflow-hidden will-change-transform">
        <SidebarInset>
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
        </SidebarInset>

        <NearbySidebar sessionId={sessionId} turnCount={turnCount} />
      </div>
    </SidebarProvider>
  );
}

export default App;
