namespace Telechron.Sdk.Domain;

// A Requirement extracted from a markdown design document, before it becomes
// a persisted Requirement row (R-DM16). Kept in Sdk so both the parser and
// any future consumer (e.g. a diff/drift tool) can share the shape without a
// Host dependency.
public sealed record ParsedRequirement(string RequirementId, string Title, string Body);
