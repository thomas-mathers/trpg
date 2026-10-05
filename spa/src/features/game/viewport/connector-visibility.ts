import type {
  FootprintWire,
  LocationBoundarySnapshot,
  NearbyExitDestination,
  NearbyExitSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { isOnOpenEdge } from './boundary-geometry';

type DestinationKind = 'District' | 'Building' | 'Room' | 'Wilderness';

export interface BuildingNameBoard {
  exit: NearbyExitSnapshot;
  text: string;
  width: number;
}

const MIN_NAME_BOARD_WIDTH = 1.2;
const MAX_NAME_BOARD_WIDTH = 2.4;
const NAME_BOARD_WIDTH_PER_CHARACTER = 0.13;
const NAME_BOARD_PADDING = 0.4;

function destinationKind({ destination }: NearbyExitSnapshot): DestinationKind | undefined {
  return (destination as NearbyExitDestination & { $type?: DestinationKind }).$type;
}

export function hasDoor(
  exit: NearbyExitSnapshot,
  boundary: LocationBoundarySnapshot | undefined,
  size: FootprintWire,
): boolean {
  const kind = destinationKind(exit);
  if (!boundary || (kind !== 'District' && kind !== 'Wilderness')) {
    return true;
  }

  const isGate = boundary.gates.some(({ connectorId }) => connectorId === exit.connectorId);
  return !isGate && !isOnOpenEdge(exit.placement, size, boundary.openEdges);
}

export function buildingNameBoards(exits: NearbyExitSnapshot[]): BuildingNameBoard[] {
  return exits
    .filter((exit) => !exit.stairs && destinationKind(exit) === 'Building')
    .map((exit) => ({
      exit,
      text: exit.destination.name,
      width: nameBoardWidth(exit.destination.name),
    }));
}

function nameBoardWidth(text: string): number {
  const fitted = text.length * NAME_BOARD_WIDTH_PER_CHARACTER + NAME_BOARD_PADDING;
  return Math.min(Math.max(fitted, MIN_NAME_BOARD_WIDTH), MAX_NAME_BOARD_WIDTH);
}
