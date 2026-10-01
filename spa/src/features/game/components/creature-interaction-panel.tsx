import type {
  CreatureStatusSnapshot,
  SceneSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { Button } from '@/components/ui/button';
import { TradeDialog } from '@/features/inventory/components/trade-dialog';
import { TransferItemDialog } from '@/features/inventory/components/transfer-item-dialog';
import type { DeliverItemDialogState } from '@/features/quests/components/deliver-item-dialog';
import type { QuestDialogState } from '@/features/quests/components/quest-dialog';

import { useCreaturePanel } from '../hooks/use-creature-panel';
import { GameChat } from './game-chat';

const HUMANOIDS = new Set(['Human', 'Elf', 'Dwarf', 'Orc', 'Halfling', 'Gnome']);
export interface CreatureInteractionPanelProps {
  scene: SceneSnapshot;
  creature: CreatureStatusSnapshot;
  onClose: () => void;
  onQuestDialogRequested: (quest: QuestDialogState) => void;
  onDeliverItemDialogRequested: (dialog: DeliverItemDialogState) => void;
}

export function CreatureInteractionPanel({
  scene,
  creature,
  onClose,
  onQuestDialogRequested,
  onDeliverItemDialogRequested,
}: CreatureInteractionPanelProps) {
  const { id, name, condition, tradeWorkstationId, questMarkers, readyToDeliver } = creature;
  const { playerStatus, worldId, buildingName } = scene;
  const {
    mode,
    setMode,
    disabled,
    error,
    startIndex,
    close,
    talk,
    quest,
    deliver,
    beginAction,
    theft,
  } = useCreaturePanel({
    scene,
    creature,
    onClose,
    onQuestDialogRequested,
    onDeliverItemDialogRequested,
  });
  return (
    <section
      className="bg-background/95 absolute top-[42%] right-4 bottom-20 z-20 flex w-[calc(100%-2rem)] max-w-[calc(100%-2rem)] flex-col rounded-lg border shadow-xl backdrop-blur-sm md:top-4 md:w-[26rem]"
      aria-label={`Interact with ${name}`}
    >
      <header className="flex items-center justify-between gap-3 border-b p-4">
        <h2 className="font-heading text-xl">{name}</h2>
        <Button variant="ghost" onClick={close} disabled={disabled}>
          {mode === 'talk' ? 'End conversation' : 'Leave'}
        </Button>
      </header>
      {error && (
        <p role="alert" className="text-destructive p-4">
          {error}
        </p>
      )}
      {mode === 'talk' ? (
        <GameChat recipient={name} startIndex={startIndex} />
      ) : (
        <div className="flex flex-col gap-2 p-4">
          <Button onClick={() => void talk()} disabled={condition !== 'Awake' || disabled}>
            Talk
          </Button>
          <Button variant="outline" onClick={() => void beginAction('inspect')} disabled={disabled}>
            {condition === 'Dead' ? 'Search' : 'Inspect'}
          </Button>
          {condition !== 'Dead' && tradeWorkstationId && (
            <Button variant="outline" onClick={() => void beginAction('trade')} disabled={disabled}>
              Trade
            </Button>
          )}
          {condition !== 'Dead' &&
            (questMarkers ?? []).map((marker) => (
              <Button
                key={marker.questId}
                variant="outline"
                onClick={() => void quest(marker.questId)}
                disabled={disabled}
              >
                Quest: {marker.name}
              </Button>
            ))}
          {condition !== 'Dead' && readyToDeliver && (
            <Button variant="outline" onClick={() => void deliver()} disabled={disabled}>
              Give Item
            </Button>
          )}
        </div>
      )}
      <TransferItemDialog
        playerId={playerStatus.id}
        target={{ id, name, ownerType: 'Creature' }}
        open={mode === 'inspect'}
        transfersEnabled={condition === 'Dead' || HUMANOIDS.has(creature.creatureType)}
        onClose={() => setMode('actions')}
        onTheftEncounter={theft}
      />
      {tradeWorkstationId && (
        <TradeDialog
          playerId={playerStatus.id}
          worldId={worldId}
          workstationId={tradeWorkstationId}
          workerId={id}
          workerName={name}
          shopName={buildingName ?? 'Shop'}
          open={mode === 'trade'}
          onClose={() => setMode('actions')}
        />
      )}
    </section>
  );
}
