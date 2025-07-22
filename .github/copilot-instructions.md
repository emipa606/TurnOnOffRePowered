# GitHub Copilot Instructions for RimWorld Modding Project

## Mod Overview and Purpose

This RimWorld mod, titled "Turn It On and Off," enhances the gameplay by allowing players to have more control over powered devices in the colony. The mod primarily focuses on managing power consumption by enabling and disabling various powered devices such as turrets, work tables, hydroponics basins, and doors based on certain conditions. This not only optimizes power usage but also introduces strategic considerations for energy management within the game.

## Key Features and Systems

- **Dynamic Power Management**: The mod includes systems that can evaluate and adjust the power state of various devices automatically, potentially reducing unnecessary power consumption.

- **Scheduled Building Management**: Players can set schedules for buildings that should be turned on or off, ensuring that power is used efficiently when needed.

- **Turret Management**: Turrets can be evaluated based on criteria and turned off when not in use to save power.

- **Hydroponics Basin Evaluation**: Adjusts power states of hydroponics basins, ensuring they are active when required for plant growth and saving power otherwise.

## Coding Patterns and Conventions

- **Static Classes**: The mod makes extensive use of static classes, such as `Building_WorkTable_UsableForBillsAfterFueling` and `Building_WorkTable_UsedThisTick`, to encapsulate functionality that does not maintain state.

- **Consistent Naming Conventions**: All classes and methods follow a clear and concise naming system, making it intuitive to understand their purpose, such as `EvalHydroponicsBasins` and `UpdateRepowerDefs`.

- **Separation of Concerns**: Different responsibilities, such as evaluation of devices or updating definitions, are separated into distinct methods to maintain clean and readable code.

## XML Integration

While the summary does not detail XML content, RimWorld mods often use XML to define game data like ThingDefs and WorkGivers. Ensure that your mod correctly references any necessary XML files to define new items or modify existing game data. Any XML files used should adhere to RimWorld's mod structure for easy compatibility and updates.

## Harmony Patching

Harmony is used for patching methods to extend or modify game behavior without modifying the original codebase. In this mod, files like `ThingWithComps_GetInspectString_Patch` suggest that patches are used to modify or extend the functionalities of RimWorld components like `CompPowerTrader`.

- **Suggested Harmony Patterns**:
  - **Prefix and Postfix Methods**: Use these to intercept and augment base game methods.
  - **Transpiler**: For more complex modifications where reordering IL codes is necessary.

## Suggestions for Copilot

- **Method Stubs and Completion**: Use Copilot to suggest method stubs and provide auto-completions for frequent tasks such as power evaluations and building schedules.
  
- **Pattern Recognition**: Leverage Copilot's pattern recognition to replicate similar logic across different classes and methods, such as power management routines for various building types.

- **Suggest XML Definitions**: Encourage Copilot to suggest XML snippets for defining new ThingDefs or modifying existing ones based on the context within the C# code.

- **Harmony Practices**: Use Copilot for writing concise Harmony patch methods, ensuring to follow RimWorld's best practices in modding.

This documentation should equip developers with the necessary information to understand and extend the "Turn It On and Off" mod, integrating further enhancements using C# and XML through the power of GitHub Copilot.
