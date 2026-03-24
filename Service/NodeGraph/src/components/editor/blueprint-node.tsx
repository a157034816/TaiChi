"use client";

import type { CSSProperties } from "react";
import { Handle, Position, type NodeProps } from "@xyflow/react";

import { useDebuggerNodeContext } from "@/components/editor/debugger-node-context";
import { useEditorI18n } from "@/components/editor/editor-i18n-context";
import { useTypeColors } from "@/components/editor/type-colors";
import {
  resolveNodeCategory,
  resolveNodeDescription,
  resolveNodeLabel,
  resolvePortLabel,
} from "@/lib/nodegraph/localization";
import type { NodeGraphNode, NodeGraphNodeData, NodePortDefinition } from "@/lib/nodegraph/types";

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

function formatValue(
  value: unknown,
  text: ReturnType<typeof useEditorI18n>["text"],
) {
  if (typeof value === "boolean") {
    return value ? text("editor.blueprint.enabled") : text("editor.blueprint.disabled");
  }

  if (typeof value === "number") {
    return String(value);
  }

  if (typeof value === "string") {
    return value.trim() ? value : text("editor.blueprint.empty");
  }

  if (Array.isArray(value)) {
    return text("editor.blueprint.items", { count: value.length });
  }

  if (value && typeof value === "object") {
    return text("editor.blueprint.configured");
  }

  return text("editor.blueprint.unset");
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

function getPortDisplayLabel(port: NodePortDefinition, i18n: ReturnType<typeof useEditorI18n>) {
  return resolvePortLabel(port, i18n).trim() || port.id;
}

/**
 * Renders the custom blueprint node body used inside the React Flow canvas.
 */
export function BlueprintNode({ id, data, selected }: NodeProps<NodeGraphNode>) {
  const i18n = useEditorI18n();
  const debuggerContext = useDebuggerNodeContext();
  const typeColors = useTypeColors();
  const valueEntries = Object.entries(data.values ?? {}).slice(0, 3);
  const hiddenFieldCount = Math.max(Object.keys(data.values ?? {}).length - valueEntries.length, 0);
  const inputPorts = data.inputs ?? [];
  const outputPorts = data.outputs ?? [];
  const theme = getNodeTheme(data);
  const hasBreakpoint = debuggerContext?.breakpoints.has(id) ?? false;
  const isPending = debuggerContext?.pendingNodeId === id;
  const isFailed = debuggerContext?.failedNodeId === id;
  const isLastExecuted = debuggerContext?.lastEventNodeId === id;
  const statusLabel = isFailed
    ? i18n.text("editor.debugger.nodeFailed")
    : isPending
      ? i18n.text("editor.debugger.nodePending")
      : isLastExecuted
        ? i18n.text("editor.debugger.nodeExecuted")
        : hasBreakpoint
          ? i18n.text("editor.debugger.breakpointSet")
          : i18n.text("editor.blueprint.ready");
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
      className={`blueprint-node${selected ? " blueprint-node--selected" : ""}${hasBreakpoint ? " blueprint-node--debug-breakpoint" : ""}${isPending ? " blueprint-node--debug-pending" : ""}${isFailed ? " blueprint-node--debug-failed" : ""}${isLastExecuted ? " blueprint-node--debug-last" : ""}`}
      style={nodeStyle}
    >
      <div className="blueprint-node__header">
        <span className="blueprint-node__category">
          {resolveNodeCategory(data, i18n) ?? i18n.text("editor.blueprint.fallbackCategory")}
        </span>
        <div className="blueprint-node__actions">
          {debuggerContext ? (
            <button
              className={`blueprint-node__debug-button${hasBreakpoint ? " blueprint-node__debug-button--active" : ""}`}
              data-testid={`debug-node-breakpoint-toggle-${id}`}
              onClick={(event) => {
                event.preventDefault();
                event.stopPropagation();
                debuggerContext.onToggleBreakpoint(id);
              }}
              type="button"
            >
              {hasBreakpoint ? i18n.text("editor.debugger.clearBreakpoint") : i18n.text("editor.debugger.setBreakpoint")}
            </button>
          ) : null}
          <span className="blueprint-node__type">{data.nodeType}</span>
        </div>
      </div>

      <div className="blueprint-node__body">
        <div className="blueprint-node__title-row">
          <h3 className="blueprint-node__title">{resolveNodeLabel(data, i18n)}</h3>
          <span className="blueprint-node__status">{statusLabel}</span>
        </div>

        <p className="blueprint-node__description">
          {resolveNodeDescription(data, i18n)?.trim() || i18n.text("editor.blueprint.fallbackDescription")}
        </p>

        <div className="blueprint-node__ports">
          <div className="blueprint-node__port-column">
            <span className="blueprint-node__port-label">{i18n.text("editor.blueprint.inputs")}</span>
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
                    <span className="blueprint-node__port-name">{getPortDisplayLabel(port, i18n)}</span>
                  </div>
                ))
              ) : (
                <div className="blueprint-node__port-empty">{i18n.text("editor.blueprint.noInputs")}</div>
              )}
            </div>
          </div>

          <div className="blueprint-node__port-column blueprint-node__port-column--output">
            <span className="blueprint-node__port-label blueprint-node__port-label--out">
              {i18n.text("editor.blueprint.outputs")}
            </span>
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
                    <span className="blueprint-node__port-name">{getPortDisplayLabel(port, i18n)}</span>
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
                <div className="blueprint-node__port-empty blueprint-node__port-empty--out">
                  {i18n.text("editor.blueprint.noOutputs")}
                </div>
              )}
            </div>
          </div>
        </div>

        <div className="blueprint-node__fields">
          {valueEntries.length ? (
            valueEntries.map(([key, value]) => (
              <div className="blueprint-node__field" key={key}>
                <span className="blueprint-node__field-key">{key}</span>
                <span className="blueprint-node__field-value">{formatValue(value, i18n.text)}</span>
              </div>
            ))
          ) : (
            <div className="blueprint-node__field blueprint-node__field--empty">
              <span className="blueprint-node__field-key">{i18n.text("editor.blueprint.values")}</span>
              <span className="blueprint-node__field-value">{i18n.text("editor.blueprint.noEditableFields")}</span>
            </div>
          )}
        </div>

        {hiddenFieldCount ? (
          <div className="blueprint-node__footer">
            {i18n.text("editor.blueprint.moreFieldRows", { count: hiddenFieldCount })}
          </div>
        ) : null}
      </div>
    </div>
  );
}
