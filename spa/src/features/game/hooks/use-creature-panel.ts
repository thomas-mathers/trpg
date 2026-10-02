import { useEffect, useRef, useState } from 'react';

import { getDeliverItemDialog, getQuestDialog } from '@/api/client';

import type { CreatureInteractionPanelProps } from '../components/creature-interaction-panel';
import { runAction } from '../run-action';
import { useGameChat } from './use-game-chat';
import { useChatHub } from './use-game-hub-connection';
import { useCreatureInteraction } from './use-interaction-lifecycle';
type Mode = 'actions' | 'trade' | 'inspect' | 'talk' | 'quest';

function usePanelContext({
  scene,
  creature,
  onClose,
  onQuestDialogRequested,
  onDeliverItemDialogRequested,
}: CreatureInteractionPanelProps) {
  const { id, name, condition } = creature;
  const { playerStatus, worldId } = scene;
  const chatHub = useChatHub();
  const { messages, isStreaming, submitNarratedTurn } = useGameChat();
  const [mode, setMode] = useState<Mode>('actions');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const startIndex = useRef(messages.length);
  const { release } = useCreatureInteraction({
    playerId: playerStatus.id,
    worldId,
    creatureId: mode === 'actions' && condition !== 'Dead' ? id : undefined,
  });
  return {
    scene,
    creature,
    onClose,
    onQuestDialogRequested,
    onDeliverItemDialogRequested,
    id,
    name,
    playerStatus,
    worldId,
    chatHub,
    messages,
    isStreaming,
    submitNarratedTurn,
    mode,
    setMode,
    busy,
    setBusy,
    error,
    setError,
    startIndex,
    release,
  };
}

type PanelContext = ReturnType<typeof usePanelContext>;

export function useCreaturePanel(props: CreatureInteractionPanelProps) {
  const context = usePanelContext(props);
  const { mode, setMode, busy, isStreaming, error, startIndex } = context;
  useEffect(() => {
    const escape = (event: KeyboardEvent) => {
      if (
        !event.defaultPrevented &&
        event.key === 'Escape' &&
        (mode === 'actions' || mode === 'talk')
      ) {
        event.preventDefault();
        closePanel(context);
      }
    };
    window.addEventListener('keydown', escape);
    return () => window.removeEventListener('keydown', escape);
  });
  return {
    mode,
    setMode,
    disabled: busy || isStreaming,
    error,
    startIndex: startIndex.current,
    close: () => closePanel(context),
    talk: () => talk(context),
    quest: (questId: string) => quest(context, questId),
    deliver: () => deliver(context),
    beginAction: (next: Mode) => beginAction(context, next),
    theft: (encounterId: string) => {
      void runAction(context.chatHub.startTheftEncounter(encounterId));
      context.onClose();
    },
  };
}

async function beginAction({ setBusy, release, setMode }: PanelContext, next: Mode) {
  setBusy(true);
  await release();
  setMode(next);
  setBusy(false);
}

async function talk(context: PanelContext) {
  const { startIndex, messages, submitNarratedTurn, chatHub, name, setError } = context;
  await beginAction(context, 'talk');
  startIndex.current = messages.length;
  submitNarratedTurn(
    null,
    chatHub.sendChat(
      `I begin a conversation with ${JSON.stringify(name)}. Greet me as this person.`,
    ),
    () => setError('Could not start the conversation. Please try again.'),
  );
}

function closePanel({
  busy,
  isStreaming,
  mode,
  release,
  onClose,
  setBusy,
  setError,
  submitNarratedTurn,
  chatHub,
  name,
}: PanelContext) {
  if (busy || isStreaming) return;
  if (mode !== 'talk') {
    void release().then(onClose);
    return;
  }
  setBusy(true);
  setError('');
  let failed = false;
  submitNarratedTurn(
    null,
    chatHub.sendChat(
      `I end my conversation with ${JSON.stringify(name)}. Save and close this conversation.`,
    ),
    () => {
      failed = true;
      setError('Could not end the conversation. Try again.');
    },
    () => {
      setBusy(false);
      if (!failed) onClose();
    },
  );
}

async function quest(
  {
    setBusy,
    setError,
    playerStatus,
    worldId,
    id,
    release,
    setMode,
    onQuestDialogRequested,
  }: PanelContext,
  questId: string,
) {
  setBusy(true);
  setError('');
  try {
    const { data } = await getQuestDialog({
      path: { playerId: playerStatus.id },
      query: { worldId, giverId: id, questId },
      throwOnError: true,
    });
    await release();
    if (data) {
      setMode('quest');
      onQuestDialogRequested({ ...data, giverId: id, worldId });
    }
  } catch {
    setError('Could not load this quest. Try again.');
  } finally {
    setBusy(false);
  }
}

async function deliver({
  setBusy,
  setError,
  playerStatus,
  worldId,
  id,
  release,
  setMode,
  onDeliverItemDialogRequested,
}: PanelContext) {
  setBusy(true);
  setError('');
  try {
    const { data } = await getDeliverItemDialog({
      path: { playerId: playerStatus.id },
      query: { worldId, recipientId: id },
      throwOnError: true,
    });
    await release();
    if (data) {
      setMode('quest');
      onDeliverItemDialogRequested({ ...data, recipientId: id, worldId });
    }
  } catch {
    setError('Could not load the delivery. Try again.');
  } finally {
    setBusy(false);
  }
}
