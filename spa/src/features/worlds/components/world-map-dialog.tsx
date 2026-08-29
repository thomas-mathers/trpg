import { useQuery } from '@tanstack/react-query';
import { ReactFlowProvider } from '@xyflow/react';
import {
  GiCastle,
  GiHouse,
  GiMountains,
  GiPerson,
  GiScrollUnfurled,
  GiTombstone,
} from 'react-icons/gi';

import { getWorldMapOptions } from '@/api/client';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { WorldMap } from '@/features/worlds/components/world-map';

const LEGEND_ITEMS = [
  { Icon: GiCastle, label: 'Capital city' },
  { Icon: GiHouse, label: 'City' },
  { Icon: GiMountains, label: 'Rural / wilderness' },
  { Icon: GiPerson, label: 'You are here' },
  { Icon: GiTombstone, label: 'Your unlooted remains' },
  { Icon: GiScrollUnfurled, label: 'Active quest objective' },
];

interface WorldMapDialogProps {
  playerId: string;
  open: boolean;
  onClose: () => void;
}

export function WorldMapDialog({ playerId, open, onClose }: WorldMapDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent
        className="flex h-[min(94vh,880px)] flex-col gap-4 md:max-w-7xl"
        onPointerDownOutside={(event) => event.preventDefault()}
      >
        <ReactFlowProvider>
          <WorldMapDialogBody playerId={playerId} onClose={onClose} />
        </ReactFlowProvider>
      </DialogContent>
    </Dialog>
  );
}

function WorldMapDialogBody({ playerId, onClose }: { playerId: string; onClose: () => void }) {
  const map = useQuery(getWorldMapOptions({ path: { playerId } }));

  return (
    <>
      <DialogHeader>
        <DialogTitle>World Map</DialogTitle>
      </DialogHeader>

      {!map.data ? (
        <div className="flex flex-1 items-center justify-center py-12">
          <p className="text-muted-foreground text-sm">Loading map...</p>
        </div>
      ) : (
        <>
          <WorldMap
            countries={map.data.countries}
            states={map.data.states}
            cities={map.data.cities}
            roads={map.data.roads}
            playerStateId={map.data.playerStateId}
            corpses={map.data.corpses}
            questMarkers={map.data.questMarkers}
          />
          <WorldMapLegend />
        </>
      )}

      <DialogFooter>
        <Button variant="outline" onClick={onClose}>
          Close
        </Button>
      </DialogFooter>
    </>
  );
}

function WorldMapLegend() {
  return (
    <div className="flex flex-wrap gap-x-4 gap-y-1.5 border-t pt-3">
      {LEGEND_ITEMS.map(({ Icon, label }) => (
        <span key={label} className="text-muted-foreground flex items-center gap-1.5 text-xs">
          <Icon className="text-foreground size-4 shrink-0" />
          {label}
        </span>
      ))}
    </div>
  );
}
