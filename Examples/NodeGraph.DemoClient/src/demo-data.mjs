import { ApprovalDecision, ReviewTask, WorkflowRequest } from "./contracts.mjs";

const workflowRequestType = "workflow/request";
const reviewTaskType = "workflow/review-task";
const approvalDecisionType = "workflow/approval-decision";

const categoryKeys = {
  control: "categories.control",
  integration: "categories.integration",
  workflow: "categories.workflow",
};

const nodeKeys = {
  approval: {
    description: "nodes.approval.description",
    label: "nodes.approval.label",
  },
  merge: {
    description: "nodes.merge.description",
    label: "nodes.merge.label",
  },
  notify: {
    description: "nodes.notify.description",
    label: "nodes.notify.label",
  },
  parallelSplit: {
    description: "nodes.parallelSplit.description",
    label: "nodes.parallelSplit.label",
  },
  start: {
    description: "nodes.start.description",
    label: "nodes.start.label",
  },
};

const fieldKeys = {
  channel: "fields.channel.label",
  includeSummary: "fields.includeSummary.label",
  note: "fields.note.label",
  owner: "fields.owner.label",
  slaHours: "fields.slaHours.label",
  strategy: "fields.strategy.label",
  waitForAll: "fields.waitForAll.label",
};

const portKeys = {
  approved: "ports.approved",
  failure: "ports.failure",
  finance: "ports.finance",
  legal: "ports.legal",
  next: "ports.next",
  rejected: "ports.rejected",
  request: "ports.request",
  security: "ports.security",
  success: "ports.success",
  trigger: "ports.trigger",
};

export const demoI18n = {
  defaultLocale: "en",
  locales: {
    en: {
      [categoryKeys.control]: "Control",
      [categoryKeys.integration]: "Integration",
      [categoryKeys.workflow]: "Workflow",
      [fieldKeys.channel]: "Channel",
      [fieldKeys.includeSummary]: "Include summary",
      [fieldKeys.note]: "Opening note",
      [fieldKeys.owner]: "Owner",
      [fieldKeys.slaHours]: "SLA hours",
      [fieldKeys.strategy]: "Dispatch strategy",
      [fieldKeys.waitForAll]: "Wait for all branches",
      [nodeKeys.approval.description]: "Manual approval step with explicit approved and rejected exits.",
      [nodeKeys.approval.label]: "Approval",
      [nodeKeys.merge.description]: "Collect multiple upstream branches before continuing.",
      [nodeKeys.merge.label]: "Merge",
      [nodeKeys.notify.description]: "Send success or failure notifications after approval completes.",
      [nodeKeys.notify.label]: "Notify",
      [nodeKeys.parallelSplit.description]: "Fan out one trigger into multiple review branches.",
      [nodeKeys.parallelSplit.label]: "Parallel Split",
      [nodeKeys.start.description]: "Entry point for a new workflow.",
      [nodeKeys.start.label]: "Start",
      [portKeys.approved]: "Approved",
      [portKeys.failure]: "Failure",
      [portKeys.finance]: "Finance",
      [portKeys.legal]: "Legal",
      [portKeys.next]: "Next",
      [portKeys.rejected]: "Rejected",
      [portKeys.request]: "Request",
      [portKeys.security]: "Security",
      [portKeys.success]: "Success",
      [portKeys.trigger]: "Trigger",
    },
    "zh-CN": {
      [categoryKeys.control]: "控制",
      [categoryKeys.integration]: "集成",
      [categoryKeys.workflow]: "流程",
      [fieldKeys.channel]: "通道",
      [fieldKeys.includeSummary]: "包含摘要",
      [fieldKeys.note]: "开场说明",
      [fieldKeys.owner]: "负责人",
      [fieldKeys.slaHours]: "SLA 小时",
      [fieldKeys.strategy]: "分发策略",
      [fieldKeys.waitForAll]: "等待全部分支",
      [nodeKeys.approval.description]: "带有明确通过与驳回出口的人工审批步骤。",
      [nodeKeys.approval.label]: "审批",
      [nodeKeys.merge.description]: "在继续前汇集多个上游分支。",
      [nodeKeys.merge.label]: "汇合",
      [nodeKeys.notify.description]: "审批结束后发送成功或失败通知。",
      [nodeKeys.notify.label]: "通知",
      [nodeKeys.parallelSplit.description]: "把一个触发拆分成多个并行评审分支。",
      [nodeKeys.parallelSplit.label]: "并行分发",
      [nodeKeys.start.description]: "新工作流的入口节点。",
      [nodeKeys.start.label]: "开始",
      [portKeys.approved]: "通过",
      [portKeys.failure]: "失败",
      [portKeys.finance]: "财务",
      [portKeys.legal]: "法务",
      [portKeys.next]: "下一步",
      [portKeys.rejected]: "驳回",
      [portKeys.request]: "请求",
      [portKeys.security]: "安全",
      [portKeys.success]: "成功",
      [portKeys.trigger]: "触发",
    },
  },
};

function translate(locale, key) {
  return demoI18n.locales[locale]?.[key] ?? key;
}

function createPort(id, labelKey, dataType) {
  return dataType ? { id, labelKey, dataType } : { id, labelKey };
}

function createField(key, labelKey, kind, defaultValue) {
  return defaultValue === undefined
    ? { key, labelKey, kind }
    : { key, labelKey, kind, defaultValue };
}

function createStoredNode({
  id,
  nodeType,
  position,
  labelKey,
  descriptionKey,
  categoryKey,
  inputs = [],
  outputs = [],
  values = {},
  appearance,
}) {
  return {
    id,
    type: "default",
    position,
    data: {
      label: translate("en", labelKey),
      labelKey,
      description: translate("en", descriptionKey),
      descriptionKey,
      categoryKey,
      nodeType,
      inputs,
      outputs,
      values,
      appearance,
    },
    style: {
      background: appearance?.bgColor,
      borderColor: appearance?.borderColor,
      color: appearance?.textColor,
      borderWidth: 1,
      borderRadius: 20,
    },
  };
}

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
    labelKey: nodeKeys.start.label,
    descriptionKey: nodeKeys.start.description,
    categoryKey: categoryKeys.control,
    inputs: [],
    outputs: [createPort("next", portKeys.next, workflowRequestType)],
    fields: [
      createField("note", fieldKeys.note, "text", "Kick off the workflow"),
    ],
    appearance: {
      bgColor: "#ecfdf5",
      borderColor: "#10b981",
      textColor: "#14532d",
    },
  },
  {
    type: "parallel_split",
    labelKey: nodeKeys.parallelSplit.label,
    descriptionKey: nodeKeys.parallelSplit.description,
    categoryKey: categoryKeys.control,
    inputs: [createPort("trigger", portKeys.trigger, workflowRequestType)],
    outputs: [
      createPort("finance", portKeys.finance, reviewTaskType),
      createPort("legal", portKeys.legal, reviewTaskType),
      createPort("security", portKeys.security, reviewTaskType),
    ],
    fields: [
      createField("strategy", fieldKeys.strategy, "text", "broadcast"),
    ],
    appearance: {
      bgColor: "#eff6ff",
      borderColor: "#0ea5e9",
      textColor: "#0c4a6e",
    },
  },
  {
    type: "merge",
    labelKey: nodeKeys.merge.label,
    descriptionKey: nodeKeys.merge.description,
    categoryKey: categoryKeys.control,
    inputs: [
      createPort("finance", portKeys.finance, reviewTaskType),
      createPort("legal", portKeys.legal, reviewTaskType),
      createPort("security", portKeys.security, reviewTaskType),
    ],
    outputs: [createPort("next", portKeys.next, workflowRequestType)],
    fields: [
      createField("waitForAll", fieldKeys.waitForAll, "boolean", true),
    ],
    appearance: {
      bgColor: "#fdf4ff",
      borderColor: "#c026d3",
      textColor: "#701a75",
    },
  },
  {
    type: "approval",
    labelKey: nodeKeys.approval.label,
    descriptionKey: nodeKeys.approval.description,
    categoryKey: categoryKeys.workflow,
    inputs: [createPort("request", portKeys.request, workflowRequestType)],
    outputs: [
      createPort("approved", portKeys.approved, approvalDecisionType),
      createPort("rejected", portKeys.rejected, approvalDecisionType),
    ],
    fields: [
      createField("owner", fieldKeys.owner, "text", "finance.manager"),
      createField("slaHours", fieldKeys.slaHours, "number", 24),
    ],
    appearance: {
      bgColor: "#fff7ed",
      borderColor: "#f97316",
      textColor: "#7c2d12",
    },
  },
  {
    type: "notify",
    labelKey: nodeKeys.notify.label,
    descriptionKey: nodeKeys.notify.description,
    categoryKey: categoryKeys.integration,
    inputs: [
      createPort("success", portKeys.success, approvalDecisionType),
      createPort("failure", portKeys.failure, approvalDecisionType),
    ],
    outputs: [],
    fields: [
      createField("channel", fieldKeys.channel, "text", "email"),
      createField("includeSummary", fieldKeys.includeSummary, "boolean", true),
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
      createStoredNode({
        id: "node_start",
        nodeType: "start",
        position: { x: 80, y: 220 },
        labelKey: nodeKeys.start.label,
        descriptionKey: nodeKeys.start.description,
        categoryKey: categoryKeys.control,
        inputs: [],
        outputs: [createPort("next", portKeys.next, workflowRequestType)],
        values: {
          note: "Request entered from the demo client",
        },
        appearance: {
          bgColor: "#ecfdf5",
          borderColor: "#10b981",
          textColor: "#14532d",
        },
      }),
      createStoredNode({
        id: "node_parallel_split",
        nodeType: "parallel_split",
        position: { x: 360, y: 220 },
        labelKey: nodeKeys.parallelSplit.label,
        descriptionKey: nodeKeys.parallelSplit.description,
        categoryKey: categoryKeys.control,
        inputs: [createPort("trigger", portKeys.trigger, workflowRequestType)],
        outputs: [
          createPort("finance", portKeys.finance, reviewTaskType),
          createPort("legal", portKeys.legal, reviewTaskType),
          createPort("security", portKeys.security, reviewTaskType),
        ],
        values: {
          strategy: "broadcast",
        },
        appearance: {
          bgColor: "#eff6ff",
          borderColor: "#0ea5e9",
          textColor: "#0c4a6e",
        },
      }),
      createStoredNode({
        id: "node_merge",
        nodeType: "merge",
        position: { x: 700, y: 220 },
        labelKey: nodeKeys.merge.label,
        descriptionKey: nodeKeys.merge.description,
        categoryKey: categoryKeys.control,
        inputs: [
          createPort("finance", portKeys.finance, reviewTaskType),
          createPort("legal", portKeys.legal, reviewTaskType),
          createPort("security", portKeys.security, reviewTaskType),
        ],
        outputs: [createPort("next", portKeys.next, workflowRequestType)],
        values: {
          waitForAll: true,
        },
        appearance: {
          bgColor: "#fdf4ff",
          borderColor: "#c026d3",
          textColor: "#701a75",
        },
      }),
      createStoredNode({
        id: "node_approval",
        nodeType: "approval",
        position: { x: 1040, y: 220 },
        labelKey: nodeKeys.approval.label,
        descriptionKey: nodeKeys.approval.description,
        categoryKey: categoryKeys.workflow,
        inputs: [createPort("request", portKeys.request, workflowRequestType)],
        outputs: [
          createPort("approved", portKeys.approved, approvalDecisionType),
          createPort("rejected", portKeys.rejected, approvalDecisionType),
        ],
        values: {
          owner: "finance.manager",
          slaHours: 24,
        },
        appearance: {
          bgColor: "#fff7ed",
          borderColor: "#f97316",
          textColor: "#7c2d12",
        },
      }),
      createStoredNode({
        id: "node_notify",
        nodeType: "notify",
        position: { x: 1400, y: 220 },
        labelKey: nodeKeys.notify.label,
        descriptionKey: nodeKeys.notify.description,
        categoryKey: categoryKeys.integration,
        inputs: [
          createPort("success", portKeys.success, approvalDecisionType),
          createPort("failure", portKeys.failure, approvalDecisionType),
        ],
        outputs: [],
        values: {
          channel: "email",
          includeSummary: true,
        },
        appearance: {
          bgColor: "#eef2ff",
          borderColor: "#6366f1",
          textColor: "#312e81",
        },
      }),
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
