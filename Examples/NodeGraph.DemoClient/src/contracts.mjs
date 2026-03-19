export class WorkflowRequest {
  constructor({ requestId = "", note = "" } = {}) {
    this.requestId = requestId;
    this.note = note;
  }
}

export class ReviewTask {
  constructor({ reviewer = "", instructions = "" } = {}) {
    this.reviewer = reviewer;
    this.instructions = instructions;
  }
}

export class ApprovalDecision {
  constructor({ approved = false, reason = "" } = {}) {
    this.approved = approved;
    this.reason = reason;
  }
}

