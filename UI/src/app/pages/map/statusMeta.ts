export interface LocoStatusMeta {
  label: string;
  color: string;
  badgeBg: string;
  glow: string;
  icon: string;
}

export const LOCO_STATUS: Record<string, LocoStatusMeta> = {
  IDLE: {
    label: 'Idle',
    color: '#6b7280',
    badgeBg: 'rgba(107, 114, 128, 0.2)',
    glow: 'rgba(107, 114, 128, 0.4)',
    icon: '🛑'
  },
  LOADING: {
    label: 'Loading',
    color: '#f59e0b',
    badgeBg: 'rgba(245, 158, 11, 0.2)',
    glow: 'rgba(245, 158, 11, 0.8)',
    icon: '⏳'
  },
  TO_WEIGHBRIDGE: {
    label: 'To Weighbridge',
    color: '#3b82f6',
    badgeBg: 'rgba(59, 130, 246, 0.2)',
    glow: 'rgba(59, 130, 246, 0.8)',
    icon: '⚖️'
  },
  QUEUED_AT_WEIGHBRIDGE: {
    label: 'Queued',
    color: '#f97316',
    badgeBg: 'rgba(249, 115, 22, 0.2)',
    glow: 'rgba(249, 115, 22, 0.8)',
    icon: '⏳'
  },
  WEIGHING: {
    label: 'Weighing',
    color: '#a855f7',
    badgeBg: 'rgba(168, 85, 247, 0.2)',
    glow: 'rgba(168, 85, 247, 0.8)',
    icon: '⚖️'
  },
  TO_DESTINATION: {
    label: 'To Destination',
    color: '#06b6d4',
    badgeBg: 'rgba(6, 182, 212, 0.2)',
    glow: 'rgba(6, 182, 212, 0.8)',
    icon: '⚡'
  },
  UNLOADING: {
    label: 'Unloading',
    color: '#ec4899',
    badgeBg: 'rgba(236, 72, 153, 0.2)',
    glow: 'rgba(236, 72, 153, 0.8)',
    icon: '🌋'
  },
  RETURNING: {
    label: 'Returning Empty',
    color: '#22c55e',
    badgeBg: 'rgba(34, 197, 94, 0.2)',
    glow: 'rgba(34, 197, 94, 0.8)',
    icon: '🔄'
  },
  RUNNING: {
    label: 'In Transit',
    color: '#00ff88',
    badgeBg: 'rgba(0, 255, 136, 0.2)',
    glow: 'rgba(0, 255, 136, 0.8)',
    icon: '⚡'
  },
  HOT_METAL_TRANSIT: {
    label: 'Hot Metal Transit',
    color: '#ff5722',
    badgeBg: 'rgba(255, 87, 34, 0.25)',
    glow: 'rgba(255, 87, 34, 0.9)',
    icon: '🔥'
  }
};

export const LADLE_STATUS: Record<string, { label: string; color: string; isHot: boolean }> = {
  EMPTY_AT_SOURCE: { label: 'Empty @ Source', color: '#6b7280', isHot: false },
  LOADING: { label: 'Loading', color: '#f59e0b', isHot: true },
  FULL_IN_TRANSIT: { label: 'Full ➔ Weighbridge', color: '#3b82f6', isHot: true },
  FULL_TO_DEST: { label: 'Full ➔ Destination', color: '#06b6d4', isHot: true },
  UNLOADING: { label: 'Unloading', color: '#ec4899', isHot: true },
  EMPTY_RETURNING: { label: 'Returning Empty', color: '#22c55e', isHot: false },
  FULL: { label: 'Loaded with Hot Metal', color: '#ff5722', isHot: true },
  EMPTY: { label: 'Empty Ladle', color: '#6b7280', isHot: false }
};

export interface PlantNode {
  id: string;
  name: string;
  type: 'source' | 'weighbridge' | 'destination' | 'junction' | 'shop';
  kind: string;
  color: string;
  pos: [number, number];
  description?: string;
}

export const BOKARO_NODES: Record<string, PlantNode> = {
  BF1: { id: 'BF1', name: 'Blast Furnace 1', type: 'source', kind: 'Blast Furnace', color: '#ef4444', pos: [23.630511, 86.290917], description: 'Taphole A/B casting molten iron at ~1450°C' },
  BF2: { id: 'BF2', name: 'Blast Furnace 2', type: 'source', kind: 'Blast Furnace', color: '#ef4444', pos: [23.630522, 86.292500], description: 'Taphole C/D hot metal discharge' },
  BF3: { id: 'BF3', name: 'Blast Furnace 3', type: 'source', kind: 'Blast Furnace', color: '#ef4444', pos: [23.630600, 86.296111], description: 'Mega furnace outload point' },
  WB:  { id: 'WB',  name: 'Weighbridge (WB)', type: 'weighbridge', kind: 'Weighbridge', color: '#eab308', pos: [23.631639, 86.298406], description: 'Dynamic in-motion rail weighing system' },
  SMS: { id: 'SMS', name: 'Steel Melting Shop', type: 'destination', kind: 'Steel Melting Shop', color: '#3b82f6', pos: [23.635417, 86.301056], description: 'BOF Converters & Primary steelmaking' },
  DIP: { id: 'DIP', name: 'DIP Plant', type: 'destination', kind: 'DIP Plant', color: '#a855f7', pos: [23.634329, 86.299567], description: 'Desulphurisation injection facility' },
  PCM: { id: 'PCM', name: 'PCM Plant', type: 'destination', kind: 'Pig Casting Machine', color: '#06b6d4', pos: [23.634310, 86.299506], description: 'Pig casting machine solidifier' },
  LRS: { id: 'LRS', name: 'LRS Repair Shop', type: 'shop', kind: 'Ladle Repair Shop', color: '#38bdf8', pos: [23.634917, 86.300306], description: 'Refractory & preheating yard' }
};

export const NODE_META = BOKARO_NODES;

export interface LadleRecord {
  id: string;
  status: string;
  locationNode: string;
  tareKg: number;
  netKg: number;
  grossKg?: number;
  assignedLoco?: string | null;
  temperatureC?: number;
  heatNo?: string;
}

export interface LocoRecord {
  id: string;
  locoId: number;
  status: string;
  pos: [number, number];
  latitude?: number;
  longitude?: number;
  bearing: number;
  speedKmh: number;
  speed?: number;
  ladleIds: string[];
  destination?: string | null;
  currentNode?: string | null;
  tripIds?: number[];
  returningTo?: string;
  origin?: string;
  ladles?: LadleRecord[];
  ladle?: string;
}

export interface WeighbridgeState {
  busy: boolean;
  occupiedBy: string | null;
  queue: string[];
}

export interface TelemetryEvent {
  type: string;
  message: string;
  created_at: string;
  loco_id?: string;
  ladle_ids?: string[];
}

export interface PlantStateSnapshot {
  timestamp: string;
  nodes: Record<string, PlantNode>;
  railSegments: Record<string, [number, number][]>;
  weighbridge: WeighbridgeState;
  locos: LocoRecord[];
  ladles: LadleRecord[];
  events: TelemetryEvent[];
}
