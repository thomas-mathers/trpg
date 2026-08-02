import { useNavigate, useParams } from '@tanstack/react-router';
import { MenuIcon } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';

import { ChatMarker, type ChatMarkerVariant } from './components/ChatMarker';
import { NarrationText } from './components/NarrationText';
import { NearbySidebar } from './components/NearbySidebar';
import { NearbyToggleButton } from './components/NearbyToggleButton';
import { StatusBar } from './components/StatusBar';
import { Button } from './components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from './components/ui/dialog';
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
import { useSceneQuery } from './hooks/useSceneQuery';
import { gameEventBus, type ConnectionStatus } from './lib/gameEventBus';
import { appendNarrationToken, type NarrationSegment } from './lib/narration-markup';
import { formatLocation, locationKey } from './lib/scene-format';
import { clearStoredMessages, loadStoredMessages, saveMessages } from './lib/session-storage';

export type ChatMessage =
  | { id: string; role: 'narrator'; segments: NarrationSegment[] }
  | { id: string; role: 'player'; content: string }
  | { id: string; role: 'marker'; text: string; variant: ChatMarkerVariant };

const CONNECTION_STATUS_TEXT: Record<ConnectionStatus, string> = {
  reconnecting: 'Reconnecting…',
  reconnected: 'Reconnected',
  disconnected: 'Connection lost',
};

function GameScreen() {
  const navigate = useNavigate();
  const { sessionId } = useParams({ from: '/session/$sessionId' });
  const { isConnected, streamOpening, streamChat, endSession } = useGameHubConnection(sessionId);
  const sceneQuery = useSceneQuery(sessionId);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [isNearbyOpen, setIsNearbyOpen] = useState(true);
  const [input, setInput] = useState('');
  const [showDisconnectedDialog, setShowDisconnectedDialog] = useState(false);
  const startedSessionId = useRef<string | null>(null);
  const lastLocationKey = useRef<string | null>(null);
  const currentNarratorId = useRef<string | null>(null);

  const appendToken = (id: string, token: string) => {
    setMessages((current) =>
      current.map((m) =>
        m.id === id && m.role === 'narrator'
          ? { ...m, segments: appendNarrationToken(m.segments, token) }
          : m,
      ),
    );
  };

  const appendMarker = (text: string, variant: ChatMarkerVariant) => {
    const marker: ChatMessage = { id: crypto.randomUUID(), role: 'marker', text, variant };
    setMessages((current) => {
      const insertIndex = currentNarratorId.current
        ? current.findIndex((m) => m.id === currentNarratorId.current)
        : -1;
      if (insertIndex === -1) {
        return [...current, marker];
      }
      return [...current.slice(0, insertIndex), marker, ...current.slice(insertIndex)];
    });
  };

  useEffect(() => {
    if (sceneQuery.data && lastLocationKey.current === null) {
      lastLocationKey.current = locationKey(sceneQuery.data);
    }
  }, [sceneQuery.data]);

  useEffect(
    () =>
      gameEventBus.on('SceneChanged', (scene) => {
        const key = locationKey(scene);
        if (lastLocationKey.current !== null && key !== lastLocationKey.current) {
          appendMarker(formatLocation(scene), 'location');
        }
        lastLocationKey.current = key;
      }),
    [],
  );

  useEffect(
    () =>
      gameEventBus.on('ConnectionStatusChanged', (status) => {
        appendMarker(CONNECTION_STATUS_TEXT[status], status);
        if (status === 'disconnected') {
          setShowDisconnectedDialog(true);
        }
      }),
    [],
  );

  const handleReturnToMenu = () => {
    clearStoredMessages(sessionId);
    setShowDisconnectedDialog(false);
    navigate({ to: '/' });
  };

  useEffect(() => {
    if (!isConnected || startedSessionId.current === sessionId) {
      return;
    }

    startedSessionId.current = sessionId;
    lastLocationKey.current = null;

    const stored = loadStoredMessages(sessionId);
    if (stored && stored.length > 0) {
      setMessages(stored);
      return;
    }

    setMessages([]);

    const narratorId = crypto.randomUUID();
    setMessages([{ id: narratorId, role: 'narrator', segments: [] }]);
    currentNarratorId.current = narratorId;
    setIsStreaming(true);
    streamOpening(
      (token) => appendToken(narratorId, token),
      () => {
        currentNarratorId.current = null;
        setIsStreaming(false);
      },
    );
  }, [isConnected, sessionId, streamOpening]);

  useEffect(() => {
    if (messages.length > 0) {
      saveMessages(sessionId, messages);
    }
  }, [messages, sessionId]);

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
    currentNarratorId.current = narratorId;

    setIsStreaming(true);
    streamChat(
      text,
      (token) => appendToken(narratorId, token),
      () => {
        currentNarratorId.current = null;
        setIsStreaming(false);
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
        <StatusBar sessionId={sessionId} />
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
            <DropdownMenuItem
              onClick={async () => {
                clearStoredMessages(sessionId);
                await endSession();
                navigate({ to: '/' });
              }}
            >
              Exit to Main Menu
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      <div className="relative flex min-h-0 flex-1 overflow-hidden will-change-transform">
        <SidebarInset>
          <MessageScrollerProvider>
            <MessageScroller className="flex-1">
              <MessageScrollerViewport>
                <MessageScrollerContent className="mx-auto w-full max-w-2xl p-4">
                  {messages.map((message) =>
                    message.role === 'marker' ? (
                      <MessageScrollerItem key={message.id}>
                        <ChatMarker text={message.text} variant={message.variant} />
                      </MessageScrollerItem>
                    ) : (
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
                    ),
                  )}
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

        <NearbySidebar sessionId={sessionId} />
      </div>

      <Dialog open={showDisconnectedDialog}>
        <DialogContent
          showCloseButton={false}
          onEscapeKeyDown={(e) => e.preventDefault()}
          onPointerDownOutside={(e) => e.preventDefault()}
        >
          <DialogHeader>
            <DialogTitle>Connection Lost</DialogTitle>
            <DialogDescription>
              The connection to the server was lost and couldn't be restored. Returning to the main
              menu.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button onClick={handleReturnToMenu}>OK</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </SidebarProvider>
  );
}

export default GameScreen;
