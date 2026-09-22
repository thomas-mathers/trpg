import { CircleHelp, MoreVertical, Package } from 'lucide-react';
import type { ReactNode } from 'react';
import { useState } from 'react';
import type { IconType } from 'react-icons';
import {
  GiAncientRuins,
  GiAnvil,
  GiApothecary,
  GiBadGnome,
  GiBed,
  GiBeerStein,
  GiBubblingFlask,
  GiCampingTent,
  GiCastle,
  GiBlackBook,
  GiChest,
  GiChurch,
  GiCroissant,
  GiCrossedSwords,
  GiCrown,
  GiCrystalBall,
  GiCryptEntrance,
  GiDeathSkull,
  GiDevilMask,
  GiDragonHead,
  GiDwarfFace,
  GiElfHelmet,
  GiExitDoor,
  GiFireSilhouette,
  GiFountain,
  GiGems,
  GiGhost,
  GiGiant,
  GiGoblinHead,
  GiGoldMine,
  GiGolemHead,
  GiHammerNails,
  GiHandcuffs,
  GiHobbitDoor,
  GiHolySymbol,
  GiHorseHead,
  GiHouse,
  GiLever,
  GiMedievalGate,
  GiMeepleGroup,
  GiMountainCave,
  GiMountains,
  GiOpenBook,
  GiOrcHead,
  GiPerson,
  GiShirt,
  GiShoppingBag,
  GiSkeleton,
  GiStable,
  GiStoneTower,
  GiTombstone,
  GiWolfHead,
  GiWoodenSign,
} from 'react-icons/gi';

import { getDeliverItemDialog, getQuestDialog } from '@/api/client';
import type { BuildingType, DistrictType, NearbyCaravanSnapshot, OwnerType } from '@/api/client';
import type {
  CreatureStatusSnapshot,
  CreatureType,
  NearbyExitDestination as SignalrNearbyExitDestination,
  RoomRole,
  SceneSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { BookshelfDialog } from '@/features/books/components/bookshelf-dialog';
import { isDangerous } from '@/features/combat/threat-level';
import { CaravanDialog } from '@/features/game/components/caravan-dialog';
import { EntityTooltip } from '@/features/game/components/entity-tooltip';
import { ExitDirectionArrow } from '@/features/game/components/exit-direction-arrow';
import { ExitFamiliarity } from '@/features/game/components/exit-familiarity';
import { SignDialog } from '@/features/game/components/sign-dialog';
import { SleepDialog } from '@/features/game/components/sleep-dialog';
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { ROOM_ROLE_ICONS } from '@/features/game/room-role-icons';
import { TradeDialog } from '@/features/inventory/components/trade-dialog';
import { TransferItemDialog } from '@/features/inventory/components/transfer-item-dialog';
import type { DeliverItemDialogState } from '@/features/quests/components/deliver-item-dialog';
import type { QuestDialogState } from '@/features/quests/components/quest-dialog';
import { QuestTracker } from '@/features/quests/components/quest-tracker';
import { cn } from '@/lib/utils';

const HUMANOID_CREATURE_TYPES: ReadonlySet<CreatureType> = new Set([
  'Human',
  'Elf',
  'Dwarf',
  'Orc',
  'Halfling',
  'Gnome',
]);

const CREATURE_TYPE_ICON: Record<CreatureType, IconType> = {
  Human: GiPerson,
  Elf: GiElfHelmet,
  Dwarf: GiDwarfFace,
  Orc: GiOrcHead,
  Halfling: GiHobbitDoor,
  Gnome: GiBadGnome,
  Goblin: GiGoblinHead,
  Undead: GiSkeleton,
  Wraith: GiGhost,
  Demon: GiDevilMask,
  Beast: GiWolfHead,
  Construct: GiGolemHead,
  Elemental: GiFireSilhouette,
  Giant: GiGiant,
  Dragon: GiDragonHead,
};

type NearbyExitDestination = SignalrNearbyExitDestination & {
  $type?: 'District' | 'Building' | 'Room' | 'Wilderness';
  buildingType?: BuildingType;
  districtType?: DistrictType;
  role?: RoomRole;
};

const BUILDING_TYPE_ICONS: Record<BuildingType, IconType> = {
  ArcaneShop: GiCrystalBall,
  Apothecary: GiApothecary,
  Bakery: GiCroissant,
  Barracks: GiCrossedSwords,
  Blacksmith: GiAnvil,
  Carpenter: GiHammerNails,
  Castle: GiCastle,
  Cave: GiMountainCave,
  Crypt: GiCryptEntrance,
  GeneralGoods: GiShoppingBag,
  GuildHall: GiMeepleGroup,
  House: GiHouse,
  Inn: GiBed,
  Jail: GiHandcuffs,
  Jeweler: GiGems,
  Library: GiOpenBook,
  Mine: GiGoldMine,
  Ruins: GiAncientRuins,
  Stable: GiStable,
  Tailor: GiShirt,
  Tavern: GiBeerStein,
  Temple: GiChurch,
  Tower: GiStoneTower,
};

const DISTRICT_TYPE_ICONS: Record<DistrictType, IconType> = {
  Residential: GiHouse,
  Scientific: GiBubblingFlask,
  CityCenter: GiFountain,
  CityEntrance: GiMedievalGate,
  Governmental: GiCrown,
  HolySite: GiHolySymbol,
  Encampment: GiCampingTent,
};

interface NearbyPanelProps {
  scene: SceneSnapshot;
  onOpenQuestJournal: () => void;
  onQuestDialogRequested: (dialog: QuestDialogState) => void;
  onDeliverItemDialogRequested: (dialog: DeliverItemDialogState) => void;
  onTheftEncounter?: (encounterId: string) => void;
}

export function NearbyPanel({
  scene,
  onOpenQuestJournal,
  onQuestDialogRequested,
  onDeliverItemDialogRequested,
  onTheftEncounter,
}: NearbyPanelProps) {
  const chatHub = useChatHub();
  const { submitNarratedTurn } = useGameChat();
  const [inventoryTarget, setInventoryTarget] = useState<{
    id: string;
    name: string;
    ownerType: OwnerType;
    transfersEnabled: boolean;
  } | null>(null);
  const [isTransferOpen, setIsTransferOpen] = useState(false);
  const [tradeWorker, setTradeWorker] = useState<{
    name: string;
    workstationId: string;
  } | null>(null);
  const [isTradeOpen, setIsTradeOpen] = useState(false);
  const [isSleepOpen, setIsSleepOpen] = useState(false);
  const [bookshelf, setBookshelf] = useState<{ id: string; name: string } | null>(null);
  const [caravan, setCaravan] = useState<NearbyCaravanSnapshot | null>(null);
  const [sign, setSign] = useState<{ id: string; name: string } | null>(null);

  const handleQuestDialog = async (giverId: string, questId: string) => {
    const response = await getQuestDialog({
      path: { playerId: scene.playerStatus.id },
      query: { worldId: scene.worldId, giverId, questId },
    });
    if (response.data) {
      onQuestDialogRequested({ ...response.data, worldId: scene.worldId });
    }
  };

  const handleDeliverItemDialog = async (recipientId: string) => {
    const response = await getDeliverItemDialog({
      path: { playerId: scene.playerStatus.id },
      query: { worldId: scene.worldId, recipientId },
    });
    if (response.data) {
      onDeliverItemDialogRequested({ ...response.data, recipientId, worldId: scene.worldId });
    }
  };

  const nearbyBuildings = scene.nearbyBuildings.map((b) => ({
    ...b,
    entityType: 'Building' as const,
  }));
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
      {scene.exits.length > 0 && (
        <Section title="Nearby Exits">
          {scene.exits.map((exit, index) => (
            <div key={index} className="flex items-center gap-1.5 py-1.5">
              <ExitDirectionArrow direction={exit.direction ?? null} />
              <ExitDestinationIcon destination={exit.destination} />
              <span className="truncate font-medium">{exit.destination.name}</span>
              <ExitFamiliarity isVisited={exit.isVisited} isWayBack={exit.isWayBack} />
            </div>
          ))}
        </Section>
      )}

      {scene.nearbyCreatures.length > 0 && (
        <Section title="Nearby Creatures">
          {scene.nearbyCreatures.map((creature) => (
            <CreatureRow
              key={creature.id}
              creature={creature}
              playerLevel={scene.playerStatus.level}
              onOpenInventory={() => {
                setInventoryTarget({
                  id: creature.id,
                  name: creature.name,
                  ownerType: 'Creature',
                  transfersEnabled:
                    creature.state === 'Dead' || HUMANOID_CREATURE_TYPES.has(creature.creatureType),
                });
                setIsTransferOpen(true);
              }}
              onTrade={() => {
                if (creature.tradeWorkstationId) {
                  setTradeWorker({
                    name: creature.name,
                    workstationId: creature.tradeWorkstationId,
                  });
                  setIsTradeOpen(true);
                }
              }}
              tradeEnabled={Boolean(creature.tradeWorkstationId)}
              onQuestDialog={(questId) => void handleQuestDialog(creature.id, questId)}
              onDeliverItem={() => void handleDeliverItemDialog(creature.id)}
            />
          ))}
        </Section>
      )}

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
                onClick={() =>
                  submitNarratedTurn(
                    `Activate ${trigger.name}`,
                    chatHub.sendActivateTrigger(trigger.id),
                  )
                }
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

      {scene.nearbyCaravans.length > 0 && (
        <Section title="Caravans">
          {scene.nearbyCaravans.map((nearbyCaravan) => (
            <div
              key={nearbyCaravan.caravanId}
              className="flex items-center justify-between gap-2 py-1.5"
            >
              <span className="flex min-w-0 items-center gap-1.5">
                <GiHorseHead className="text-muted-foreground size-[18px] shrink-0" />
                <button
                  type="button"
                  onClick={() => setCaravan(nearbyCaravan)}
                  className="cursor-pointer truncate font-medium underline decoration-dotted underline-offset-2"
                >
                  {nearbyCaravan.routeName}
                </button>
              </span>
            </div>
          ))}
        </Section>
      )}

      {nearbyBuildings.length > 0 && (
        <Section title="Nearby Buildings">
          {nearbyBuildings.map((poi) => {
            const Icon = BUILDING_TYPE_ICONS[poi.type];
            return (
              <div key={poi.id} className="flex items-center justify-between gap-2 py-1.5">
                <span className="flex min-w-0 items-center gap-1.5">
                  <Icon className="text-muted-foreground size-[18px] shrink-0" />
                  <EntityTooltip
                    id={poi.id}
                    name={poi.name}
                    entityType={poi.entityType}
                    side="left"
                  >
                    <span className="cursor-help truncate font-medium">{poi.name}</span>
                  </EntityTooltip>
                </span>
                <span className="bg-muted text-muted-foreground shrink-0 rounded-full px-2 py-0.5 text-[10px]">
                  {poi.typeDescription}
                </span>
              </div>
            );
          })}
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
      {tradeWorker && (
        <TradeDialog
          playerId={scene.playerStatus.id}
          worldId={scene.worldId}
          workstationId={tradeWorker.workstationId}
          workerName={tradeWorker.name}
          shopName={scene.buildingName ?? 'Shop'}
          open={isTradeOpen}
          onClose={() => setIsTradeOpen(false)}
        />
      )}
      <SleepDialog open={isSleepOpen} onClose={() => setIsSleepOpen(false)} />

      <CaravanDialog caravan={caravan} onClose={() => setCaravan(null)} />

      <BookshelfDialog
        playerId={scene.playerStatus.id}
        workstationId={bookshelf?.id ?? null}
        name={bookshelf?.name ?? ''}
        open={bookshelf !== null}
        onClose={() => setBookshelf(null)}
      />

      <SignDialog
        worldId={scene.worldId}
        signId={sign?.id ?? null}
        name={sign?.name ?? ''}
        open={sign !== null}
        onClose={() => setSign(null)}
      />
    </div>
  );
}

function ExitDestinationIcon({ destination }: { destination: NearbyExitDestination }) {
  const Icon = pickExitIcon(destination);

  return <Icon className="text-muted-foreground size-4 shrink-0" />;
}

// A room's own role beats its building's type: every room of a dungeon shares one building, so the
// building icon would draw the same glyph beside every exit.
function pickExitIcon(destination: NearbyExitDestination): IconType {
  if (destination.name === 'Outside') {
    return GiExitDoor;
  }

  if (destination.$type === 'Room' && destination.role) {
    return ROOM_ROLE_ICONS[destination.role];
  }

  if (destination.$type === 'District' && destination.districtType) {
    return DISTRICT_TYPE_ICONS[destination.districtType];
  }

  if (
    (destination.$type === 'Building' || destination.$type === 'Room') &&
    destination.buildingType
  ) {
    return BUILDING_TYPE_ICONS[destination.buildingType];
  }

  return GiMountains;
}

function CreatureRow({
  creature,
  playerLevel,
  onOpenInventory,
  onTrade,
  tradeEnabled,
  onQuestDialog,
  onDeliverItem,
}: {
  creature: CreatureStatusSnapshot;
  playerLevel: number | string;
  onOpenInventory: () => void;
  onTrade: () => void;
  tradeEnabled: boolean;
  onQuestDialog: (questId: string) => void;
  onDeliverItem: () => void;
}) {
  const dead = creature.state === 'Dead';
  const dangerous = !dead && isDangerous(Number(creature.level), Number(playerLevel));
  const reputation = creature.reputation == null ? null : Number(creature.reputation);
  const RaceIcon = CREATURE_TYPE_ICON[creature.creatureType];
  const questMarkers = creature.questMarkers ?? [];
  const hasAvailableQuest = questMarkers.some((marker) => marker.marker === 'Available');
  const hasReadyToTurnInQuest = questMarkers.some((marker) => marker.marker === 'ReadyToTurnIn');

  return (
    <div className={cn('flex items-center gap-2 py-1.5', dead && 'opacity-45')}>
      <span className="border-border bg-muted relative flex size-8 shrink-0 items-center justify-center rounded-full border">
        <RaceIcon className="text-muted-foreground size-4" aria-label={creature.creatureType} />
        {dead ? (
          <span
            className="bg-muted text-muted-foreground border-sidebar absolute -top-1 -right-1 flex size-[15px] items-center justify-center rounded-full border-2"
            aria-label="Dead"
          >
            <GiTombstone className="size-2.5" />
          </span>
        ) : hasAvailableQuest ? (
          <span
            className="bg-stamina border-sidebar text-sidebar absolute -top-1 -right-1 flex size-[15px] items-center justify-center rounded-full border-2"
            aria-label="Has a quest available"
          >
            <CircleHelp className="size-2.5" />
          </span>
        ) : hasReadyToTurnInQuest ? (
          <span
            className="bg-stamina border-sidebar text-sidebar absolute -top-1 -right-1 flex size-[15px] items-center justify-center rounded-full border-2 text-[10px] font-black"
            aria-label="Has a quest ready to turn in"
          >
            !
          </span>
        ) : creature.readyToDeliver ? (
          <span
            className="bg-stamina border-sidebar text-sidebar absolute -top-1 -right-1 flex size-[15px] items-center justify-center rounded-full border-2"
            aria-label="You have something to give them"
          >
            <Package className="size-2.5" />
          </span>
        ) : (
          dangerous && (
            <span
              className="bg-muted border-sidebar absolute -top-1 -right-1 flex size-[15px] items-center justify-center rounded-full border-2"
              aria-label="Much more powerful than you"
            >
              <GiDeathSkull className="size-2.5" />
            </span>
          )
        )}
        <span className="text-muted-foreground absolute -bottom-1.5 left-1/2 -translate-x-1/2 px-1 text-[9px] font-bold tabular-nums">
          {creature.level}
        </span>
      </span>

      <span className="min-w-0 flex-1">
        <button
          type="button"
          onClick={onOpenInventory}
          className={cn(
            'block w-full cursor-pointer truncate text-left font-medium underline decoration-dotted underline-offset-2',
            reputation != null && reputation > 0 && 'text-heal',
            reputation != null && reputation < 0 && 'text-destructive',
          )}
        >
          {creature.name}
        </button>
        {creature.state && (
          <span className="bg-muted text-muted-foreground rounded-full px-2 py-0.5 text-[10px]">
            {creature.state}
          </span>
        )}
      </span>

      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="icon-xs" aria-label={`Actions for ${creature.name}`}>
            <MoreVertical />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuItem onClick={onOpenInventory}>Inspect</DropdownMenuItem>
          {!dead && tradeEnabled && <DropdownMenuItem onClick={onTrade}>Trade</DropdownMenuItem>}
          {!dead &&
            questMarkers.map((marker) => (
              <DropdownMenuItem key={marker.questId} onClick={() => onQuestDialog(marker.questId)}>
                Quest: {marker.name}
              </DropdownMenuItem>
            ))}
          {!dead && creature.readyToDeliver && (
            <DropdownMenuItem onClick={onDeliverItem}>Give Item</DropdownMenuItem>
          )}
        </DropdownMenuContent>
      </DropdownMenu>
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
