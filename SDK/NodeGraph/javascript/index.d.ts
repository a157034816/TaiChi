export interface NodeGraphViewport {
  x: number;
  y: number;
  zoom: number;
}

export interface NodeGraphNode {
  id: string;
  type: string;
  position: {
    x: number;
    y: number;
  };
  data: Record<string, unknown>;
  width?: number;
  height?: number;
  style?: Record<string, unknown>;
}

export interface NodeGraphEdge {
  id: string;
  source: string;
  target: string;
  sourceHandle?: string | null;
  targetHandle?: string | null;
  label?: string;
  type?: string;
  animated?: boolean;
  invalidReason?: string;
}

export interface NodeGraphDocument {
  graphId?: string;
  name: string;
  description?: string;
  nodes: NodeGraphNode[];
  edges: NodeGraphEdge[];
  viewport: NodeGraphViewport;
}

export interface RuntimeCapabilities {
  canExecute?: boolean;
  canDebug?: boolean;
  canProfile?: boolean;
}

export interface NodePortDefinition {
  id: string;
  label: string;
  dataType?: string;
}

export type NodeFieldKind =
  | "text"
  | "textarea"
  | "boolean"
  | "select"
  | "date"
  | "color"
  | "int"
  | "float"
  | "double"
  | "decimal";

export interface NodeLibraryFieldBase {
  key: string;
  label: string;
  placeholder?: string;
}

export interface NodeLibraryStringField extends NodeLibraryFieldBase {
  kind: "text" | "textarea" | "date" | "decimal";
  defaultValue?: string;
}

export interface NodeLibraryBooleanField extends NodeLibraryFieldBase {
  kind: "boolean";
  defaultValue?: boolean;
}

export interface NodeLibraryColorField extends NodeLibraryFieldBase {
  kind: "color";
  defaultValue?: string;
}

export interface NodeLibraryNumericField extends NodeLibraryFieldBase {
  kind: "int" | "float" | "double";
  defaultValue?: number;
}

export interface NodeLibrarySelectField extends NodeLibraryFieldBase {
  kind: "select";
  optionsEndpoint: string;
  defaultValue?: string;
}

export type NodeLibraryField =
  | NodeLibraryStringField
  | NodeLibraryBooleanField
  | NodeLibraryColorField
  | NodeLibraryNumericField
  | NodeLibrarySelectField;

export interface NodeLibraryItem {
  type: string;
  displayName: string;
  description?: string;
  category: string;
  inputs?: NodePortDefinition[];
  outputs?: NodePortDefinition[];
  fields?: NodeLibraryField[];
  defaultData?: Record<string, unknown>;
  appearance?: {
    bgColor?: string;
    borderColor?: string;
    textColor?: string;
  };
}

export interface TypeMappingEntry {
  canonicalId: string;
  type: string;
  color?: string;
}

export interface RuntimeRegistrationRequest {
  runtimeId: string;
  domain: string;
  clientName?: string;
  controlBaseUrl: string;
  libraryVersion: string;
  capabilities?: RuntimeCapabilities;
  library: {
    nodes: NodeLibraryItem[];
    typeMappings?: TypeMappingEntry[];
  };
}

export interface RuntimeRegistrationResponse {
  runtimeId: string;
  cached: boolean;
  expiresAt: string;
  libraryVersion: string;
}

export interface CreateSessionRequest {
  runtimeId: string;
  completionWebhook: string;
  graph?: NodeGraphDocument;
  metadata?: Record<string, string>;
}

export interface CreateSessionResponse {
  sessionId: string;
  runtimeId: string;
  editorUrl: string;
  accessType: "public" | "private";
}

export interface NodeGraphClientOptions {
  baseUrl: string;
  fetch?: typeof fetch;
}

export interface NodeExecutionTrigger {
  reason: "initial" | "message";
  portId?: string;
  value?: unknown;
}

export interface NodeExecutionContext {
  graph: NodeGraphDocument;
  node: NodeGraphNode;
  state: Record<string, unknown>;
  trigger: NodeExecutionTrigger;
  values: Record<string, unknown>;
  getInputs(): Record<string, unknown[]>;
  readInput(portId: string): unknown;
  emit(portId: string, value: unknown): void;
  pushResult(channel: string, value: unknown): void;
}

export interface NodeDefinition extends NodeLibraryItem {
  execute(context: NodeExecutionContext): Promise<void> | void;
}

export interface DebuggerSnapshot {
  status: "idle" | "running" | "paused" | "completed" | "budget_exceeded" | "failed";
  pauseReason: string | null;
  pendingNodeId: string | null;
  lastError?: Error | null;
  lastEvent?: {
    step: number;
    kind: string;
    nodeId: string;
    nodeType?: string;
    durationMs?: number;
    reason?: string;
    portId?: string | null;
  } | null;
  profiler: Record<string, {
    averageDurationMs: number;
    callCount: number;
    lastDurationMs: number;
    totalDurationMs: number;
  }>;
  results: Record<string, unknown[]>;
  events: Array<Record<string, unknown>>;
}

export interface RuntimeExecuteOptions {
  breakpoints?: string[];
  maxSteps?: number;
  maxWallTimeMs?: number;
}

export interface NodeGraphRuntimeOptions {
  domain: string;
  clientName?: string;
  controlBaseUrl: string;
  libraryVersion: string;
  capabilities?: RuntimeCapabilities;
  runtimeId?: string;
  cacheTtlMs?: number;
  now?: () => number;
}

export declare class NodeGraphError extends Error {
  constructor(message: string, status: number, payload: unknown);
  status: number;
  payload: unknown;
}

export declare class NodeGraphClient {
  constructor(options: NodeGraphClientOptions);
  registerRuntime(request: RuntimeRegistrationRequest): Promise<RuntimeRegistrationResponse>;
  createSession(request: CreateSessionRequest): Promise<CreateSessionResponse>;
  getSession(sessionId: string): Promise<unknown>;
}

export declare class NodeGraphRuntime {
  constructor(options: NodeGraphRuntimeOptions);
  readonly runtimeId: string;
  readonly domain: string;
  readonly clientName?: string;
  readonly controlBaseUrl: string;
  readonly libraryVersion: string;
  readonly capabilities: Required<RuntimeCapabilities>;
  registerNode(definition: NodeDefinition): this;
  registerTypeMapping(mapping: TypeMappingEntry): this;
  getLibrary(): {
    nodes: NodeLibraryItem[];
    typeMappings?: TypeMappingEntry[];
  };
  createRegistrationRequest(): RuntimeRegistrationRequest;
  ensureRegistered(
    client: Pick<NodeGraphClient, "registerRuntime">,
    options?: {
      force?: boolean;
    },
  ): Promise<RuntimeRegistrationResponse>;
  createDebugger(graph: NodeGraphDocument, options?: RuntimeExecuteOptions): {
    step(): Promise<DebuggerSnapshot>;
    continue(): Promise<DebuggerSnapshot>;
  };
  executeGraph(graph: NodeGraphDocument, options?: RuntimeExecuteOptions): Promise<DebuggerSnapshot>;
}
