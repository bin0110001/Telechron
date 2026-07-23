namespace Telechron.Sdk.Modules.Llm;

// R-LLM5: SystemPrompt and Instructions are trusted content (Persona
// authoring, Project configuration). UntrustedContentBlocks holds
// anything attacker-influenceable -- Finding free-text, connector
// responses (a fetched GitHub issue body, a CVE description) -- kept
// structurally separate so an engine module is FORCED to render it as
// inert delimited data rather than being tempted to string-concatenate
// it into the instruction stream. See PromptRenderer for how these are
// combined; engine modules never receive a single pre-flattened string
// for anything containing UntrustedContentBlocks.
public sealed record LlmCompletionRequest(
    string SystemPrompt,
    string Instructions,
    IReadOnlyList<UntrustedContentBlock> UntrustedContentBlocks,
    string ModelOverride,
    double Temperature,
    int MaxOutputTokens);

// Label is a short, human-readable provenance tag (e.g. "Finding
// description", "GitHub issue #42 body") -- rendered alongside the
// content so the LLM has context on WHERE untrusted text came from
// without that provenance tag itself being trusted as an instruction.
public sealed record UntrustedContentBlock(string Label, string Content);
