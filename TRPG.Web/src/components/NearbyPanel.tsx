import { DoorOpen, Skull } from 'lucide-react';
import type { ReactNode } from 'react';

import type { CreatureStatusSnapshot, SceneSnapshot } from '@/api/client';
import { EntityTooltip } from '@/components/EntityLink';
import { BUILDING_TYPE_ICONS, DISTRICT_TYPE_ICONS } from '@/lib/place-type-icons';
import { isDangerous } from '@/lib/threat-level';
import { cn } from '@/lib/utils';

interface NearbyPanelProps {
  sessionId: string;
  scene: SceneSnapshot;
}

export function NearbyPanel({ sessionId, scene }: NearbyPanelProps) {
  const pointsOfInterest = [
    ...scene.nearbyBuildings.map((b) => ({ ...b, entityType: 'Building' as const })),
    ...scene.nearbyDungeons.map((b) => ({ ...b, entityType: 'Building' as const })),
    ...scene.nearbyDistricts.map((d) => ({ ...d, entityType: 'District' as const })),
  ];

  return (
    <div className="flex flex-col gap-6 p-4 text-sm">
      <Section title="Exits">
        {scene.exits.length === 0 ? (
          <EmptyState />
        ) : (
          scene.exits.map((exit, index) => (
            <div key={index} className="flex items-start gap-2 py-1.5">
              <DoorOpen className="text-muted-foreground mt-0.5 h-4 w-4 shrink-0" />
              <div className="min-w-0">
                <div>{exit.description}</div>
                <div className="text-muted-foreground text-xs">→ {exit.destinationRoomName}</div>
              </div>
            </div>
          ))
        )}
      </Section>

      <Section title="Creatures nearby" subtitle={`You: Level ${scene.playerStatus.level}`}>
        {scene.nearbyCreatures.length === 0 ? (
          <EmptyState />
        ) : (
          scene.nearbyCreatures.map((creature) => (
            <CreatureRow
              key={creature.id}
              sessionId={sessionId}
              creature={creature}
              playerLevel={scene.playerStatus.level}
            />
          ))
        )}
      </Section>

      <Section title="Points of interest">
        {pointsOfInterest.length === 0 ? (
          <EmptyState />
        ) : (
          pointsOfInterest.map((poi) => {
            const Icon =
              poi.entityType === 'Building'
                ? BUILDING_TYPE_ICONS[poi.type]
                : DISTRICT_TYPE_ICONS[poi.type];
            return (
              <div key={poi.id} className="flex items-center justify-between gap-2 py-1.5">
                <span className="flex min-w-0 items-center gap-1.5">
                  <Icon className="text-muted-foreground h-[15px] w-[15px] shrink-0" />
                  <EntityTooltip
                    sessionId={sessionId}
                    id={poi.id}
                    name={poi.name}
                    entityType={poi.entityType}
                    side="left"
                  >
                    <span className="cursor-help truncate font-semibold">{poi.name}</span>
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

      <Section title="Props">
        {scene.nearbyProps.length === 0 ? (
          <EmptyState />
        ) : (
          scene.nearbyProps.map((prop) => (
            <div key={prop.id} className="py-1.5">
              {prop.name}
            </div>
          ))
        )}
      </Section>
    </div>
  );
}

function CreatureRow({
  sessionId,
  creature,
  playerLevel,
}: {
  sessionId: string;
  creature: CreatureStatusSnapshot;
  playerLevel: number | string;
}) {
  const dangerous = isDangerous(Number(creature.level), Number(playerLevel));
  const reputation = creature.reputation == null ? null : Number(creature.reputation);

  return (
    <div className="flex items-center justify-between gap-2 py-1.5">
      <span className="flex min-w-0 items-center gap-1.5">
        <span className="flex h-[15px] w-[15px] shrink-0 items-center justify-center">
          {dangerous && (
            <Skull className="h-[15px] w-[15px]" aria-label="Much more powerful than you" />
          )}
        </span>
        <EntityTooltip
          sessionId={sessionId}
          id={creature.id}
          name={creature.name}
          entityType="Creature"
          side="left"
        >
          <span
            className={cn(
              'cursor-help truncate font-semibold',
              reputation != null && reputation > 0 && 'text-green-500',
              reputation != null && reputation < 0 && 'text-red-500',
            )}
          >
            {creature.name}
          </span>
        </EntityTooltip>
        <span className="text-muted-foreground shrink-0 text-xs">Lv {creature.level}</span>
      </span>
      {creature.state && (
        <span className="bg-muted text-muted-foreground shrink-0 rounded-full px-2 py-0.5 text-[10px]">
          {creature.state}
        </span>
      )}
    </div>
  );
}

function Section({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle?: string;
  children: ReactNode;
}) {
  return (
    <div>
      <p className="text-muted-foreground text-[11px] font-semibold tracking-wider uppercase">
        {title}
      </p>
      {subtitle && <p className="text-muted-foreground -mt-0.5 mb-1 text-xs">{subtitle}</p>}
      <div className="divide-border divide-y">{children}</div>
    </div>
  );
}

function EmptyState() {
  return <p className="text-muted-foreground py-1.5 text-xs italic">Nothing here.</p>;
}
