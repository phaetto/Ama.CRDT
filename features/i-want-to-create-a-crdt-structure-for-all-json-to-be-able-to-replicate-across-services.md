# RFC: Create CRDTs for JSON structures

**Status:** Approved
**Author(s):** Alexander Mantzoukas
**Date:** 2025-08-21

## 1. Summary
We need to have stctures that can be updated async and they are strongly eventual consistent for usage behind queues and othe async barriers.

## 2. Motivation
We don't want to create and replicate code for each need of a structure that needs a state on every project. So we need a library that help us to do that, and the nmost extensive way to do it is on JSON.

## 3. High-Level Design
<!---
This section is for the proposed technical architecture. It can be drafted by a human and refined by the AI, or vice-versa.
Use diagrams (e.g., Mermaid.js), and explain how new components will interact with existing ones. This is the core of the proposal.
--->
	
	- We need a way to initialize a CRDT using System.Json and resolving it with another Json.
	- In high level we need to be able to accept types so we can make it easy for the user to add his own type.

## 4. Alternatives Considered
<!---
Describe other solutions or approaches that were considered and explain why they were not chosen. This shows a thorough thought process.
It should look like this:
- **[Alternative A]:** [Description and why it was rejected.]
- **[Alternative B]:** [Description and why it was rejected.]
--->

## Implementation Specs
- [X] `Core CRDT Data Structures`: $/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/01-core-crdt-data-structures.md
- [X] `JSON Diff and Patch Generation`: $/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/02-json-diff-and-patch-generation.md
- [X] `JSON Patch Application`: $/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/03-json-patch-application.md
- [X] `Public API and Integration`: $/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/04-public-api-and-integration.md
- [X] `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/put-the-lww-structures-in-metadata.md`: `Put the LWW structures in metadata`