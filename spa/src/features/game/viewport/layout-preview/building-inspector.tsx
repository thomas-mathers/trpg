import { useState } from 'react';

import type { Building } from './district-generator';
import { floorNames } from './interior-generator';
import { InteriorMap } from './interior-map';

export function BuildingInspector({
  building,
  seed,
  onClose,
}: {
  building: Building;
  seed: number;
  onClose: () => void;
}) {
  const [floor, setFloor] = useState(0);
  const floors = floorNames(building, seed);
  return (
    <section className="layout-inspector" aria-label="Selected building">
      <div>
        <div className="layout-eyebrow">Building {building.id}</div>
        <h2>{building.name}</h2>
        <p>
          {building.frontage.toFixed(1)} m frontage × {building.length.toFixed(1)} m deep ·{' '}
          {(building.width * building.depth).toFixed(0)} m² footprint · {building.height.toFixed(1)}{' '}
          m tall, {floors.length} floors
        </p>
        <label>
          Floor
          <select value={floor} onChange={(event) => setFloor(Number(event.target.value))}>
            {floors.map((_, index) => (
              <option key={index} value={index}>
                {index === 0 ? 'Ground floor' : `Floor ${index}`}
              </option>
            ))}
          </select>
        </label>
        <p>{floors[floor].join(' · ')}</p>
        <p>
          {floor === 0 ? 'Entrance at top' : 'Upper floor'} · stairs at rear · person shown to scale
        </p>
        <button onClick={onClose}>Close interior</button>
      </div>
      <InteriorMap building={building} seed={seed} floor={floor} />
    </section>
  );
}
