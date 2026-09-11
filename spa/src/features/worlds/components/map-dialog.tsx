import { useQuery } from '@tanstack/react-query';
import { ReactFlowProvider } from '@xyflow/react';
import { useEffect } from 'react';
import {
  GiCastle,
  GiHouse,
  GiMountains,
  GiPerson,
  GiScrollUnfurled,
  GiTombstone,
} from 'react-icons/gi';

import { getLocalMapOptions, getWorldMapOptions } from '@/api/client';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { LocalMap } from '@/features/worlds/components/local-map';
import { WorldMap } from '@/features/worlds/components/world-map';
import { gameEventBus } from '@/lib/game-event-bus';

const WORLD_LEGEND_ITEMS = [
  { Icon: GiCastle, label: 'Capital city' },
  { Icon: GiHouse, label: 'City' },
  { Icon: GiMountains, label: 'Rural / wilderness' },
  { Icon: GiPerson, label: 'You are here' },
  { Icon: GiTombstone, label: 'Your unlooted remains' },
  { Icon: GiScrollUnfurled, label: 'Active quest objective' },
];

interface MapDialogProps {
  playerId: string;
  isInsideBuilding: boolean;
  open: boolean;
  onClose: () => void;
}

export function MapDialog({ playerId, isInsideBuilding, open, onClose }: MapDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent
        className="flex h-[min(94vh,880px)] flex-col gap-0 overflow-hidden p-0 md:max-w-7xl"
        onPointerDownOutside={(event) => event.preventDefault()}
      >
        <ReactFlowProvider>
          {isInsideBuilding ? (
            <LocalMapDialogBody playerId={playerId} open={open} onClose={onClose} />
          ) : (
            <WorldMapDialogBody playerId={playerId} open={open} onClose={onClose} />
          )}
        </ReactFlowProvider>
      </DialogContent>
    </Dialog>
  );
}

function LocalMapDialogBody({
  playerId,
  open,
  onClose,
}: {
  playerId: string;
  open: boolean;
  onClose: () => void;
}) {
  const map = useQuery({ ...getLocalMapOptions({ path: { playerId } }), enabled: open });
  const { refetch } = map;
  useEffect(
    () =>
      gameEventBus.on('SceneSnapshot', () => {
        if (open) void refetch();
      }),
    [open, refetch],
  );

  return (
    <>
      <DialogHeader>
        <DialogTitle className="mx-0 mt-0 rounded-none">Map</DialogTitle>
      </DialogHeader>
      {!map.data ? (
        <div className="flex flex-1 items-center justify-center py-12">
          <p className="text-muted-foreground text-sm">Loading map...</p>
        </div>
      ) : (
        <LocalMap map={map.data} />
      )}
      <DialogFooter className="mx-0 mb-0 rounded-none">
        <Button variant="outline" onClick={onClose}>
          Close
        </Button>
      </DialogFooter>
    </>
  );
}

function WorldMapDialogBody({
  playerId,
  open,
  onClose,
}: {
  playerId: string;
  open: boolean;
  onClose: () => void;
}) {
  const map = useQuery({ ...getWorldMapOptions({ path: { playerId } }), enabled: open });

  return (
    <>
      <DialogHeader>
        <DialogTitle className="mx-0 mt-0 rounded-none">Map</DialogTitle>
      </DialogHeader>
      {!map.data ? (
        <div className="flex flex-1 items-center justify-center py-12">
          <p className="text-muted-foreground text-sm">Loading map...</p>
        </div>
      ) : (
        <>
          <WorldMap {...map.data} />
          <WorldMapLegend />
        </>
      )}
      <DialogFooter className="mx-0 mb-0 rounded-none">
        <Button variant="outline" onClick={onClose}>
          Close
        </Button>
      </DialogFooter>
    </>
  );
}

function WorldMapLegend() {
  return (
    <div className="flex flex-wrap gap-x-4 gap-y-1.5 border-t px-4 py-3">
      {WORLD_LEGEND_ITEMS.map(({ Icon, label }) => (
        <span key={label} className="text-muted-foreground flex items-center gap-1.5 text-xs">
          <Icon className="text-foreground size-4 shrink-0" />
          {label}
        </span>
      ))}
    </div>
  );
}
