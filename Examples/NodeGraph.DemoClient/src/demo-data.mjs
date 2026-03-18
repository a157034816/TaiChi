export const demoNodeLibrary = [
  {
    type: "start",
    label: "Start",
    description: "Entry point for a new workflow.",
    category: "control",
    inputs: [],
    outputs: [{ id: "next", label: "Next" }],
    fields: [
      {
        key: "note",
        label: "Opening note",
        kind: "text",
        defaultValue: "Kick off the workflow",
      },
    ],
    appearance: {
      bgColor: "#ecfdf5",
      borderColor: "#10b981",
      textColor: "#14532d",
    },
  },
  {
    type: "parallel_split",
    label: "Parallel Split",
    description: "Fan out one trigger into multiple review branches.",
    category: "control",
    inputs: [{ id: "trigger", label: "Trigger" }],
    outputs: [
      { id: "finance", label: "Finance" },
      { id: "legal", label: "Legal" },
      { id: "security", label: "Security" },
    ],
    fields: [
      {
        key: "strategy",
        label: "Dispatch strategy",
        kind: "text",
        defaultValue: "broadcast",
      },
    ],
    appearance: {
      bgColor: "#eff6ff",
      borderColor: "#0ea5e9",
      textColor: "#0c4a6e",
    },
  },
  {
    type: "merge",
    label: "Merge",
    description: "Collect multiple upstream branches before continuing.",
    category: "control",
    inputs: [
      { id: "finance", label: "Finance" },
      { id: "legal", label: "Legal" },
      { id: "security", label: "Security" },
    ],
    outputs: [{ id: "next", label: "Next" }],
    fields: [
      {
        key: "waitForAll",
        label: "Wait for all branches",
        kind: "boolean",
        defaultValue: true,
      },
    ],
    appearance: {
      bgColor: "#fdf4ff",
      borderColor: "#c026d3",
      textColor: "#701a75",
    },
  },
  {
    type: "approval",
    label: "Approval",
    description: "Manual approval step with explicit approved and rejected exits.",
    category: "workflow",
    inputs: [{ id: "request", label: "Request" }],
    outputs: [
      { id: "approved", label: "Approved" },
      { id: "rejected", label: "Rejected" },
    ],
    fields: [
      {
        key: "owner",
        label: "Owner",
        kind: "text",
        defaultValue: "finance.manager",
      },
      {
        key: "slaHours",
        label: "SLA hours",
        kind: "number",
        defaultValue: 24,
      },
    ],
    appearance: {
      bgColor: "#fff7ed",
      borderColor: "#f97316",
      textColor: "#7c2d12",
    },
  },
  {
    type: "notify",
    label: "Notify",
    description: "Send success or failure notifications after approval completes.",
    category: "integration",
    inputs: [
      { id: "success", label: "Success" },
      { id: "failure", label: "Failure" },
    ],
    outputs: [],
    fields: [
      {
        key: "channel",
        label: "Channel",
        kind: "text",
        defaultValue: "email",
      },
      {
        key: "includeSummary",
        label: "Include summary",
        kind: "boolean",
        defaultValue: true,
      },
    ],
    appearance: {
      bgColor: "#eef2ff",
      borderColor: "#6366f1",
      textColor: "#312e81",
    },
  },
];

export function createGraphDocument(graphName, graphMode = "new") {
  if (graphMode === "existing") {
    return createExistingGraph(graphName);
  }

  return {
    name: graphName,
    description: "Created from the NodeGraph demo client.",
    nodes: [],
    edges: [],
    viewport: {
      x: 0,
      y: 0,
      zoom: 1,
    },
  };
}

function createExistingGraph(graphName) {
  return {
    graphId: "demo-existing-graph",
    name: graphName,
    description: "A pre-filled multi-port workflow used to demo existing graph editing.",
    nodes: [
      {
        id: "node_start",
        type: "default",
        position: { x: 80, y: 220 },
        data: {
          label: "Start",
          description: "Receive the original request.",
          category: "control",
          nodeType: "start",
          inputs: [],
          outputs: [{ id: "next", label: "Next" }],
          values: {
            note: "Request entered from the demo client",
          },
        },
        style: {
          background: "#ecfdf5",
          borderColor: "#10b981",
          color: "#14532d",
          borderWidth: 1,
          borderRadius: 20,
        },
      },
      {
        id: "node_parallel_split",
        type: "default",
        position: { x: 360, y: 220 },
        data: {
          label: "Parallel Split",
          description: "Dispatch the request to finance, legal, and security reviewers.",
          category: "control",
          nodeType: "parallel_split",
          inputs: [{ id: "trigger", label: "Trigger" }],
          outputs: [
            { id: "finance", label: "Finance" },
            { id: "legal", label: "Legal" },
            { id: "security", label: "Security" },
          ],
          values: {
            strategy: "broadcast",
          },
        },
        style: {
          background: "#eff6ff",
          borderColor: "#0ea5e9",
          color: "#0c4a6e",
          borderWidth: 1,
          borderRadius: 20,
        },
      },
      {
        id: "node_merge",
        type: "default",
        position: { x: 700, y: 220 },
        data: {
          label: "Merge",
          description: "Wait for the parallel checks before sending the consolidated request forward.",
          category: "control",
          nodeType: "merge",
          inputs: [
            { id: "finance", label: "Finance" },
            { id: "legal", label: "Legal" },
            { id: "security", label: "Security" },
          ],
          outputs: [{ id: "next", label: "Next" }],
          values: {
            waitForAll: true,
          },
        },
        style: {
          background: "#fdf4ff",
          borderColor: "#c026d3",
          color: "#701a75",
          borderWidth: 1,
          borderRadius: 20,
        },
      },
      {
        id: "node_approval",
        type: "default",
        position: { x: 1040, y: 220 },
        data: {
          label: "Approval",
          description: "Manager checks the aggregated review bundle.",
          category: "workflow",
          nodeType: "approval",
          inputs: [{ id: "request", label: "Request" }],
          outputs: [
            { id: "approved", label: "Approved" },
            { id: "rejected", label: "Rejected" },
          ],
          values: {
            owner: "finance.manager",
            slaHours: 24,
          },
        },
        style: {
          background: "#fff7ed",
          borderColor: "#f97316",
          color: "#7c2d12",
          borderWidth: 1,
          borderRadius: 20,
        },
      },
      {
        id: "node_notify",
        type: "default",
        position: { x: 1400, y: 220 },
        data: {
          label: "Notify",
          description: "Tell the requester whether the request was approved or rejected.",
          category: "integration",
          nodeType: "notify",
          inputs: [
            { id: "success", label: "Success" },
            { id: "failure", label: "Failure" },
          ],
          outputs: [],
          values: {
            channel: "email",
            includeSummary: true,
          },
        },
        style: {
          background: "#eef2ff",
          borderColor: "#6366f1",
          color: "#312e81",
          borderWidth: 1,
          borderRadius: 20,
        },
      },
    ],
    edges: [
      {
        id: "edge_start_split",
        source: "node_start",
        sourceHandle: "next",
        target: "node_parallel_split",
        targetHandle: "trigger",
      },
      {
        id: "edge_split_merge_finance",
        source: "node_parallel_split",
        sourceHandle: "finance",
        target: "node_merge",
        targetHandle: "finance",
      },
      {
        id: "edge_split_merge_legal",
        source: "node_parallel_split",
        sourceHandle: "legal",
        target: "node_merge",
        targetHandle: "legal",
      },
      {
        id: "edge_split_merge_security",
        source: "node_parallel_split",
        sourceHandle: "security",
        target: "node_merge",
        targetHandle: "security",
      },
      {
        id: "edge_merge_approval",
        source: "node_merge",
        sourceHandle: "next",
        target: "node_approval",
        targetHandle: "request",
      },
      {
        id: "edge_approval_notify_success",
        source: "node_approval",
        sourceHandle: "approved",
        target: "node_notify",
        targetHandle: "success",
      },
      {
        id: "edge_approval_notify_failure",
        source: "node_approval",
        sourceHandle: "rejected",
        target: "node_notify",
        targetHandle: "failure",
      },
    ],
    viewport: {
      x: 120,
      y: 40,
      zoom: 0.72,
    },
  };
}
