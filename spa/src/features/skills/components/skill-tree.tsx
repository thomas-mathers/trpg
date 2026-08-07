import dagre from '@dagrejs/dagre';
import {
  Background,
  ReactFlow,
  ConnectionLineType,
  MarkerType,
  Handle,
  Position,
  type Node,
  type Edge,
  type NodeProps,
  useReactFlow,
} from '@xyflow/react';
import { Clock3, Droplet, Sparkles, Zap, Lock } from 'lucide-react';
import { useEffect, useMemo } from 'react';

import type { AbilitySummary } from '@/api/client';
import { Empty, EmptyDescription, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import { getAbilityIcon } from '@/features/skills/ability-visuals';

import '@xyflow/react/dist/style.css';
import { cn } from '@/lib/utils';

import {
  HoverPopover,
  HoverPopoverTrigger,
  HoverPopoverContent,
} from '../../../components/ui/hover-popover';

const NODE_WIDTH = 150;
const NODE_HEIGHT = 56;

interface SkillTreeProps {
  level: number;
  abilities: AbilitySummary[];
}

export function SkillTree({ abilities, level }: SkillTreeProps) {
  const { nodes, edges } = useMemo(() => createSkillTree(level, abilities), [abilities, level]);
  const { fitView } = useReactFlow();

  useEffect(() => {
    if (nodes.length === 0) return;

    requestAnimationFrame(() => {
      fitView({
        padding: 0.25,
        maxZoom: 1.5,
        minZoom: 0.5,
        duration: 0,
      });
    });
  }, [nodes, fitView]);

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

  return (
    <div className="min-h-0 flex-1">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        nodeTypes={nodeTypes}
        connectionLineType={ConnectionLineType.SmoothStep}
        colorMode="light"
        nodesFocusable={true}
        nodesConnectable={false}
        nodesDraggable={false}
        elementsSelectable={true}
      >
        <Background />
      </ReactFlow>
    </div>
  );
}

const createSkillTree = (
  level: number,
  abilities: AbilitySummary[],
  isHorizontal: boolean = true,
) => {
  const dagreGraph = new dagre.graphlib.Graph().setDefaultEdgeLabel(() => ({}));

  dagreGraph.setGraph({ rankdir: isHorizontal ? 'LR' : 'TB' });

  abilities.forEach((ability) => {
    dagreGraph.setNode(ability.name, { width: NODE_WIDTH, height: NODE_HEIGHT });
  });

  abilities.forEach((ability) => {
    ability.prerequisites.forEach((prerequisite) => {
      dagreGraph.setEdge(prerequisite, ability.name);
    });
  });

  dagre.layout(dagreGraph);

  const nodes: AbilityFlowNode[] = abilities.map((ability) => {
    const node = dagreGraph.node(ability.name);

    return {
      id: ability.name,
      targetPosition: isHorizontal ? Position.Left : Position.Top,
      sourcePosition: isHorizontal ? Position.Right : Position.Bottom,
      position: {
        x: node.x - NODE_WIDTH / 2,
        y: node.y - NODE_HEIGHT / 2,
      },
      type: ABILITY_NODE_TYPE,
      data: {
        ability,
        isUnlocked: level >= ability.requiredSkillLevel,
      },
    };
  });

  const abilityLevels = new Map(
    abilities.map((ability) => [ability.name, ability.requiredSkillLevel]),
  );

  const edges: AbilityFlowEdge[] = abilities.flatMap((ability) =>
    ability.prerequisites.map((prerequisite) => {
      const isUnlocked =
        level >= ability.requiredSkillLevel && level >= abilityLevels.get(prerequisite)!;
      return {
        id: `${prerequisite} -> ${ability.name}`,
        source: prerequisite,
        target: ability.name,
        type: 'smoothstep',
        animated: !isUnlocked,
        markerEnd: {
          type: MarkerType.ArrowClosed,
        },
      };
    }),
  );

  return { nodes: nodes, edges };
};

type AbilityNodeData = {
  ability: AbilitySummary;
  isUnlocked: boolean;
};

const ABILITY_NODE_TYPE = 'ability' as const;

type AbilityFlowNode = Node<AbilityNodeData, typeof ABILITY_NODE_TYPE>;
type AbilityFlowEdge = Edge;

function AbilityNode({ data, sourcePosition, targetPosition }: NodeProps<AbilityFlowNode>) {
  const { ability, isUnlocked } = data;
  const Icon = getAbilityIcon(ability);

  return (
    <HoverPopover>
      <HoverPopoverTrigger asChild>
        <div
          style={{
            width: NODE_WIDTH,
            height: NODE_HEIGHT,
          }}
          className={cn(
            'flex cursor-help items-center justify-center rounded border bg-white transition-colors',
            isUnlocked
              ? 'border-primary/50 hover:border-primary hover:shadow-sm'
              : 'border-muted bg-muted/50 opacity-60',
          )}
        >
          <Handle type="target" position={targetPosition ?? Position.Left} style={{ opacity: 0 }} />

          <div className="flex w-full flex-col items-center gap-1">
            <Icon className={cn('size-6', !isUnlocked && 'grayscale text-muted-foreground')} />

            <span className="w-full truncate text-center text-[10px] leading-tight">
              {ability.name}
            </span>
          </div>

          <Handle
            type="source"
            position={sourcePosition ?? Position.Right}
            style={{ opacity: 0 }}
          />
        </div>
      </HoverPopoverTrigger>

      <HoverPopoverContent side="top">
        <AbilityTooltip ability={ability} isUnlocked={isUnlocked} />
      </HoverPopoverContent>
    </HoverPopover>
  );
}

function AbilityTooltip({ ability, isUnlocked }: { ability: AbilitySummary; isUnlocked: boolean }) {
  return (
    <div className="space-y-2">
      <div className="font-semibold">{ability.name}</div>

      <p>{ability.description}</p>

      <div className="flex gap-3 text-xs opacity-80">
        <span className="flex items-center gap-1 text-amber-500">
          <Zap className="h-4 w-4" />
          {ability.apCost}
        </span>
        <span className="flex items-center gap-1 text-blue-500">
          <Droplet className="h-4 w-4" />
          {ability.mpCost}
        </span>
        <span className="flex items-center gap-1">
          <Clock3 className="h-4 w-4" />
          {ability.cooldown} turns
        </span>
      </div>

      <div className="space-y-1 text-xs">
        <div className="text-muted-foreground">Requires:</div>

        <div
          className={cn(
            'flex items-center gap-1',
            isUnlocked ? 'text-muted-foreground' : 'text-destructive',
          )}
        >
          <Lock className="h-3 w-3" />
          Level {ability.requiredSkillLevel}
        </div>
      </div>
    </div>
  );
}

const nodeTypes = {
  ability: AbilityNode,
};
