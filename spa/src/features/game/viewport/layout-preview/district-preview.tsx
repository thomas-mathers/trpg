import { useMemo, useState } from 'react';

import { BuildingInspector } from './building-inspector';
import {
  DISTRICTS,
  DISTRICT_NAMES,
  DISTRICT_DESCRIPTIONS,
  type DistrictKind,
} from './district-catalog';
import { generateDistrict, rowComparison } from './district-generator';
import { LayoutMap } from './layout-map';

import './district-preview.css';

export function DistrictPreview() {
  const [kind, setKind] = useState<DistrictKind>('CityCenter');
  const [guildHall, setGuildHall] = useState(true);
  const [seed, setSeed] = useState(42);
  const [blocks, setBlocks] = useState(2);
  const [alley, setAlley] = useState(2);
  const [scale, setScale] = useState(0.75);
  const [mode, setMode] = useState('blocks');
  const [selected, setSelected] = useState<number>();
  const district = useMemo(
    () => generateDistrict({ seed, blocks, alley, scale, kind, guildHall }),
    [seed, blocks, alley, scale, kind, guildHall],
  );
  const shown = useMemo(
    () => (mode === 'rows' ? rowComparison(district) : district),
    [district, mode],
  );
  const building = shown.buildings.find(({ id }) => id === selected);
  return (
    <main className="district-preview">
      <header>
        <div className="layout-eyebrow">TRPG · Layout workshop</div>
        <h1>{DISTRICT_NAMES[kind]}</h1>
        <p>{DISTRICT_DESCRIPTIONS[kind]}</p>
      </header>
      <section className="district-choice" aria-label="District selection">
        <label>
          District type
          <select
            value={kind}
            onChange={(event) => {
              setKind(event.target.value as DistrictKind);
              setSelected(undefined);
            }}
          >
            {DISTRICTS.map((type) => (
              <option key={type} value={type}>
                {DISTRICT_NAMES[type]}
              </option>
            ))}
          </select>
        </label>
        {kind === 'CityCenter' && (
          <label>
            Guild hall
            <select
              value={String(guildHall)}
              onChange={(event) => {
                setGuildHall(event.target.value === 'true');
                setSelected(undefined);
              }}
            >
              <option value="true">Faction present</option>
              <option value="false">No faction hall</option>
            </select>
          </label>
        )}
      </section>
      <PreviewControls
        {...{
          seed,
          setSeed,
          blocks,
          setBlocks,
          alley,
          setAlley,
          scale,
          setScale,
          mode,
          setMode,
          kind,
        }}
      />
      <div className="layout-status">
        <strong>{shown.buildings.length} buildings</strong>
        <span>
          {shown.width.toFixed(0)} × {shown.depth.toFixed(0)} m
        </span>
        <span>Seed {seed}</span>
      </div>
      <LayoutMap
        key={`${kind}:${guildHall}:${seed}:${blocks}:${alley}:${scale}:${mode}`}
        district={shown}
        seed={seed}
        selected={selected}
        onSelect={setSelected}
      />
      <div className="layout-legend">
        <span>● Entrances</span>
        <span>Green: gardens / yards</span>
        <span>Drag to pan · + to zoom</span>
      </div>
      {building ? (
        <BuildingInspector
          key={`${kind}:${seed}:${building.id}`}
          building={building}
          seed={seed}
          onClose={() => setSelected(undefined)}
        />
      ) : (
        <p className="layout-hint">
          {shown.buildings.length
            ? 'Tap a building to inspect its floors.'
            : 'No buildings in the current generation roster. The square and approach remain open.'}
        </p>
      )}
      <footer>
        Design prototype using the current building assignments and room names/floors. Dimensions
        and room arrangements are proposed; the game’s C# generator is unchanged. House bedrooms and
        guild membership use sample counts. “Rows” compares the same buildings.
      </footer>
    </main>
  );
}

type Controls = {
  kind: DistrictKind;
  seed: number;
  setSeed: (value: number) => void;
  blocks: number;
  setBlocks: (value: number) => void;
  alley: number;
  setAlley: (value: number) => void;
  scale: number;
  setScale: (value: number) => void;
  mode: string;
  setMode: (value: string) => void;
};

function PreviewControls({
  kind,
  seed,
  setSeed,
  blocks,
  setBlocks,
  alley,
  setAlley,
  scale,
  setScale,
  mode,
  setMode,
}: Controls) {
  return (
    <section className="layout-controls" aria-label="Layout controls">
      <label>
        Layout
        <select value={mode} onChange={(event) => setMode(event.target.value)}>
          <option value="blocks">District layout</option>
          <option value="rows">Current rows pattern</option>
        </select>
      </label>
      <label>
        Seed
        <input
          type="number"
          value={seed}
          min={0}
          max={999999}
          onChange={(event) => setSeed(Number(event.target.value))}
        />
      </label>
      <button onClick={() => setSeed(seed + 1)}>Next seed →</button>
      {kind === 'Residential' && (
        <label>
          Housing
          <select value={blocks} onChange={(event) => setBlocks(Number(event.target.value))}>
            <option value={2}>Small · 4 blocks</option>
            <option value={3}>Large · 9 blocks</option>
          </select>
        </label>
      )}
      <label>
        Alleys
        <select value={alley} onChange={(event) => setAlley(Number(event.target.value))}>
          <option value={1.5}>Narrow · 1.5 m</option>
          <option value={2}>Standard · 2 m</option>
          <option value={3}>Wide · 3 m</option>
        </select>
      </label>
      <label>
        Building size
        <select value={scale} onChange={(event) => setScale(Number(event.target.value))}>
          <option value={0.75}>Compact</option>
          <option value={1}>Generous</option>
          <option value={1.3}>Grand</option>
        </select>
      </label>
    </section>
  );
}
