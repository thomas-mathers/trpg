import { MoreVertical } from 'lucide-react';
import type { ReactNode } from 'react';
import { useState } from 'react';
import { GiBed, GiBlackBook, GiChest, GiLever, GiShoppingBag, GiWoodenSign } from 'react-icons/gi';

import type { OwnerType } from '@/api/client';
import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { BookshelfDialog } from '@/features/books/components/bookshelf-dialog';
import { SignDialog } from '@/features/game/components/sign-dialog';
import { SleepDialog } from '@/features/game/components/sleep-dialog';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { runAction } from '@/features/game/run-action';
import { TransferItemDialog } from '@/features/inventory/components/transfer-item-dialog';
import type { DeliverItemDialogState } from '@/features/quests/components/deliver-item-dialog';
import type { QuestDialogState } from '@/features/quests/components/quest-dialog';
import { QuestTracker } from '@/features/quests/components/quest-tracker';

interface NearbyPanelProps {
  scene: SceneSnapshot;
  onOpenQuestJournal: () => void;
  onQuestDialogRequested: (dialog: QuestDialogState) => void;
  onDeliverItemDialogRequested: (dialog: DeliverItemDialogState) => void;
  onTheftEncounter?: (encounterId: string) => void;
}

export function NearbyPanel({ scene, onOpenQuestJournal, onTheftEncounter }: NearbyPanelProps) {
  const chatHub = useChatHub();
  const [inventoryTarget, setInventoryTarget] = useState<{
    id: string;
    name: string;
    ownerType: OwnerType;
    transfersEnabled: boolean;
  } | null>(null);
  const [isTransferOpen, setIsTransferOpen] = useState(false);
  const [isSleepOpen, setIsSleepOpen] = useState(false);
  const [bookshelf, setBookshelf] = useState<{
    id: string;
    name: string;
  } | null>(null);
  const [sign, setSign] = useState<{ id: string; name: string } | null>(null);

  const nearbyContainers = scene.nearbyProps.filter((prop) => prop.type === 'Container');
  const nearbyTriggers = scene.nearbyProps.filter((prop) => prop.type === 'Trigger');
  const nearbyTradeWorkstations = scene.nearbyProps.filter((prop) => prop.type === 'Trade');
  const nearbyBeds = scene.nearbyProps.filter((prop) => prop.type === 'Bed');
  const nearbyBookshelves = scene.nearbyProps.filter((prop) => prop.type === 'Reading');
  const nearbySigns = scene.nearbyProps.filter((prop) => prop.type === 'Sign');

  return (
    <div className="flex flex-col gap-6 p-4 text-sm">
      <QuestTracker
        playerId={scene.playerStatus.id}
        worldId={scene.worldId}
        onOpenJournal={onOpenQuestJournal}
      />

      {nearbyContainers.length > 0 && (
        <Section title="Nearby Containers">
          {nearbyContainers.map((container) => (
            <div key={container.id} className="flex items-center justify-between gap-2 py-1.5">
              <span className="flex min-w-0 items-center gap-1.5">
                <GiChest className="text-muted-foreground size-[18px] shrink-0" />
                <button
                  type="button"
                  onClick={() => {
                    setInventoryTarget({
                      id: container.id,
                      name: container.name,
                      ownerType: 'Container',
                      transfersEnabled: true,
                    });
                    setIsTransferOpen(true);
                  }}
                  className="cursor-pointer truncate font-medium underline decoration-dotted underline-offset-2"
                >
                  {container.name}
                </button>
              </span>
            </div>
          ))}
        </Section>
      )}

      {nearbyTriggers.length > 0 && (
        <Section title="Nearby Triggers">
          {nearbyTriggers.map((trigger) => (
            <div key={trigger.id} className="flex items-center justify-between gap-2 py-1.5">
              <span className="flex min-w-0 items-center gap-1.5">
                <GiLever className="text-muted-foreground size-[18px] shrink-0" />
                <span className="truncate font-medium">{trigger.name}</span>
              </span>
              <Button
                variant="outline"
                size="xs"
                className="border-sidebar-border bg-sidebar-accent text-sidebar-accent-foreground hover:bg-sidebar-accent/80 hover:text-sidebar-accent-foreground"
                onClick={() => void runAction(chatHub.sendActivateTrigger(trigger.id))}
              >
                Activate
              </Button>
            </div>
          ))}
        </Section>
      )}

      {nearbyBookshelves.length > 0 && (
        <Section title="Nearby Bookshelves">
          {nearbyBookshelves.map((shelf) => (
            <div key={shelf.id} className="flex items-center justify-between gap-2 py-1.5">
              <span className="flex min-w-0 items-center gap-1.5">
                <GiBlackBook className="text-muted-foreground size-[18px] shrink-0" />
                <button
                  type="button"
                  onClick={() => setBookshelf({ id: shelf.id, name: shelf.name })}
                  className="cursor-pointer truncate font-medium underline decoration-dotted underline-offset-2"
                >
                  {shelf.name}
                </button>
              </span>
            </div>
          ))}
        </Section>
      )}

      {nearbyTradeWorkstations.length > 0 && (
        <Section title="Nearby Workstations">
          {nearbyTradeWorkstations.map((workstation) => (
            <div key={workstation.id} className="flex items-center justify-between gap-2 py-1.5">
              <span className="flex min-w-0 items-center gap-1.5">
                <GiShoppingBag className="text-muted-foreground size-[18px] shrink-0" />
                <button
                  type="button"
                  onClick={() => {
                    setInventoryTarget({
                      id: workstation.id,
                      name: workstation.name,
                      ownerType: 'Workstation',
                      transfersEnabled: true,
                    });
                    setIsTransferOpen(true);
                  }}
                  className="cursor-pointer truncate font-medium underline decoration-dotted underline-offset-2"
                >
                  {workstation.name}
                </button>
              </span>
            </div>
          ))}
        </Section>
      )}

      {nearbyBeds.length > 0 && (
        <Section title="Nearby Beds">
          {nearbyBeds.map((bed) => (
            <div key={bed.id} className="flex items-center justify-between gap-2 py-1.5">
              <span className="flex min-w-0 items-center gap-1.5">
                <GiBed className="text-muted-foreground size-[18px] shrink-0" />
                <span className="truncate font-medium">{bed.name}</span>
              </span>
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="ghost" size="icon-xs" aria-label={`Actions for ${bed.name}`}>
                    <MoreVertical />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem onClick={() => setIsSleepOpen(true)}>Sleep</DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          ))}
        </Section>
      )}

      {nearbySigns.length > 0 && (
        <Section title="Signs">
          {nearbySigns.map((nearbySign) => (
            <div key={nearbySign.id} className="flex items-center justify-between gap-2 py-1.5">
              <span className="flex min-w-0 items-center gap-1.5">
                <GiWoodenSign className="text-muted-foreground size-[18px] shrink-0" />
                <button
                  type="button"
                  onClick={() => setSign({ id: nearbySign.id, name: nearbySign.name })}
                  className="cursor-pointer truncate font-medium underline decoration-dotted underline-offset-2"
                >
                  {nearbySign.name}
                </button>
              </span>
            </div>
          ))}
        </Section>
      )}

      <TransferItemDialog
        playerId={scene.playerStatus.id}
        target={inventoryTarget}
        open={isTransferOpen}
        transfersEnabled={inventoryTarget?.transfersEnabled}
        onClose={() => setIsTransferOpen(false)}
        onTheftEncounter={onTheftEncounter}
      />
      <SleepDialog open={isSleepOpen} onClose={() => setIsSleepOpen(false)} />

      <BookshelfDialog
        playerId={scene.playerStatus.id}
        workstationId={bookshelf?.id ?? null}
        name={bookshelf?.name ?? ''}
        open={bookshelf !== null}
        onClose={() => setBookshelf(null)}
      />

      <SignDialog
        signId={sign?.id ?? null}
        name={sign?.name ?? ''}
        open={sign !== null}
        onClose={() => setSign(null)}
      />
    </div>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div>
      <p className="text-muted-foreground text-[11px] font-semibold tracking-wider uppercase">
        {title}
      </p>
      <div className="divide-border divide-y">{children}</div>
    </div>
  );
}
