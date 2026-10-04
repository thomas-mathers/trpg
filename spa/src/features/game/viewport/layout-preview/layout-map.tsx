import { useMemo, useRef, useState, type PointerEvent } from 'react';

import { doorOf, type District } from './district-generator';
import { PopulationControls } from './population-controls';
import { PopulationLayer } from './population-layer';
import { districtPopulation } from './preview-population';

export function LayoutMap({
  district,
  seed,
  selected,
  onSelect,
}: {
  district: District;
  seed: number;
  selected?: number;
  onSelect: (id: number) => void;
}) {
  const population = useMemo(() => districtPopulation(district, seed), [district, seed]);
  const [visible, setVisible] = useState(['creatures', 'props', 'labels']);
  const { width, depth, buildings, streets, courts, square } = district;
  const [zoom, setZoom] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const drag = useRef<{
    x: number;
    y: number;
    startX: number;
    startY: number;
    moved: boolean;
  } | null>(null);
  function move(event: PointerEvent<SVGSVGElement>) {
    if (!drag.current) return;
    const bounds = event.currentTarget.getBoundingClientRect();
    const ratio = Math.max(width / bounds.width, depth / bounds.height) / zoom;
    const dx = event.clientX - drag.current.x;
    const dy = event.clientY - drag.current.y;
    if (Math.abs(dx) + Math.abs(dy) > 5) drag.current.moved = true;
    setOffset({ x: drag.current.startX - dx * ratio, y: drag.current.startY - dy * ratio });
  }
  return (
    <div>
      <PopulationControls population={population} visible={visible} onChange={setVisible} />
      <div className="layout-map">
        <svg
          aria-label="District plan"
          viewBox={`${offset.x - 3} ${offset.y - 3} ${(width + 6) / zoom} ${(depth + 6) / zoom}`}
          onPointerDown={(event) => {
            drag.current = {
              x: event.clientX,
              y: event.clientY,
              startX: offset.x,
              startY: offset.y,
              moved: false,
            };
          }}
          onPointerMove={move}
          onPointerUp={() => {
            setTimeout(() => {
              drag.current = null;
            }, 0);
          }}
          onPointerCancel={() => {
            drag.current = null;
          }}
        >
          <rect x={0} y={0} width={width} height={depth} fill="#8b8972" />
          {streets.map(({ x, y, width: w, depth: d }, index) => (
            <rect key={index} x={x} y={y} width={w} height={d} fill="#d5cbb4" />
          ))}
          {courts.map(({ x, y, width: w, depth: d }, index) => (
            <rect key={index} x={x} y={y} width={w} height={d} fill="#9ea581" />
          ))}
          <rect
            x={square.x}
            y={square.y}
            width={square.width}
            height={square.depth}
            fill="#e3d5b4"
          />
          {square.width > 0 && (
            <text
              x={square.x + square.width / 2}
              y={square.y + square.depth - 2}
              textAnchor="middle"
              fill="#6c624c"
              fontSize={2}
            >
              {district.squareName}
            </text>
          )}
          {buildings.map((building) => {
            const { id, x, y, width: w, depth: d, color, name } = building;
            const door = doorOf(building);
            return (
              <g
                key={id}
                role="button"
                tabIndex={0}
                aria-label={`Inspect ${name} ${id}`}
                onClick={() => {
                  if (!drag.current?.moved) onSelect(id);
                }}
                onKeyDown={(event) => {
                  if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    onSelect(id);
                  }
                }}
              >
                <rect x={x + 0.7} y={y + 0.9} width={w} height={d} fill="#343c3940" />
                <rect
                  x={x}
                  y={y}
                  width={w}
                  height={d}
                  fill={color}
                  stroke={selected === id ? '#fff7ba' : '#61564a'}
                  strokeWidth={selected === id ? 0.9 : 0.35}
                />
                <path
                  d={`M ${x + w * 0.15} ${y + d / 2} H ${x + w * 0.85}`}
                  stroke="#695444"
                  strokeWidth={0.25}
                  opacity={0.5}
                />
                <text
                  x={x + w / 2}
                  y={y + d / 2 + 0.7}
                  textAnchor="middle"
                  fontSize={buildings.length <= 10 ? 1.6 : 2.2}
                  fill="#312e29"
                  pointerEvents="none"
                >
                  {buildings.length <= 10 ? name : id}
                </text>
                <circle
                  cx={door.x}
                  cy={door.y}
                  r={0.75}
                  fill="#254e57"
                  stroke="#e5ecdf"
                  strokeWidth={0.2}
                />
              </g>
            );
          })}
          <PopulationLayer population={population} visible={visible} labelSize={0.8} />
          <path
            d={`M 2 ${depth - 2} h 10 m -10 -1 v 2 m 10 -2 v 2`}
            stroke="#293d37"
            strokeWidth={0.3}
          />
          <text x={7} y={depth - 3.5} fontSize={2} textAnchor="middle" fill="#293d37">
            10 m
          </text>
        </svg>
        <div className="layout-map-tools">
          <button aria-label="Zoom in" onClick={() => setZoom(Math.min(6, zoom * 1.4))}>
            +
          </button>
          <button aria-label="Zoom out" onClick={() => setZoom(Math.max(1, zoom / 1.4))}>
            −
          </button>
          <button
            onClick={() => {
              setZoom(1);
              setOffset({ x: 0, y: 0 });
            }}
          >
            Fit
          </button>
        </div>
      </div>
    </div>
  );
}
