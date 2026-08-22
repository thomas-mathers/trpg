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
  reconnecting: 'text-stamina',
  reconnected: 'text-heal',
  disconnected: 'text-destructive',
  'combat-start': 'text-destructive',
  'combat-end': 'text-muted-foreground',
};

export function ChatMarker({ text, variant = 'location' }: ChatMarkerProps) {
  const colorClass = VARIANT_CLASSES[variant];

  return (
    <div className={cn('flex items-center gap-3 py-2 text-xs', colorClass)}>
      <Separator className={cn('flex-1 bg-current opacity-30', colorClass)} />
      <span className="font-heading shrink-0 tracking-wide uppercase">{text}</span>
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
                        <div className="typeset typeset-chat border-primary/25 border-l-2 pl-4 whitespace-pre-line">
                          <NarrationText segments={message.segments} />
                        </div>
                      ) : (
                        <p className="parchment-bubble inline-block self-end rounded-lg px-3 py-1.5 text-right">
                          {message.content}
                        </p>
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
