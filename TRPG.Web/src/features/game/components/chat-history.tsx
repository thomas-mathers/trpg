import type { EntityType } from '@/api/client';
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

import { ENTITY_TYPE_COLORS } from '../entity-type-colors';
import type { NarrationSegment } from '../narration-markup';
import { EntityTooltip } from './entity-tooltip';

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

interface EntityLinkProps {
  sessionId: string;
  id: string;
  name: string;
  entityType: EntityType;
}

export function EntityLink({ sessionId, id, name, entityType }: EntityLinkProps) {
  return (
    <EntityTooltip sessionId={sessionId} id={id} name={name} entityType={entityType}>
      <span
        className="cursor-help font-bold whitespace-nowrap not-italic"
        style={{ color: ENTITY_TYPE_COLORS[entityType] }}
      >
        [{name}]
      </span>
    </EntityTooltip>
  );
}

interface NarrationTextProps {
  sessionId: string;
  segments: NarrationSegment[];
}

function NarrationText({ sessionId, segments }: NarrationTextProps) {
  return (
    <>
      {segments.map((segment, index) =>
        segment.type === 'entity' ? (
          <EntityLink
            key={index}
            sessionId={sessionId}
            id={segment.id}
            name={segment.name}
            entityType={segment.entityType}
          />
        ) : (
          <span key={index}>{segment.text}</span>
        ),
      )}
    </>
  );
}

export type ChatMessage =
  | { id: string; role: 'narrator'; segments: NarrationSegment[] }
  | { id: string; role: 'player'; content: string }
  | { id: string; role: 'marker'; text: string; variant: ChatMarkerVariant };

export type ChatHistoryProps = {
  sessionId: string;
  messages: ChatMessage[];
};

export function ChatHistory({ sessionId, messages }: ChatHistoryProps) {
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
  );
}
