import type { Edge, Node } from "@xyflow/react";

/**
 * 编辑器入口既可能来自公网，也可能来自宿主所在内网。
 */
export type AccessType = "public" | "private";

/**
 * 当前编辑器 UI 仍然使用开放的语言代码，方便沿用内建多语言能力。
 */
export type LocaleCode = string;

/**
 * 内建编辑器文案仍然使用扁平翻译目录。
 */
export type TranslationCatalog = Record<string, string>;

/**
 * 仅供 NodeGraph 自身 UI 文案运行时使用。
 */
export interface I18nBundle {
  defaultLocale?: LocaleCode;
  locales: Record<LocaleCode, TranslationCatalog>;
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

/**
 * Select 字段代理接口直接返回当前语言下可展示的文本。
 */
export interface NodeFieldOption {
  value: string;
  label: string;
}

interface NodeLibraryFieldBase {
  key: string;
  label: string;
  placeholder?: string;
}

type NodeLibraryStringFieldKind = "text" | "textarea" | "date" | "decimal";
type NodeLibraryNumericFieldKind = "int" | "float" | "double";

interface NodeLibraryStringField extends NodeLibraryFieldBase {
  kind: NodeLibraryStringFieldKind;
  defaultValue?: string;
}

interface NodeLibraryColorField extends NodeLibraryFieldBase {
  kind: "color";
  defaultValue?: string;
}

interface NodeLibraryBooleanField extends NodeLibraryFieldBase {
  kind: "boolean";
  defaultValue?: boolean;
}

interface NodeLibraryNumericField extends NodeLibraryFieldBase {
  kind: NodeLibraryNumericFieldKind;
  defaultValue?: number;
}

interface NodeLibrarySelectField extends NodeLibraryFieldBase {
  kind: "select";
  optionsEndpoint: string;
  defaultValue?: string;
}

/**
 * 节点库字段全部使用宿主提供的原始字符串。
 */
export type NodeLibraryField =
  | NodeLibraryStringField
  | NodeLibraryColorField
  | NodeLibraryBooleanField
  | NodeLibraryNumericField
  | NodeLibrarySelectField;

/**
 * 节点卡片在编辑器中的外观配置。
 */
export interface NodeAppearance {
  bgColor?: string;
  borderColor?: string;
  textColor?: string;
}

/**
 * 端口定义使用可直接展示的原始字符串标签。
 */
export interface NodePortDefinition {
  id: string;
  label: string;
  dataType?: string;
}

export interface TypeMappingEntry {
  canonicalId: string;
  type: string;
  color?: string;
}

/**
 * 节点模板描述宿主可提供给编辑器的新建节点。
 */
export interface NodeLibraryItem {
  type: string;
  displayName: string;
  description?: string;
  category: string;
  inputs?: NodePortDefinition[];
  outputs?: NodePortDefinition[];
  fields?: NodeLibraryField[];
  defaultData?: Record<string, unknown>;
  appearance?: NodeAppearance;
}

export interface InvalidTemplateMarker {
  code: string;
  reason: string;
}

/**
 * 画布中的节点始终保存一份可直接展示的文本快照。
 * `templateMarkers` 用于记录刷新后发现的失效项，供 UI 标红提示。
 */
export interface NodeGraphNodeData extends Record<string, unknown> {
  label: string;
  labelOverride?: string;
  description?: string;
  descriptionOverride?: string;
  category?: string;
  nodeType: string;
  inputs?: NodePortDefinition[];
  outputs?: NodePortDefinition[];
  values?: Record<string, unknown>;
  appearance?: NodeAppearance;
  templateMarkers?: InvalidTemplateMarker[];
}

export type NodeGraphNode = Node<NodeGraphNodeData>;

export interface NodeGraphEdge extends Edge {
  sourceHandle?: string | null;
  targetHandle?: string | null;
  invalidReason?: string;
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

export interface RuntimeCapabilities {
  canExecute: boolean;
  canDebug: boolean;
  canProfile: boolean;
}

export interface NodeLibraryEnvelope {
  nodes: NodeLibraryItem[];
  typeMappings?: TypeMappingEntry[];
}

export interface RuntimeRegistrationRequest {
  runtimeId: string;
  domain: string;
  clientName?: string;
  controlBaseUrl: string;
  libraryVersion: string;
  capabilities?: Partial<RuntimeCapabilities>;
  library: NodeLibraryEnvelope;
}

export interface RuntimeRegistrationResponse {
  runtimeId: string;
  cached: boolean;
  expiresAt: string;
  libraryVersion: string;
}

export interface RuntimeRegistryEntry {
  runtimeId: string;
  domain: string;
  clientName?: string;
  controlBaseUrl: string;
  libraryVersion: string;
  capabilities: RuntimeCapabilities;
  nodeLibrary: NodeLibraryItem[];
  typeMappings?: TypeMappingEntry[];
  createdAt: string;
  updatedAt: string;
  expiresAt: string;
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
  accessType: AccessType;
}

export type SessionStatus = "draft" | "completed";

export interface NodeGraphSession {
  sessionId: string;
  runtimeId: string;
  domain: string;
  clientName?: string;
  graph: NodeGraphDocument;
  metadata: Record<string, string>;
  accessType: AccessType;
  editorUrl: string;
  status: SessionStatus;
  completionWebhook: string;
  createdAt: string;
  updatedAt: string;
  completedAt?: string;
}

export interface EditorSessionRuntimePayload {
  runtimeId: string;
  domain: string;
  clientName?: string;
  libraryVersion: string;
  capabilities: RuntimeCapabilities;
  expiresAt: string;
}

export interface EditorSessionPayload {
  session: NodeGraphSession;
  runtime: EditorSessionRuntimePayload;
  nodeLibrary: NodeLibraryItem[];
  typeMappings?: TypeMappingEntry[];
}

/**
 * Select 字段的代理接口返回当前语言下可直接渲染的选项列表。
 */
export interface NodeFieldOptionsResponse {
  options: NodeFieldOption[];
}

export interface CompletionWebhookPayload {
  sessionId: string;
  runtimeId: string;
  domain: string;
  graph: NodeGraphDocument;
  metadata: Record<string, string>;
  completedAt: string;
  status: "completed";
}

export interface RuntimeLibraryRefreshResult {
  runtime: EditorSessionRuntimePayload;
  nodeLibrary: NodeLibraryItem[];
  typeMappings?: TypeMappingEntry[];
  migratedGraph?: NodeGraphDocument;
}
