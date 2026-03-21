import type { Edge, Node } from "@xyflow/react";

/**
 * The editor can be opened from public or private network entrypoints.
 */
export type AccessType = "public" | "private";

/**
 * Locale codes now stay open-ended so the built-in `src/i18n/<locale>.json`
 * files can define the supported language set at runtime.
 */
export type LocaleCode = string;

/**
 * Translation catalogs store flattened dot-notation keys.
 */
export type TranslationCatalog = Record<string, string>;

/**
 * Domain-specific i18n payloads travel with the node library contract.
 */
export interface I18nBundle {
  defaultLocale?: LocaleCode;
  locales: Record<LocaleCode, TranslationCatalog>;
}

/**
 * Older saved graphs may still carry inline locale maps for ports or category
 * metadata. We keep that compatibility path during the refactor.
 */
export type LegacyLocalizedText = Record<string, string>;

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
 * 字段远端选项在编辑器中统一使用已本地化的 label。
 */
export interface NodeFieldOption {
  value: string;
  label: string;
}

interface NodeLibraryFieldBase {
  key: string;
  labelKey: string;
  placeholderKey?: string;
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
 * Node field metadata now points at translation keys instead of inline values.
 */
export type NodeLibraryField =
  | NodeLibraryStringField
  | NodeLibraryColorField
  | NodeLibraryBooleanField
  | NodeLibraryNumericField
  | NodeLibrarySelectField;

/**
 * Visual styling for a node card inside the editor.
 */
export interface NodeAppearance {
  bgColor?: string;
  borderColor?: string;
  textColor?: string;
}

/**
 * Ports primarily use translation keys. Legacy saved graphs may still carry an
 * inline label snapshot or localized object, so the runtime accepts both.
 */
export interface NodePortDefinition {
  id: string;
  labelKey?: string;
  label?: string | LegacyLocalizedText;
  /** Canonical type identifier shared across languages, e.g. workflow/request. */
  dataType?: string;
}

export interface TypeMappingEntry {
  canonicalId: string;
  type: string;
  color?: string;
}

/**
 * Node-library templates describe reusable business nodes for a domain.
 */
export interface NodeLibraryItem {
  type: string;
  labelKey: string;
  descriptionKey?: string;
  categoryKey: string;
  inputs?: NodePortDefinition[];
  outputs?: NodePortDefinition[];
  fields?: NodeLibraryField[];
  defaultData?: Record<string, unknown>;
  appearance?: NodeAppearance;
}

/**
 * Stored node data keeps compatibility snapshots in `label` and
 * `description`, while key-based fields drive locale-aware rendering.
 */
export interface NodeGraphNodeData extends Record<string, unknown> {
  label: string;
  labelKey?: string;
  labelOverride?: string;
  description?: string;
  descriptionKey?: string;
  descriptionOverride?: string;
  categoryKey?: string;
  category?: string | LegacyLocalizedText;
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
  i18n: I18nBundle;
  typeMappings?: TypeMappingEntry[];
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
  i18n: I18nBundle;
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
  domain: string;
  graph: NodeGraphDocument;
  metadata: Record<string, string>;
  completedAt: string;
  status: "completed";
}
