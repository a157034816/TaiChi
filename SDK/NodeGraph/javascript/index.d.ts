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
}

export interface NodeGraphDocument {
  graphId?: string;
  name: string;
  description?: string;
  nodes: NodeGraphNode[];
  edges: NodeGraphEdge[];
  viewport: NodeGraphViewport;
}

export interface CreateSessionRequest {
  domain: string;
  clientName?: string;
  nodeLibraryEndpoint: string;
  completionWebhook: string;
  graph?: NodeGraphDocument;
  metadata?: Record<string, string>;
}

export interface CreateSessionResponse {
  sessionId: string;
  editorUrl: string;
  accessType: "public" | "private";
  domainCached: boolean;
}

export interface NodeGraphClientOptions {
  baseUrl: string;
  fetch?: typeof fetch;
}

export declare class NodeGraphError extends Error {
  constructor(message: string, status: number, payload: unknown);
  status: number;
  payload: unknown;
}

export declare class NodeGraphClient {
  constructor(options: NodeGraphClientOptions);
  createSession(request: CreateSessionRequest): Promise<CreateSessionResponse>;
  getSession(sessionId: string): Promise<unknown>;
}
