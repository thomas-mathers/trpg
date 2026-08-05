import { Sparkles } from 'lucide-react';

import type { AbilitySummary } from '@/api/client';
import { AbilityTooltip } from '@/components/abilities/AbilityTooltip';
import { Empty, EmptyDescription, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import {
  HoverPopover,
  HoverPopoverContent,
  HoverPopoverTrigger,
} from '@/components/ui/hover-popover';
import { getAbilityIcon } from '@/lib/ability-visuals';
import { layoutSkillTree } from '@/lib/skill-tree-layout';
import { cn } from '@/lib/utils';

const COLUMN_WIDTH = 168;
const ROW_HEIGHT = 88;
const NODE_SIZE = 56;
const PADDING = 32;

interface SkillTreeProps {
  abilities: AbilitySummary[];
  knownAbilityNames: Set<string>;
  currentLevel: number;
}

export function SkillTree({ abilities, knownAbilityNames, currentLevel }: SkillTreeProps) {
  if (abilities.length === 0) {
    return (
      <Empty>
        <EmptyMedia variant="icon">
          <Sparkles />
        </EmptyMedia>
        <EmptyTitle>No abilities yet</EmptyTitle>
        <EmptyDescription>This skill has no abilities defined.</EmptyDescription>
      </Empty>
    );
  }

  const { nodes, edges, columnCount } = layoutSkillTree(abilities);
  const nodeByName = new Map(nodes.map((node) => [node.ability.name, node]));

  const rowCountByColumn = new Map<number, number>();
  for (const node of nodes) {
    rowCountByColumn.set(
      node.column,
      Math.max(rowCountByColumn.get(node.column) ?? 0, node.row + 1),
    );
  }
  const maxRows = Math.max(1, ...rowCountByColumn.values());

  const width = PADDING * 2 + columnCount * COLUMN_WIDTH;
  const height = PADDING * 2 + maxRows * ROW_HEIGHT;

  const centerOf = (column: number, row: number) => ({
    x: PADDING + column * COLUMN_WIDTH + COLUMN_WIDTH / 2,
    y: PADDING + row * ROW_HEIGHT + ROW_HEIGHT / 2,
  });

  return (
    <div className="border-border bg-muted/30 overflow-auto rounded-lg border p-2">
      <div className="relative" style={{ width, height }}>
        <svg className="pointer-events-none absolute inset-0" width={width} height={height}>
          <defs>
            <marker
              id="skill-tree-arrow"
              viewBox="0 0 10 10"
              refX="9"
              refY="5"
              markerWidth="6"
              markerHeight="6"
              orient="auto-start-reverse"
            >
              <path d="M0 0 L10 5 L0 10 z" className="fill-border" />
            </marker>
          </defs>
          {edges.map((edge) => {
            const fromNode = nodeByName.get(edge.from);
            const toNode = nodeByName.get(edge.to);
            if (!fromNode || !toNode) return null;
            const from = centerOf(fromNode.column, fromNode.row);
            const to = centerOf(toNode.column, toNode.row);
            return (
              <line
                key={`${edge.from}->${edge.to}`}
                x1={from.x + NODE_SIZE / 2}
                y1={from.y}
                x2={to.x - NODE_SIZE / 2}
                y2={to.y}
                className="stroke-border"
                strokeWidth={2}
                markerEnd="url(#skill-tree-arrow)"
              />
            );
          })}
        </svg>

        {nodes.map((node) => {
          const isUnlocked = knownAbilityNames.has(node.ability.name);
          const Icon = getAbilityIcon(node.ability);
          const { x, y } = centerOf(node.column, node.row);
          return (
            <div
              key={node.ability.name}
              className="absolute -translate-x-1/2 -translate-y-1/2"
              style={{ left: x, top: y, width: NODE_SIZE + 16 }}
            >
              <HoverPopover>
                <HoverPopoverTrigger asChild>
                  <button
                    type="button"
                    aria-disabled={!isUnlocked}
                    className={cn(
                      'flex w-full flex-col items-center gap-1 rounded-lg border p-1.5 transition-colors',
                      isUnlocked
                        ? 'border-primary/40 bg-card hover:bg-accent'
                        : 'border-border/60 bg-muted/40 opacity-50 grayscale',
                    )}
                  >
                    <Icon className="size-6" />
                    <span className="w-full truncate text-center text-[10px] leading-tight">
                      {node.ability.name}
                    </span>
                  </button>
                </HoverPopoverTrigger>
                <HoverPopoverContent side="top">
                  <AbilityTooltip
                    ability={node.ability}
                    isUnlocked={isUnlocked}
                    currentLevel={currentLevel}
                  />
                </HoverPopoverContent>
              </HoverPopover>
            </div>
          );
        })}
      </div>
    </div>
  );
}
