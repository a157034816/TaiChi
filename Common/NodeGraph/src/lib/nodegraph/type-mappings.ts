import type { NodeLibraryItem, NodePortDefinition, TypeMappingEntry } from "@/lib/nodegraph/types";

export interface TypeMappingIndex {
  canonicalToType: Map<string, string>;
  typeToCanonical: Map<string, string>;
}

function collectPorts(item: NodeLibraryItem): NodePortDefinition[] {
  return [...(item.inputs ?? []), ...(item.outputs ?? [])];
}

export function buildTypeMappingIndex(typeMappings?: TypeMappingEntry[]): TypeMappingIndex {
  const canonicalToType = new Map<string, string>();
  const typeToCanonical = new Map<string, string>();

  for (const mapping of typeMappings ?? []) {
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

    canonicalToType.set(mapping.canonicalId, mapping.type);
    typeToCanonical.set(mapping.type, mapping.canonicalId);
  }

  return {
    canonicalToType,
    typeToCanonical,
  };
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
