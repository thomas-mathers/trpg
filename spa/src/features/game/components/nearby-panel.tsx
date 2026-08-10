import { Crown, Skull } from 'lucide-react';
import {
  Anvil,
  BedDouble,
  Beer,
  BookOpen,
  Building2,
  Castle,
  Church,
  Croissant,
  Cross,
  FlaskConical,
  Gem,
  Hammer,
  House,
  Landmark,
  type LucideIcon,
  Lock,
  Mountain,
  Pickaxe,
  Shirt,
  ShoppingBag,
  Sparkles,
  Swords,
  Users,
  Warehouse,
} from 'lucide-react';
import type { ReactNode } from 'react';
import { useState } from 'react';

import type {
  BuildingType,
  CreatureStatusSnapshot,
  DistrictType,
  NearbyExitDestination,
  SceneSnapshot,
} from '@/api/client';
import { isDangerous } from '@/features/combat/threat-level';
import { EntityTooltip } from '@/features/game/components/entity-tooltip';
import { TradeDialog } from '@/features/inventory/components/trade-dialog';
import { TransferItemDialog } from '@/features/inventory/components/transfer-item-dialog';
import { cn } from '@/lib/utils';

const BUILDING_TYPE_ICONS: Record<BuildingType, LucideIcon> = {
  ArcaneShop: Sparkles,
  Apothecary: FlaskConical,
  Bakery: Croissant,
  Barracks: Swords,
  Blacksmith: Anvil,
  Carpenter: Hammer,
  Castle: Castle,
  Cave: Mountain,
  Crypt: Cross,
  GeneralGoods: ShoppingBag,
  GuildHall: Users,
  House: House,
  Inn: BedDouble,
  Jail: Lock,
  Jeweler: Gem,
  Library: BookOpen,
  Mine: Pickaxe,
  Ruins: Landmark,
  Stable: Warehouse,
  Tailor: Shirt,
  Tavern: Beer,
  Temple: Church,
  Tower: Building2,
};

const DISTRICT_TYPE_ICONS: Record<DistrictType, LucideIcon> = {
  Residential: House,
  Scientific: FlaskConical,
  CityCenter: Landmark,
  Governmental: Crown,
  HolySite: Church,
  Encampment: Swords,
};

interface NearbyPanelProps {
  scene: SceneSnapshot;
}

export function NearbyPanel({ scene }: NearbyPanelProps) {
  const [inventoryTarget, setInventoryTarget] = useState<{
    id: string;
    name: string;
    transfersEnabled: boolean;
  } | null>(null);
  const [isTransferOpen, setIsTransferOpen] = useState(false);
  const [tradeWorker, setTradeWorker] = useState<{
    name: string;
    workstationId: string;
  } | null>(null);

  const nearbyBuildings = scene.nearbyBuildings.map((b) => ({
    ...b,
    entityType: 'Building' as const,
  }));

  return (
    <div className="flex flex-col gap-6 p-4 text-sm">
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
                  transfersEnabled: creature.state === 'Dead',
                });
                setIsTransferOpen(true);
              }}
              onTrade={() => {
                if (creature.tradeWorkstationId) {
                  setTradeWorker({
                    name: creature.name,
                    workstationId: creature.tradeWorkstationId,
                  });
                }
              }}
              tradeEnabled={Boolean(creature.tradeWorkstationId)}
            />
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
                  <Icon className="text-muted-foreground h-[15px] w-[15px] shrink-0" />
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
      />
      {tradeWorker && (
        <TradeDialog
          playerId={scene.playerStatus.id}
          workstationId={tradeWorker.workstationId}
          workerName={tradeWorker.name}
          shopName={scene.buildingName ?? 'Shop'}
          open
          onClose={() => setTradeWorker(null)}
        />
      )}
    </div>
  );
}

function ExitDestinationIcon({ destination }: { destination: NearbyExitDestination }) {
  const Icon =
    destination.$type === 'District'
      ? DISTRICT_TYPE_ICONS[destination.districtType]
      : destination.$type === 'Building' || destination.$type === 'Room'
        ? BUILDING_TYPE_ICONS[destination.buildingType]
        : Mountain;

  return <Icon className="text-muted-foreground size-3 shrink-0" />;
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

  return (
    <div className={cn('flex items-center justify-between gap-2 py-1.5', dead && 'opacity-45')}>
      <span className="flex min-w-0 items-center gap-1.5">
        <span className="flex h-[15px] w-[15px] shrink-0 items-center justify-center">
          {dead ? (
            <Skull className="h-[15px] w-[15px]" aria-label="Dead" />
          ) : (
            dangerous && (
              <Crown className="h-[15px] w-[15px]" aria-label="Much more powerful than you" />
            )
          )}
        </span>
        <button
          type="button"
          onClick={onOpenInventory}
          className={cn(
            'cursor-pointer truncate font-medium underline decoration-dotted underline-offset-2',
            reputation != null && reputation > 0 && 'text-green-500',
            reputation != null && reputation < 0 && 'text-red-500',
          )}
        >
          {creature.name}
        </button>
        <span className="text-muted-foreground shrink-0 text-xs">Lv {creature.level}</span>
      </span>
      {creature.state && (
        <span className="bg-muted text-muted-foreground shrink-0 rounded-full px-2 py-0.5 text-[10px]">
          {creature.state}
        </span>
      )}
      {!dead && tradeEnabled && (
        <button type="button" onClick={onTrade} className="text-muted-foreground text-xs underline">
          Trade
        </button>
      )}
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
