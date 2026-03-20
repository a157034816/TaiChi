import { ApprovalDecision, ReviewTask, WorkflowRequest } from "./contracts.mjs";

const workflowRequestType = "workflow/request";
const reviewTaskType = "workflow/review-task";
const approvalDecisionType = "workflow/approval-decision";
const text = (zhCN, en) => ({ "zh-CN": zhCN, en });

export const demoTypeMappings = [
  {
    canonicalId: workflowRequestType,
    type: WorkflowRequest.name,
    color: "#0ea5e9",
  },
  {
    canonicalId: reviewTaskType,
    type: ReviewTask.name,
    color: "#22c55e",
  },
  {
    canonicalId: approvalDecisionType,
    type: ApprovalDecision.name,
    color: "#f97316",
  },
];

export const demoNodeLibrary = [
  {
    type: "start",
    label: text("开始", "Start"),
    description: text("新工作流的入口节点。", "Entry point for a new workflow."),
    category: text("控制", "control"),
    inputs: [],
    outputs: [{ id: "next", label: text("下一步", "Next"), dataType: workflowRequestType }],
    fields: [
      {
        key: "note",
        label: text("开场说明", "Opening note"),
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
    label: text("并行分发", "Parallel Split"),
    description: text("把一个触发拆分成多个并行评审分支。", "Fan out one trigger into multiple review branches."),
    category: text("控制", "control"),
    inputs: [{ id: "trigger", label: text("触发", "Trigger"), dataType: workflowRequestType }],
    outputs: [
      { id: "finance", label: text("财务", "Finance"), dataType: reviewTaskType },
      { id: "legal", label: text("法务", "Legal"), dataType: reviewTaskType },
      { id: "security", label: text("安全", "Security"), dataType: reviewTaskType },
    ],
    fields: [
      {
        key: "strategy",
        label: text("分发策略", "Dispatch strategy"),
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
    label: text("汇合", "Merge"),
    description: text("在继续前汇集多个上游分支。", "Collect multiple upstream branches before continuing."),
    category: text("控制", "control"),
    inputs: [
      { id: "finance", label: text("财务", "Finance"), dataType: reviewTaskType },
      { id: "legal", label: text("法务", "Legal"), dataType: reviewTaskType },
      { id: "security", label: text("安全", "Security"), dataType: reviewTaskType },
    ],
    outputs: [{ id: "next", label: text("下一步", "Next"), dataType: workflowRequestType }],
    fields: [
      {
        key: "waitForAll",
        label: text("等待全部分支", "Wait for all branches"),
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
    label: text("审批", "Approval"),
    description: text("带有明确通过与驳回出口的人工审批步骤。", "Manual approval step with explicit approved and rejected exits."),
    category: text("流程", "workflow"),
    inputs: [{ id: "request", label: text("请求", "Request"), dataType: workflowRequestType }],
    outputs: [
      { id: "approved", label: text("通过", "Approved"), dataType: approvalDecisionType },
      { id: "rejected", label: text("驳回", "Rejected"), dataType: approvalDecisionType },
    ],
    fields: [
      {
        key: "owner",
        label: text("负责人", "Owner"),
        kind: "text",
        defaultValue: "finance.manager",
      },
      {
        key: "slaHours",
        label: text("SLA 小时", "SLA hours"),
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
    label: text("通知", "Notify"),
    description: text("审批结束后发送成功或失败通知。", "Send success or failure notifications after approval completes."),
    category: text("集成", "integration"),
    inputs: [
      { id: "success", label: text("成功", "Success"), dataType: approvalDecisionType },
      { id: "failure", label: text("失败", "Failure"), dataType: approvalDecisionType },
    ],
    outputs: [],
    fields: [
      {
        key: "channel",
        label: text("通道", "Channel"),
        kind: "text",
        defaultValue: "email",
      },
      {
        key: "includeSummary",
        label: text("包含摘要", "Include summary"),
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
          category: text("控制", "control"),
          nodeType: "start",
          inputs: [],
          outputs: [{ id: "next", label: text("下一步", "Next"), dataType: workflowRequestType }],
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
          category: text("控制", "control"),
          nodeType: "parallel_split",
          inputs: [{ id: "trigger", label: text("触发", "Trigger"), dataType: workflowRequestType }],
          outputs: [
            { id: "finance", label: text("财务", "Finance"), dataType: reviewTaskType },
            { id: "legal", label: text("法务", "Legal"), dataType: reviewTaskType },
            { id: "security", label: text("安全", "Security"), dataType: reviewTaskType },
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
          category: text("控制", "control"),
          nodeType: "merge",
          inputs: [
            { id: "finance", label: text("财务", "Finance"), dataType: reviewTaskType },
            { id: "legal", label: text("法务", "Legal"), dataType: reviewTaskType },
            { id: "security", label: text("安全", "Security"), dataType: reviewTaskType },
          ],
          outputs: [{ id: "next", label: text("下一步", "Next"), dataType: workflowRequestType }],
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
          category: text("流程", "workflow"),
          nodeType: "approval",
          inputs: [{ id: "request", label: text("请求", "Request"), dataType: workflowRequestType }],
          outputs: [
            { id: "approved", label: text("通过", "Approved"), dataType: approvalDecisionType },
            { id: "rejected", label: text("驳回", "Rejected"), dataType: approvalDecisionType },
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
          category: text("集成", "integration"),
          nodeType: "notify",
          inputs: [
            { id: "success", label: text("成功", "Success"), dataType: approvalDecisionType },
            { id: "failure", label: text("失败", "Failure"), dataType: approvalDecisionType },
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
