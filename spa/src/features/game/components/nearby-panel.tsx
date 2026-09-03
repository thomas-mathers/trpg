import { CircleHelp, MoreVertical } from 'lucide-react';
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
  GiHouse,
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
} from 'react-icons/gi';

import type { BuildingType, DistrictType, OwnerType } from '@/api/client';
import type {
  CreatureStatusSnapshot,
  CreatureType,
  NearbyExitDestination as SignalrNearbyExitDestination,
  SceneSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { isDangerous } from '@/features/combat/threat-level';
import { EntityTooltip } from '@/features/game/components/entity-tooltip';
import { SleepDialog } from '@/features/game/components/sleep-dialog';
import { TradeDialog } from '@/features/inventory/components/trade-dialog';
import { TransferItemDialog } from '@/features/inventory/components/transfer-item-dialog';
import { QuestTracker } from '@/features/quests/components/quest-tracker';
import { cn } from '@/lib/utils';

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
  onTheftEncounter?: (encounterId: string) => void;
}

export function NearbyPanel({ scene, onOpenQuestJournal, onTheftEncounter }: NearbyPanelProps) {
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

  const nearbyBuildings = scene.nearbyBuildings.map((b) => ({
    ...b,
    entityType: 'Building' as const,
  }));
  const nearbyContainers = scene.nearbyProps.filter((prop) => prop.type === 'Container');
  const nearbyBeds = scene.nearbyProps.filter((prop) => prop.type === 'Bed');

  return (
    <div className="flex flex-col gap-6 p-4 text-sm">
      <QuestTracker
        playerId={scene.playerStatus.id}
        worldId={scene.worldId}
        onOpenJournal={onOpenQuestJournal}
      />
      <Section title="Nearby Exits">
        {scene.exits.length === 0 ? (
          <EmptyState />
        ) : (
          scene.exits.map((exit, index) => (
            <div key={index} className="flex items-center gap-1.5 py-1.5">
              <span className="text-muted-foreground">→</span>
              <ExitDestinationIcon destination={exit.destination} />
              <span className="truncate font-medium">{exit.destination.name}</span>
            </div>
          ))
        )}
      </Section>

      <Section title="Nearby Creatures">
        {scene.nearbyCreatures.length === 0 ? (
          <EmptyState />
        ) : (
          scene.nearbyCreatures.map((creature) => (
            <CreatureRow
              key={creature.id}
              creature={creature}
              playerLevel={scene.playerStatus.level}
              onOpenInventory={() => {
                setInventoryTarget({
                  id: creature.id,
                  name: creature.name,
                  ownerType: 'Creature',
                  transfersEnabled: true,
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
            />
          ))
        )}
      </Section>

      <Section title="Nearby Containers">
        {nearbyContainers.length === 0 ? (
          <EmptyState />
        ) : (
          nearbyContainers.map((container) => (
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
          ))
        )}
      </Section>

      <Section title="Nearby Beds">
        {nearbyBeds.length === 0 ? (
          <EmptyState />
        ) : (
          nearbyBeds.map((bed) => (
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
          ))
        )}
      </Section>

      <Section title="Nearby Buildings">
        {nearbyBuildings.length === 0 ? (
          <EmptyState />
        ) : (
          nearbyBuildings.map((poi) => {
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
          })
        )}
      </Section>

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
    </div>
  );
}

function ExitDestinationIcon({ destination }: { destination: NearbyExitDestination }) {
  const Icon =
    destination.name === 'Outside'
      ? GiExitDoor
      : destination.$type === 'District' && destination.districtType
        ? DISTRICT_TYPE_ICONS[destination.districtType]
        : (destination.$type === 'Building' || destination.$type === 'Room') &&
            destination.buildingType
          ? BUILDING_TYPE_ICONS[destination.buildingType]
          : GiMountains;

  return <Icon className="text-muted-foreground size-4 shrink-0" />;
}

function CreatureRow({
  creature,
  playerLevel,
  onOpenInventory,
  onTrade,
  tradeEnabled,
}: {
  creature: CreatureStatusSnapshot;
  playerLevel: number | string;
  onOpenInventory: () => void;
  onTrade: () => void;
  tradeEnabled: boolean;
}) {
  const dead = creature.state === 'Dead';
  const dangerous = !dead && isDangerous(Number(creature.level), Number(playerLevel));
  const reputation = creature.reputation == null ? null : Number(creature.reputation);
  const RaceIcon = CREATURE_TYPE_ICON[creature.creatureType];

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
        ) : creature.questMarker === 'Available' ? (
          <span
            className="bg-stamina border-sidebar text-sidebar absolute -top-1 -right-1 flex size-[15px] items-center justify-center rounded-full border-2"
            aria-label="Has a quest available"
          >
            <CircleHelp className="size-2.5" />
          </span>
        ) : creature.questMarker === 'ReadyToTurnIn' ? (
          <span
            className="bg-stamina border-sidebar text-sidebar absolute -top-1 -right-1 flex size-[15px] items-center justify-center rounded-full border-2 text-[10px] font-black"
            aria-label="Has a quest ready to turn in"
          >
            !
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

function EmptyState() {
  return <p className="text-muted-foreground py-1.5 text-xs italic">Nothing here.</p>;
}
