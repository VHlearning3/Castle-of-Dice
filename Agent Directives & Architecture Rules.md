# Agent Directives & Architecture Rules

## Planning Workflow (/plan)
Whenever `/plan` is initiated:
1. **Design Grounding:** Always query the `notebooklm` tool to look up game mechanics, formulas, and balance rules before writing an architectural plan.
2. **Target Notebook:** For core gameplay rules, refer to the 🎲 [Castle of Dice](https://notebooklm.google.com/notebook/ab6acec3-5413-4df2-8352-5fd173af3806) notebook.
3. **No Speculative Mechanics:** Never invent arbitrary stat modifiers, combat phases, or dialogue DCs if they are already specified in the NotebookLM design documents.
4. **Output Artifact:** Structure the plan with:
   - *Design Reference Summary* (what specs were retrieved from NotebookLM)
   - *Architecture & State Flow* (interfaces, classes, scriptable objects)
   - *Step-by-Step Task Breakdown*
   - *Verification & Unit Test Strategy*