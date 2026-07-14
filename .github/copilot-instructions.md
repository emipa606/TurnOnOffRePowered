# Copilot Instructions for RimWorld Modding Project: Turn It On and Off - RePowered

## Mod Overview and Purpose

**Turn It On and Off - RePowered** is a RimWorld mod that synergizes the functionalities of two existing mods: "Turn It On and Off" and "RePower." Its primary objective is to offer a more realistic power consumption experience within the game by dynamically adjusting the power requirements of devices based on their operational state. It scales power usage based on building activity and includes customizable multipliers for adjusting vanilla power-draw settings.

## Key Features and Systems

- **Dynamic Power Management**: Buildings such as electric stoves draw realistic power levels when active and conserve energy when dormant.
  
- **Turret Power Usage**: Turrets based on the `Building_Turret` class consume higher power during active targeting.

- **Door Automation**: Autodoors using the vanilla door class, and those in "Doors Expanded," are supported for automatic power adjustments.

- **Project RimFactory Integration**: Supports both workbenches and automatic assemblers from "Project RimFactory Revived."

- **Conditional Workblocking**: Allows workbenches inheriting the vanilla class to block usage when insufficient power is available.

- **Localization Support**: Currently includes translations in French and Russian.

- **No Dependencies**: As of version 1.6, reliance on HugsLib has been removed, enhancing compatibility and ease of use.

## Coding Patterns and Conventions

- **C# Code Structure**: The mod utilizes a structured approach with separate files for different Harmony patches, ensuring clarity and maintainability.

- **Consistent Naming**: Classes and methods follow clear, descriptive naming conventions that reflect their functionality, such as `Building_WorkTable_UsedThisTick`.

## XML Integration

- **Power Settings**: XML files are utilized for storing and applying custom power settings, providing flexibility for users to tweak power draw multipliers.

- **Def Integration**: The mod uses XML definitions, such as `UpdateDef`, for managing version updates and feature changes seamlessly.

## Harmony Patching

- **Prefix and Postfix Methods**: Harmony patches in this mod employ both prefix and postfix methods to adjust behaviour at critical execution points, like modifying power draw after `CompInspectStringExtra`.

- **Targeted Harmony Patches**: Each gameplay element, such as `Building_WorkTable_UsableForBillsAfterFueling`, is patched individually, allowing precise control over specific functionalities.

## Suggestions for Copilot

- **Automation of Patch Creation**: Utilize Copilot to propose boilerplate code for new Harmony patches, maintaining a consistent format with existing patches.

- **XML Definition Templates**: Generate structured XML templates for adding new building types or updating power draw settings.

- **Method Refactoring**: Suggest refactoring opportunities to streamline repetitive code patterns found in power adjustment logic.

- **Localization Support How-Tos**: Provide guidance for adding new translations or integrating community-driven language packs.

By following these instructions, Copilot will be better equipped to assist developers in extending and maintaining the "Turn It On and Off - RePowered" mod, ensuring it remains scalable and user-friendly.

This `.github/copilot-instructions.md` file provides a comprehensive blueprint for developers leveraging GitHub Copilot, emphasizing clear understanding of the mod's functionalities, coding practices, and integration techniques.

## Project Solution Guidelines
- Relevant mod XML files are included as Solution Items under the solution folder named XML, these can be read and modified from within the solution.
- Use these in-solution XML files as the primary files for reference and modification.
- The `.github/copilot-instructions.md` file is included in the solution under the `.github` solution folder, so it should be read/modified from within the solution instead of using paths outside the solution. Update this file once only, as it and the parent-path solution reference point to the same file in this workspace.
- When making functional changes in this mod, ensure the documented features stay in sync with implementation; use the in-solution `.github` copy as the primary file.
- In the solution is also a project called Assembly-CSharp, containing a read-only version of the decompiled game source, for reference and debugging purposes.
- For any new documentation, update this copilot-instructions.md file rather than creating separate documentation files.


## Hard rules (must follow)
- Do NOT run commands that modify the repo (no git commit, git apply, dotnet format) unless explicitly asked.
- Prefer minimal reads: read only the smallest code region needed (around the suspicious lines).

