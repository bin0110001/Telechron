using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Findings;

// R-FIX8/R-BUILD4: the one shared signal set both repair-Finding
// classification and synthesis-failure classification need. Deliberately
// generic over "the thing that failed" (a Run, a Workflow step, a
// container execution) rather than typed to any one of them -- R-ENG4
// forbids parallel classification mechanisms, so this must serve every
// caller.
public sealed record FailureClassificationInput(
    RunStatus? RunStatus,
    WorkflowRunStatus? WorkflowRunStatus,
    // True if the underlying container/Agent execution itself reported a
    // non-completion outcome (timeout, resource-limit kill, connection
    // loss) rather than the workload running to completion and failing on
    // its own terms.
    bool ContainerOrAgentInfrastructureFailure,
    string? OutputText);
