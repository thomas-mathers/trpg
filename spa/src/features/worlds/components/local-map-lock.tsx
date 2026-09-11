import { DoorClosed, LockKeyhole } from 'lucide-react';

import type { LocalMapLockKind } from '@/api/client';

import { LockedPassageSymbol } from './locked-passage-symbol';

export function MapLockSymbol({ kind }: { kind: LocalMapLockKind }) {
  return kind === 'Portcullis' ? (
    <LockedPassageSymbol className="size-full" />
  ) : (
    <span className="relative block size-full" aria-hidden="true">
      <DoorClosed className="size-full" />
      <LockKeyhole className="bg-card absolute -right-1 -bottom-1 size-1/2 rounded-sm" />
    </span>
  );
}

export function MapLock({ kind, angle }: { kind: LocalMapLockKind; angle: number }) {
  const label =
    kind === 'Portcullis'
      ? 'Locked portcullis · Operated by a lever'
      : kind === 'KeyLockedDoor'
        ? 'Locked door · Requires a key'
        : 'Locked door';
  return (
    <span
      role="img"
      aria-label={label}
      className="nodrag nopan text-destructive flex size-16 items-center justify-center"
      style={{ transform: `rotate(${kind === 'Portcullis' ? angle : 0}deg)` }}
    >
      <MapLockSymbol kind={kind} />
    </span>
  );
}
