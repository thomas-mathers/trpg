import { Message, MessageContent, MessageHeader } from '@/components/ui/message';
import {
  MessageScroller,
  MessageScrollerProvider,
  MessageScrollerViewport,
  MessageScrollerContent,
  MessageScrollerItem,
  MessageScrollerButton,
} from '@/components/ui/message-scroller';
import { Separator } from '@/components/ui/separator';
import { cn } from '@/lib/utils';

import type { NarrationSegment } from '../narration-markup';
import { NarrationText } from './narration-text';

export type ChatMarkerVariant =
  | 'location'
  | 'reconnecting'
  | 'reconnected'
  | 'disconnected'
  | 'combat-start'
  | 'combat-end';

interface ChatMarkerProps {
  text: string;
  variant?: ChatMarkerVariant;
}

const VARIANT_CLASSES: Record<ChatMarkerVariant, string> = {
  location: 'text-muted-foreground',
  reconnecting: 'text-amber-500',
  reconnected: 'text-green-500',
  disconnected: 'text-destructive',
  'combat-start': 'text-destructive',
  'combat-end': 'text-muted-foreground',
};

export function ChatMarker({ text, variant = 'location' }: ChatMarkerProps) {
  const colorClass = VARIANT_CLASSES[variant];

  return (
    <div className={cn('flex items-center gap-3 py-2 text-xs', colorClass)}>
      <Separator className={cn('flex-1 bg-current opacity-30', colorClass)} />
      <span className="shrink-0 font-medium">{text}</span>
      <Separator className={cn('flex-1 bg-current opacity-30', colorClass)} />
    </div>
  );
}

export type ChatMessage =
  | { id: string; role: 'narrator'; segments: NarrationSegment[] }
  | { id: string; role: 'player'; content: string }
  | { id: string; role: 'marker'; text: string; variant: ChatMarkerVariant };

export type ChatHistoryProps = {
  messages: ChatMessage[];
};

export function ChatHistory({ messages }: ChatHistoryProps) {
  return (
    <MessageScrollerProvider autoScroll scrollPreviousItemPeek={64}>
      <MessageScroller className="flex-1">
        <MessageScrollerViewport>
          <MessageScrollerContent className="mx-auto w-full max-w-2xl p-4">
            {messages.map((message) =>
              message.role === 'marker' ? (
                <MessageScrollerItem key={message.id} messageId={message.id}>
                  <ChatMarker text={message.text} variant={message.variant} />
                </MessageScrollerItem>
              ) : (
                <MessageScrollerItem
                  key={message.id}
                  messageId={message.id}
                  scrollAnchor={message.role === 'player'}
                >
                  <Message align={message.role === 'player' ? 'end' : 'start'}>
                    <MessageContent>
                      {message.role === 'player' && (
                        <MessageHeader className="justify-end">You</MessageHeader>
                      )}
                      {message.role === 'narrator' ? (
                        <div className="typeset typeset-chat whitespace-pre-line">
                          <NarrationText segments={message.segments} />
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
  );
}
