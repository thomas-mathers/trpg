import type { CountryMapResponse, PointResponse, StateMapResponse } from '@/api/client';

const DISPLAY_EXTENT = 10_000;

interface WorldMapGeometry {
  countries: CountryMapResponse[];
  states: StateMapResponse[];
}

export function normalizeWorldMapGeometry(
  countries: CountryMapResponse[],
  states: StateMapResponse[],
): WorldMapGeometry {
  const points =
    countries.length > 0
      ? countries.flatMap((country) => country.boundary)
      : states.flatMap((state) => state.boundary);

  if (points.length === 0) return { countries, states };

  const minimumX = Math.min(...points.map((point) => point.x));
  const minimumY = Math.min(...points.map((point) => point.y));
  const width = Math.max(...points.map((point) => point.x)) - minimumX;
  const height = Math.max(...points.map((point) => point.y)) - minimumY;
  const extent = Math.max(width, height);

  if (extent === 0) return { countries, states };

  const scale = DISPLAY_EXTENT / extent;
  const normalizePoint = (point: PointResponse): PointResponse => ({
    x: (point.x - minimumX) * scale,
    y: (point.y - minimumY) * scale,
  });

  return {
    countries: countries.map((country) => ({
      ...country,
      boundary: country.boundary.map(normalizePoint),
    })),
    states: states.map((state) => ({
      ...state,
      center: normalizePoint(state.center),
      boundary: state.boundary.map(normalizePoint),
    })),
  };
}
