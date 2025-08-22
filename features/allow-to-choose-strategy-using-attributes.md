# RFC: Allow to choose patch strategy using attributes on POCOs

**Status:** Approved
**Author(s):** Alexander Mantzoukas
**Date:** 2025-08-22

<!---Human--->
## 1. Summary
<!---
Provide a one-paragraph explanation of the feature and the proposed change. Keep it concise and high-level.
--->
We need to be able to choose strategy for specific fields in POCOs, like allowing a field to be a counter swould generate a different patch strategy.

<!---Human--->
## 2. Motivation
<!---
Explain why this change is necessary. What problem does it solve? What is the user or business value? You can link to user requests, bug reports, or business goals.
--->
This will allow better handling of edge cases and also introduce an extensible entrypoint for others to implement their own CRDT merge/patch logic.

<!---AI/Human--->
## 3. High-Level Design
<!---
This section is for the proposed technical architecture. It can be drafted by a human and refined by the AI, or vice-versa.
Use diagrams (e.g., Mermaid.js), and explain how new components will interact with existing ones. This is the core of the proposal.
--->
- We should be able to decorate POCOs properties with attributes (default merge tactic would be LWW)
- We should have a high level service/utility that would keep the JSON metadata clean and manageable and would hide the JsonNode use from our API.
- The metadata should still be in a different entity.
- We should include other basic merge strategy implementations (attribute and service pair)
- Performance is important: We should make sure our implementations are fast, so we need to generate benchmark console project
- We should update the readme, as a last task, to reflect the changes of the API.

<!---AI/Human--->
## 4. Alternatives Considered 
<!---
Describe other solutions or approaches that were considered and explain why they were not chosen. This shows a thorough thought process.
It should look like this:
- **[Alternative A]:** [Description and why it was rejected.]
- **[Alternative B]:** [Description and why it was rejected.]
--->
No other alternatives have been considered.

<!---AI--->
## Implementation Specs
- [X] `$/features/allow-to-choose-strategy-using-attributes-specs/01-crdt-strategy-attribute-and-interface.md`: `Create the core attribute and interface for the CRDT strategy pattern.`
- [X] `$/features/allow-to-choose-strategy-using-attributes-specs/02-lww-strategy-implementation.md`: `Implement the default Last-Writer-Wins (LWW) strategy.`
- [X] `$/features/allow-to-choose-strategy-using-attributes-specs/03-counter-strategy-implementation.md`: `Implement a new 'Counter' strategy for numeric fields.`
- [X] `$/features/allow-to-choose-strategy-using-attributes-specs/04-refactor-patcher-to-use-strategies.md`: `Refactor the JsonCrdtPatcher to use the strategy pattern for patch generation.`
- [ ] `$/features/allow-to-choose-strategy-using-attributes-specs/05-refactor-applicator-to-use-strategies.md`: `Refactor the JsonCrdtApplicator to use the strategy pattern for patch application.`
- [ ] `$/features/allow-to-choose-strategy-using-attributes-specs/06-create-benchmark-project.md`: `Create a benchmark project to monitor performance.`
- [ ] `$/features/allow-to-choose-strategy-using-attributes-specs/07-update-readme-documentation.md`: `Update the README file with documentation for the new features.`