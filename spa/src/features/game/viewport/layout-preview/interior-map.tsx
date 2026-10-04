import { useMemo, useState } from 'react';

import type { Building } from './district-generator';
import { generateInterior } from './interior-generator';
import { PopulationControls } from './population-controls';
import { PopulationLayer } from './population-layer';
import { interiorPopulation } from './preview-population';

export function InteriorMap({
  building,
  seed,
  floor,
}: {
  building: Building;
  seed: number;
  floor: number;
}) {
  const interior = useMemo(() => generateInterior(building, seed, floor), [building, seed, floor]);
  const { width, depth, rooms, hall, stairs } = interior;
  const population = useMemo(
    () => interiorPopulation(interior, seed + building.id * 71 + floor),
    [interior, seed, building.id, floor],
  );
  const [visible, setVisible] = useState(['creatures', 'props', 'labels']);
  return (
    <div>
      <PopulationControls population={population} visible={visible} onChange={setVisible} />
      <svg
        className="interior-map"
        viewBox={`-1 -1 ${width + 2} ${depth + 2}`}
        aria-label={`Floor ${floor} plan`}
      >
        <rect width={width} height={depth} fill="#c9bda6" stroke="#4c4e49" strokeWidth={0.2} />
        {hall.width > 0 && (
          <rect
            aria-label="Hallway"
            x={hall.x}
            y={hall.y}
            width={hall.width}
            height={hall.depth}
            fill="#ece0cb"
          />
        )}
        {rooms.map(({ x, y, width: w, depth: d, color, name }, index) => (
          <g key={index}>
            <rect
              x={x}
              y={y}
              width={w}
              height={d}
              fill={color}
              stroke="#4c4e49"
              strokeWidth={0.16}
            />
            {hall.width > 0 && (
              <rect
                x={index % 2 ? x - 0.1 : x + w - 0.1}
                y={y + d / 2 - 0.5}
                width={0.2}
                height={1}
                fill="#ece0cb"
              />
            )}
            <text
              x={x + w / 2}
              y={y + d / 2}
              textAnchor="middle"
              fontSize={Math.min(0.8, w / 12)}
              fill="#303b38"
            >
              {name}
            </text>
            <text
              x={x + w / 2}
              y={y + d / 2 + 0.65}
              textAnchor="middle"
              fontSize={0.5}
              fill="#4a554b"
            >
              {(w * d).toFixed(0)} m²
            </text>
          </g>
        ))}
        <path d={`M ${width / 2 - 0.6} 0 h 1.2`} stroke="#254e57" strokeWidth={0.35} />
        <g aria-label="Stairs" transform={`translate(${stairs.x}, ${stairs.y})`}>
          <rect
            width={stairs.width}
            height={stairs.depth}
            fill="#c0b299"
            stroke="#4c4e49"
            strokeWidth={0.1}
          />
          {[0.4, 0.8, 1.2, 1.6, 2].map((y) => (
            <path key={y} d={`M 0 ${y} h ${stairs.width}`} stroke="#4c4e49" strokeWidth={0.1} />
          ))}
        </g>
        <PopulationLayer population={population} visible={visible} />
      </svg>
    </div>
  );
}
