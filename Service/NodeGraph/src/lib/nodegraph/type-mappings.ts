import type { NodeLibraryItem, NodePortDefinition, TypeMappingEntry } from "@/lib/nodegraph/types";

export const DEFAULT_TYPE_COLOR = "#64748B";

export interface TypeMappingIndex {
  canonicalToType: Map<string, string>;
  typeToCanonical: Map<string, string>;
  canonicalToColor: Map<string, string>;
}

function collectPorts(item: NodeLibraryItem): NodePortDefinition[] {
  return [...(item.inputs ?? []), ...(item.outputs ?? [])];
}

function isHexColor(value: string) {
  return /^#[0-9a-f]{6}$/i.test(value.trim());
}

export function buildTypeMappingIndex(typeMappings?: TypeMappingEntry[]): TypeMappingIndex {
  const canonicalToType = new Map<string, string>();
  const typeToCanonical = new Map<string, string>();
  const canonicalToColor = new Map<string, string>();

  for (const mapping of typeMappings ?? []) {
    if (mapping.color && !isHexColor(mapping.color)) {
      throw new Error(
        `typeMappings invalid color "${mapping.color}" for canonicalId "${mapping.canonicalId}". Expected "#RRGGBB".`,
      );
    }

    const existingType = canonicalToType.get(mapping.canonicalId);
    if (existingType && existingType !== mapping.type) {
      throw new Error(
        `typeMappings conflict: canonicalId "${mapping.canonicalId}" cannot map to both "${existingType}" and "${mapping.type}".`,
      );
    }

    const existingCanonicalId = typeToCanonical.get(mapping.type);
    if (existingCanonicalId && existingCanonicalId !== mapping.canonicalId) {
      throw new Error(
        `typeMappings conflict: type "${mapping.type}" cannot map to both "${existingCanonicalId}" and "${mapping.canonicalId}".`,
      );
    }

    const existingColor = canonicalToColor.get(mapping.canonicalId);
    const nextColor = (mapping.color ?? DEFAULT_TYPE_COLOR).trim();
    if (existingColor && existingColor !== nextColor) {
      throw new Error(
        `typeMappings conflict: canonicalId "${mapping.canonicalId}" cannot use both "${existingColor}" and "${nextColor}" as color.`,
      );
    }

    canonicalToType.set(mapping.canonicalId, mapping.type);
    typeToCanonical.set(mapping.type, mapping.canonicalId);
    canonicalToColor.set(mapping.canonicalId, nextColor);
  }

  return {
    canonicalToType,
    typeToCanonical,
    canonicalToColor,
  };
}

export function normalizeTypeMappings(typeMappings?: TypeMappingEntry[]) {
  if (!typeMappings) {
    return undefined;
  }

  // Validate and compute the canonical color decision (defaults to grey).
  const index = buildTypeMappingIndex(typeMappings);

  return typeMappings.map((mapping) => ({
    ...mapping,
    color: index.canonicalToColor.get(mapping.canonicalId) ?? DEFAULT_TYPE_COLOR,
  }));
}

export function validateNodeLibraryTypeMappings(nodes: NodeLibraryItem[], typeMappings?: TypeMappingEntry[]) {
  const index = buildTypeMappingIndex(typeMappings);

  if (typeMappings === undefined) {
    return index;
  }

  const canonicalIds = new Set(index.canonicalToType.keys());

  for (const item of nodes) {
    for (const port of collectPorts(item)) {
      if (!port.dataType || canonicalIds.has(port.dataType)) {
        continue;
      }

      throw new Error(
        `typeMappings missing canonicalId "${port.dataType}" referenced by node "${item.type}" port "${port.id}".`,
      );
    }
  }

  return index;
}
