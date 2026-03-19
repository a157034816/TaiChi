export interface ConnectionCreationMenuTriggerInput {
  droppedOnBlankCanvas: boolean;
  hasClientPosition: boolean;
  hasPendingConnection: boolean;
  hasTargetHandle: boolean;
  hasTargetNode: boolean;
}

export interface PaneClickSuppressionResult {
  nextShouldSuppressPaneClick: boolean;
  shouldIgnorePaneClick: boolean;
}

export function shouldOpenConnectionCreationMenu({
  droppedOnBlankCanvas,
  hasClientPosition,
  hasPendingConnection,
  hasTargetHandle,
  hasTargetNode,
}: ConnectionCreationMenuTriggerInput) {
  return (
    hasPendingConnection &&
    hasClientPosition &&
    !hasTargetNode &&
    !hasTargetHandle &&
    droppedOnBlankCanvas
  );
}

export function consumePaneClickSuppression(shouldSuppressNextPaneClick: boolean): PaneClickSuppressionResult {
  return {
    shouldIgnorePaneClick: shouldSuppressNextPaneClick,
    nextShouldSuppressPaneClick: false,
  };
}
