import { LockKeyhole } from 'lucide-react';
import { GiChest, GiOpenChest, GiLever, GiTombstone } from 'react-icons/gi';

import type { LocalMapMarkerResponse } from '@/api/client';
import { cn } from '@/lib/utils';

const stateLabels = {
  ContainsItems: 'Contains items',
  Empty: 'Empty',
  Unactivated: 'Not activated',
  Activated: 'Activated',
  RecoverableLoot: 'Recoverable items remain',
};

export function RoomMarkers({ markers }: { markers: LocalMapMarkerResponse[] }) {
  const groups = ['PlayerCorpse', 'Lever', 'Chest'].flatMap((kind) => {
    const matches = markers.filter((marker) => marker.kind === kind);
    return matches.length ? [matches] : [];
  });
  return groups.map((group) => <MarkerGroup key={group[0].kind} markers={group} />);
}

function MarkerGroup({ markers }: { markers: LocalMapMarkerResponse[] }) {
  const marker =
    markers.find(
      (candidate) => candidate.state === 'ContainsItems' || candidate.state === 'Unactivated',
    ) ?? markers[0];
  const { kind, state, isLocked } = marker;
  const Icon =
    kind === 'PlayerCorpse'
      ? GiTombstone
      : kind === 'Lever'
        ? GiLever
        : state === 'Empty'
          ? GiOpenChest
          : GiChest;
  const complete = markers.every(
    (candidate) => candidate.state === 'Empty' || candidate.state === 'Activated',
  );
  const label =
    markers.length === 1
      ? markerLabel(marker)
      : `${markers.length} ${kind === 'PlayerCorpse' ? 'player corpses' : kind === 'Lever' ? 'levers' : 'chests'}`;
  return (
    <span
      role="img"
      aria-label={label}
      className={cn(
        'nodrag nopan relative flex size-6 shrink-0 items-center justify-center rounded-sm align-middle focus-visible:outline-2',
        complete && 'text-muted-foreground',
        kind === 'PlayerCorpse' && 'bg-primary text-primary-foreground ring-1 ring-primary',
      )}
    >
      <Icon className={kind === 'Lever' ? 'size-5' : 'size-6'} aria-hidden="true" />
      {isLocked && (
        <LockKeyhole
          className="bg-card absolute -top-1 -right-1 size-4 rounded-full ring-1 ring-current"
          aria-hidden="true"
        />
      )}
      {markers.length > 1 && (
        <span className="bg-card text-foreground absolute -top-1 -left-1 rounded px-0.5 text-[10px] leading-3 ring-1 ring-current">
          {markers.length}
        </span>
      )}
    </span>
  );
}

function markerLabel({ name, state, isLocked, itemCount }: LocalMapMarkerResponse): string {
  return `${name}: ${stateLabels[state]}${isLocked ? ' · Key required' : ''}${itemCount == null ? '' : ` (${itemCount} item stacks)`}`;
}
