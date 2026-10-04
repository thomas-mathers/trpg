import type { Population } from './preview-population';
import { footprint } from './room-plans';

const TOGGLES = [
  { kind: 'creatures', label: 'Creatures' },
  { kind: 'props', label: 'Props' },
  { kind: 'labels', label: 'Labels' },
] as const;

export function PopulationControls({
  population,
  visible,
  onChange,
}: {
  population: Population;
  visible: string[];
  onChange: (value: string[]) => void;
}) {
  return (
    <div className="population-controls">
      {TOGGLES.map(({ kind, label }) => (
        <label key={kind}>
          <input
            type="checkbox"
            checked={visible.includes(kind)}
            onChange={(event) =>
              onChange(
                event.target.checked
                  ? [...visible, kind]
                  : visible.filter((value) => value !== kind),
              )
            }
          />
          {kind === 'labels' ? label : `${label} (${population[kind].length})`}
        </label>
      ))}
      <span>People: 0.6 m wide · beds: 1 × 2 m · benches: 1.5 × 0.5 m</span>
      <details className="prop-inventory">
        <summary>Furniture key</summary>
        {[...new Set(population.props.map((prop) => prop.name))].map((name) => {
          const matching = population.props.filter((prop) => prop.name === name);
          const size = footprint(matching[0], matching[0].rotation ?? 0);
          return (
            <span key={name}>
              {name} × {matching.length} · {size.width} × {size.depth} m
            </span>
          );
        })}
      </details>
    </div>
  );
}
