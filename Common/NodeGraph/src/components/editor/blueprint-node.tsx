"use client";

import type { CSSProperties } from "react";
import { Handle, Position, type NodeProps } from "@xyflow/react";

import type { NodeGraphNode, NodeGraphNodeData, NodePortDefinition } from "@/lib/nodegraph/types";
import { useTypeColors } from "@/components/editor/type-colors";

const FALLBACK_ACCENT = "#ff9d1c";
const FALLBACK_TEXT = "#f7fbff";
const DEFAULT_TYPE_COLOR = "#64748B";

function hexToRgba(color: string, alpha: number) {
  const normalized = color.trim();

  if (/^rgba?\(/i.test(normalized)) {
    return normalized.replace(/rgba?\(([^)]+)\)/i, (_match, channels: string) => {
      const values = channels
        .split(",")
        .slice(0, 3)
        .map((value) => value.trim())
        .join(", ");
      return `rgba(${values}, ${alpha})`;
    });
  }

  const hex = normalized.replace("#", "");
  const fullHex =
    hex.length === 3
      ? hex
          .split("")
          .map((char) => char + char)
          .join("")
      : hex;

  if (!/^[0-9a-f]{6}$/i.test(fullHex)) {
    return `rgba(255, 157, 28, ${alpha})`;
  }

  const value = Number.parseInt(fullHex, 16);
  const red = (value >> 16) & 255;
  const green = (value >> 8) & 255;
  const blue = value & 255;

  return `rgba(${red}, ${green}, ${blue}, ${alpha})`;
}

function formatValue(value: unknown) {
  if (typeof value === "boolean") {
    return value ? "Enabled" : "Disabled";
  }

  if (typeof value === "number") {
    return String(value);
  }

  if (typeof value === "string") {
    return value.trim() ? value : "Empty";
  }

  if (Array.isArray(value)) {
    return `${value.length} items`;
  }

  if (value && typeof value === "object") {
    return "Configured";
  }

  return "Unset";
}

function getNodeTheme(data: NodeGraphNodeData) {
  const accent = data.appearance?.borderColor ?? FALLBACK_ACCENT;
  const text = data.appearance?.textColor ?? FALLBACK_TEXT;

  return {
    accent,
    text,
    accentMuted: hexToRgba(accent, 0.12),
    accentSoft: hexToRgba(accent, 0.2),
    accentGlow: hexToRgba(accent, 0.44),
    textSoft: hexToRgba(text, 0.7),
  };
}

function getPortLabel(port: NodePortDefinition) {
  return port.label.trim() || port.id;
}

export function BlueprintNode({ data, selected }: NodeProps<NodeGraphNode>) {
  const typeColors = useTypeColors();
  const valueEntries = Object.entries(data.values ?? {}).slice(0, 3);
  const hiddenFieldCount = Math.max(Object.keys(data.values ?? {}).length - valueEntries.length, 0);
  const inputPorts = data.inputs ?? [];
  const outputPorts = data.outputs ?? [];
  const theme = getNodeTheme(data);
  const nodeStyle = {
    "--node-accent": theme.accent,
    "--node-accent-muted": theme.accentMuted,
    "--node-accent-soft": theme.accentSoft,
    "--node-accent-glow": theme.accentGlow,
    "--node-text": theme.text,
    "--node-text-soft": theme.textSoft,
  } as CSSProperties;

  return (
    <div
      className={`blueprint-node${selected ? " blueprint-node--selected" : ""}`}
      style={nodeStyle}
    >
      <div className="blueprint-node__header">
        <span className="blueprint-node__category">{data.category ?? "Node"}</span>
        <span className="blueprint-node__type">{data.nodeType}</span>
      </div>

      <div className="blueprint-node__body">
        <div className="blueprint-node__title-row">
          <h3 className="blueprint-node__title">{data.label}</h3>
          <span className="blueprint-node__status">Ready</span>
        </div>

        <p className="blueprint-node__description">
          {data.description?.trim() || "This node is ready for field-driven configuration."}
        </p>

        <div className="blueprint-node__ports">
          <div className="blueprint-node__port-column">
            <span className="blueprint-node__port-label">Inputs</span>
            <div className="blueprint-node__port-list">
              {inputPorts.length ? (
                inputPorts.map((port) => (
                  <div
                    className="blueprint-node__port-chip blueprint-node__port-chip--input"
                    key={port.id}
                    style={{
                      borderColor: port.dataType ? (typeColors.get(port.dataType) ?? DEFAULT_TYPE_COLOR) : undefined,
                      backgroundColor: port.dataType
                        ? hexToRgba(typeColors.get(port.dataType) ?? DEFAULT_TYPE_COLOR, 0.08)
                        : undefined,
                    }}
                  >
                    <Handle
                      className="blueprint-node__handle blueprint-node__handle--input"
                      id={port.id}
                      position={Position.Left}
                      type="target"
                      style={
                        port.dataType
                          ? {
                              backgroundColor: typeColors.get(port.dataType) ?? DEFAULT_TYPE_COLOR,
                              boxShadow: `0 0 0 4px ${hexToRgba(typeColors.get(port.dataType) ?? DEFAULT_TYPE_COLOR, 0.26)}`,
                            }
                          : undefined
                      }
                    />
                    <span className="blueprint-node__port-name">{getPortLabel(port)}</span>
                  </div>
                ))
              ) : (
                <div className="blueprint-node__port-empty">No inputs</div>
              )}
            </div>
          </div>

          <div className="blueprint-node__port-column blueprint-node__port-column--output">
            <span className="blueprint-node__port-label blueprint-node__port-label--out">Outputs</span>
            <div className="blueprint-node__port-list">
              {outputPorts.length ? (
                outputPorts.map((port) => (
                  <div
                    className="blueprint-node__port-chip blueprint-node__port-chip--output"
                    key={port.id}
                    style={{
                      borderColor: port.dataType ? (typeColors.get(port.dataType) ?? DEFAULT_TYPE_COLOR) : undefined,
                      backgroundColor: port.dataType
                        ? hexToRgba(typeColors.get(port.dataType) ?? DEFAULT_TYPE_COLOR, 0.08)
                        : undefined,
                    }}
                  >
                    <span className="blueprint-node__port-name">{getPortLabel(port)}</span>
                    <Handle
                      className="blueprint-node__handle blueprint-node__handle--output"
                      id={port.id}
                      position={Position.Right}
                      type="source"
                      style={
                        port.dataType
                          ? {
                              backgroundColor: typeColors.get(port.dataType) ?? DEFAULT_TYPE_COLOR,
                              boxShadow: `0 0 0 4px ${hexToRgba(typeColors.get(port.dataType) ?? DEFAULT_TYPE_COLOR, 0.26)}`,
                            }
                          : undefined
                      }
                    />
                  </div>
                ))
              ) : (
                <div className="blueprint-node__port-empty blueprint-node__port-empty--out">No outputs</div>
              )}
            </div>
          </div>
        </div>

        <div className="blueprint-node__fields">
          {valueEntries.length ? (
            valueEntries.map(([key, value]) => (
              <div className="blueprint-node__field" key={key}>
                <span className="blueprint-node__field-key">{key}</span>
                <span className="blueprint-node__field-value">{formatValue(value)}</span>
              </div>
            ))
          ) : (
            <div className="blueprint-node__field blueprint-node__field--empty">
              <span className="blueprint-node__field-key">Values</span>
              <span className="blueprint-node__field-value">No editable fields</span>
            </div>
          )}
        </div>

        {hiddenFieldCount ? (
          <div className="blueprint-node__footer">+{hiddenFieldCount} more field rows in inspector</div>
        ) : null}
      </div>
    </div>
  );
}
