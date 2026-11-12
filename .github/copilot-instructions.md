# GitHub Copilot Instructions for Turn It On and Off - RePowered

## Mod Overview and Purpose

**Turn It On and Off - RePowered** (TurnOnOffRePowered) is a RimWorld mod that combines the functionality of the **Turn It On and Off** and **RePower** mods to enhance the realism of power consumption in the game. The mod introduces mod options that allow players to define a multiplier for the vanilla power draw, providing a more realistic power management experience. It supports a wide range of buildings and systems, including turrets, autodoors, and specialized workbenches from popular RimWorld mods like **Doors Expanded** and **Project RimFactory Revived**.

## Key Features and Systems

- **Realistic Power Consumption**: Adjusts vanilla power draw using multipliers to simulate real-world power usage.
- **Mod Options**: Provides settings to enable/disable features, set power multipliers, and block work if power is low.
- **Wide Compatibility**: Supports numerous building types from vanilla RimWorld and popular mods.
- **Dynamic Power Management**: Continuously evaluates building states and power usage, updating power states in real-time.
- **Translation Support**: Includes French and Russian translations for a wider audience.

## Coding Patterns and Conventions

### Directory Structure
- **Source/**: Contains the main C# source files.
- **Defs/**: Contains XML definition files.

### C# Source Files
- **TurnItOnUtility.cs**: Contains utility functions and classes for managing power usage and building states.
- **TurnOnOffMod.cs**: Manages mod settings and configuration.
- **TurnOnOffGameComponent.cs**: Handles game component ticks and updates.
- **RePowerDef.cs** and **TurnItOnandOffDef.cs**: Define custom mod-specific definitions.

### Coding Style
- Use PascalCase for class names and methods.
- Use camelCase for method parameters and local variables.
- Use `//` for single-line comments and `/* */` for multi-line comments.
- Use `using` directives for necessary namespaces at the top of each file.
- Follow RimWorld's naming conventions for consistency.

### XML Integration
- **Defs/**: Contains XML files that define mod-specific data and configurations.
- **UpdateInfo.xml**: Provides update information and changelog entries.

### Harmony Patching
- Harmony is used to patch vanilla RimWorld methods to integrate custom power management logic.
- Patches are defined in the `Defs/` folder using XML files.
- Use `HarmonyMethod` attributes to specify patch methods.

## Suggestions for Copilot

1. **Start with a clear understanding of the mod's purpose and features**. This will help Copilot generate more relevant and accurate suggestions.
2. **Use specific code examples** when requesting help. Providing a snippet of code or a function signature can lead to more targeted assistance.
3. **Focus on specific areas** of the mod, such as power management, building support, or mod options, to get more targeted help.
4. **Ask for explanations** of complex code or logic. Copilot can provide insights and clarify how certain parts of the code work.
5. **Review and test suggestions** thoroughly before integrating them into your mod. Ensure they align with your coding standards and mod's functionality.

## Example Usage

### Requesting a Harmony Patch

If you need help creating a Harmony patch to add support for a new building type, you might request something like:

// Create a Harmony patch for a new building type called "CustomBuilding"
[HarmonyPatch(typeof(Building), "SomeMethod")]
public static class CustomBuildingPatch
{
    [HarmonyPrefix]
    public static bool Prefix(Building __instance)
    {
        // Custom logic here
        return true;
    }
}


### Requesting a Utility Function

If you need a utility function to check if a building is expected to be active, you might request:

// Utility function to check if a building is expected to be active
public static bool IsBuildingExpectedActive(Building building)
{
    // Implementation logic here
    return true;
}


## Conclusion

By following these guidelines, you can effectively leverage GitHub Copilot to enhance your modding experience. Whether you need help with Harmony patching, XML integration, or coding patterns, Copilot can provide valuable assistance to streamline your development process.

Happy modding!

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
- When mentioning SonarQube issues, automatically use the SonarQube MCP service to fetch and address issues instead of making inferred fixes without querying SonarQube first.
- When mentioning the rimworld log, automatically use the Rimworld MCP service to fetch the log.

