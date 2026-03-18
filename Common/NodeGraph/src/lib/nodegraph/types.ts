import type { Edge, Node } from "@xyflow/react";

export type AccessType = "public" | "private";

export type NodeFieldKind = "text" | "textarea" | "number" | "boolean";

export interface NodeLibraryField {
  key: string;
  label: string;
  kind: NodeFieldKind;
  placeholder?: string;
  defaultValue?: string | number | boolean;
}

export interface NodeAppearance {
  bgColor?: string;
  borderColor?: string;
  textColor?: string;
}

export interface NodePortDefinition {
  id: string;
  label: string;
}

export interface NodeLibraryItem {
  type: string;
  label: string;
  description: string;
  category: string;
  inputs?: NodePortDefinition[];
  outputs?: NodePortDefinition[];
  fields?: NodeLibraryField[];
  defaultData?: Record<string, unknown>;
  appearance?: NodeAppearance;
}

export interface NodeGraphNodeData extends Record<string, unknown> {
  label: string;
  description?: string;
  category?: string;
  nodeType: string;
  inputs?: NodePortDefinition[];
  outputs?: NodePortDefinition[];
  values?: Record<string, unknown>;
  appearance?: NodeAppearance;
}

export type NodeGraphNode = Node<NodeGraphNodeData>;

export interface NodeGraphEdge extends Edge {
  sourceHandle?: string | null;
  targetHandle?: string | null;
}

export interface NodeGraphViewport {
  x: number;
  y: number;
  zoom: number;
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
  accessType: AccessType;
  domainCached: boolean;
}

export interface DomainRegistryEntry {
  domain: string;
  clientName?: string;
  nodeLibraryEndpoint: string;
  completionWebhook: string;
  nodeLibrary: NodeLibraryItem[];
  createdAt: string;
  updatedAt: string;
}

export type SessionStatus = "draft" | "completed";

export interface NodeGraphSession {
  sessionId: string;
  domain: string;
  clientName?: string;
  graph: NodeGraphDocument;
  metadata: Record<string, string>;
  accessType: AccessType;
  editorUrl: string;
  status: SessionStatus;
  nodeLibraryEndpoint: string;
  completionWebhook: string;
  createdAt: string;
  updatedAt: string;
  completedAt?: string;
}

export interface EditorSessionPayload {
  session: NodeGraphSession;
  nodeLibrary: NodeLibraryItem[];
}

export interface CompletionWebhookPayload {
  sessionId: string;
  domain: string;
  graph: NodeGraphDocument;
  metadata: Record<string, string>;
  completedAt: string;
  status: "completed";
}
