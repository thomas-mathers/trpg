import type { Population, ScaleProp } from './preview-population';

export function PopulationLayer({
  population,
  visible,
  labelSize = 0.32,
}: {
  population: Population;
  visible: string[];
  labelSize?: number;
}) {
  const { props, creatures } = population;
  return (
    <g pointerEvents="none">
      {visible.includes('props') &&
        props.map((prop, index) => <PropGlyph key={index} prop={prop} />)}
      {visible.includes('props') &&
        visible.includes('labels') &&
        props.map((prop, index) => <PropLabel key={index} prop={prop} size={labelSize} />)}
      {visible.includes('creatures') &&
        creatures.map(({ x, y, angle }, index) => (
          <g
            key={index}
            role="img"
            aria-label="Creature, 0.6 m shoulder width"
            transform={`translate(${x} ${y}) rotate(${angle})`}
          >
            <ellipse rx={0.3} ry={0.2} fill="#193c57" stroke="#fff8df" strokeWidth={0.04} />
            <circle cy={-0.08} r={0.12} fill="#e5be92" />
            <path d="M 0 -0.21 v -0.09" stroke="#193c57" strokeWidth={0.07} />
          </g>
        ))}
    </g>
  );
}

const ROUND_SHAPES: ScaleProp['shape'][] = ['barrel', 'fountain', 'fire'];
const FILL: Partial<Record<ScaleProp['shape'], string>> = {
  bed: '#a65851',
  hearth: '#756e65',
  altar: '#b9abc8',
  rug: '#8a4f52',
  fountain: '#9a9f9e',
  pillar: '#8d8c86',
  fire: '#6b625a',
};

function glyphTransform({ x, y, width, depth, rotation = 0 }: ScaleProp) {
  if (rotation === 90) return `translate(${x + width} ${y}) rotate(90)`;
  if (rotation === 180) return `translate(${x + width} ${y + depth}) rotate(180)`;
  if (rotation === 270) return `translate(${x} ${y + depth}) rotate(270)`;
  return `translate(${x} ${y})`;
}

function PropLabel({ prop, size }: { prop: ScaleProp; size: number }) {
  const { x, y, width, depth, name, shape } = prop;
  if (shape === 'rug') return null;
  const cx = x + width / 2;
  const cy = y + depth / 2;
  return (
    <text
      x={cx}
      y={cy}
      transform={depth > width * 1.3 ? `rotate(-90 ${cx} ${cy})` : undefined}
      textAnchor="middle"
      dominantBaseline="central"
      fontSize={size}
      fill="#fff8e8"
      stroke="#2a1f14"
      strokeWidth={size * 0.28}
      paintOrder="stroke"
    >
      {name}
    </text>
  );
}

function PropGlyph({ prop }: { prop: ScaleProp }) {
  const { name, shape, rotation = 0 } = prop;
  const sideways = rotation === 90 || rotation === 270;
  const width = sideways ? prop.depth : prop.width;
  const depth = sideways ? prop.width : prop.depth;
  return (
    <g role="img" aria-label={`${name}, ${width} × ${depth} m`} transform={glyphTransform(prop)}>
      <title>
        {name} · {width} × {depth} m
      </title>
      <rect
        width={width}
        height={depth}
        rx={ROUND_SHAPES.includes(shape) ? width / 2 : 0.04}
        fill={FILL[shape] ?? '#86603b'}
        stroke="#3c3025"
        strokeWidth={0.06}
      />
      {shape === 'bed' && (
        <rect x={0.12} y={0.12} width={width - 0.24} height={0.4} fill="#ece2ca" />
      )}
      {shape === 'seat' && <path d={`M 0 0.12 h ${width}`} stroke="#d0b387" strokeWidth={0.08} />}
      {shape === 'box' && (
        <path
          d={`M 0 0 L ${width} ${depth} M ${width} 0 L 0 ${depth}`}
          stroke="#b79668"
          strokeWidth={0.06}
        />
      )}
      <FurnitureDetail prop={{ ...prop, width, depth }} />
    </g>
  );
}

function FurnitureDetail({ prop: { shape, width, depth } }: { prop: ScaleProp }) {
  if (shape === 'shelf')
    return (
      <g>
        {[0.2, 0.4, 0.6, 0.8].map((x) => (
          <path
            key={x}
            d={`M ${width * x} 0.1 v ${depth - 0.2}`}
            stroke="#e2c698"
            strokeWidth={0.1}
          />
        ))}
      </g>
    );
  if (shape === 'table')
    return (
      <rect x={0.12} y={0.12} width={width - 0.24} height={depth - 0.24} fill="#bd9863" rx={0.08} />
    );
  if (shape === 'hearth')
    return (
      <g>
        <rect x={0.15} y={0.15} width={width - 0.3} height={depth - 0.3} fill="#393530" />
        <path
          d={`M ${width * 0.3} ${depth * 0.7} L ${width * 0.45} ${depth * 0.25} L ${width * 0.7} ${depth * 0.7} Z`}
          fill="#e19c51"
        />
      </g>
    );
  if (shape === 'altar')
    return (
      <g>
        <rect x={0.2} y={0.1} width={width - 0.4} height={depth - 0.2} fill="#e6dfc7" />
        <circle cx={width / 2} cy={depth / 2} r={0.15} fill="#806ea6" />
      </g>
    );
  if (shape === 'alchemy')
    return (
      <g>
        {[0.25, 0.5, 0.75].map((x) => (
          <circle
            key={x}
            cx={width * x}
            cy={depth / 2}
            r={0.13}
            fill="#8dc1a2"
            stroke="#33594c"
            strokeWidth={0.04}
          />
        ))}
      </g>
    );
  if (shape === 'anvil')
    return (
      <path
        d={`M 0.1 ${depth * 0.3} H ${width - 0.1} L ${width * 0.65} ${depth * 0.5} V ${depth * 0.8} H ${width * 0.35} V ${depth * 0.5} Z`}
        fill="#b3b8b8"
      />
    );
  if (shape === 'rug')
    return (
      <rect
        x={0.12}
        y={0.12}
        width={width - 0.24}
        height={depth - 0.24}
        fill="none"
        stroke="#d9b87a"
        strokeWidth={0.05}
      />
    );
  if (shape === 'fountain')
    return <circle cx={width / 2} cy={depth / 2} r={width * 0.35} fill="#74b0cf" />;
  if (shape === 'pillar')
    return (
      <rect x={0.15} y={0.15} width={width - 0.3} height={depth - 0.3} fill="#c9c8c0" rx={0.05} />
    );
  if (shape === 'fire')
    return <circle cx={width / 2} cy={depth / 2} r={width * 0.25} fill="#e19c51" />;
  if (shape === 'cell')
    return (
      <g>
        {[0.25, 0.5, 0.75].map((x) => (
          <path key={x} d={`M ${width * x} 0 V ${depth}`} stroke="#bac0c0" strokeWidth={0.08} />
        ))}
      </g>
    );
  return null;
}
