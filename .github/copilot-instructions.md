# GitHub Copilot Instructions for RimWorld Modding Project: "Turn It On and Off - RePowered"

## Mod Overview and Purpose
"Turn It On and Off - RePowered" is a RimWorld mod that combines the best aspects of two existing mods: "Turn It On and Off" and "RePower." This project aims to enhance the realism of power consumption in RimWorld by allowing electrical devices to turn off when not in use, and by configuring more realistic power requirements through mod options. The mod maintains the functionality of using RePower patches for custom settings and ensures compatibility with RimWorld's vanilla mechanics.

## Key Features and Systems
- **Power Draw Adjustments**: Configure a multiplier for vanilla power-draw to achieve more realistic levels.
- **Increased Compatibility**: Supports turrets, autodoors, Doors Expanded, and Project RimFactory Revived.
- **Workbenches Power Management**: Blocks work on benches when power is insufficient, preserving continuity with those inheriting vanilla class methods.
- **Language Support**: Includes French and Russian translations.
- **Dynamic Changes**: Settings can be applied without restarting the game.
- **Safe Installation/Removal**: Can be added or removed from current saves without impacting performance more than the original mods.

## Coding Patterns and Conventions
- **Static Classes**: Utilize `public static class` for utility and component classes to organize code related to specific game mechanics without relying on object instances.
- **Inheritance and Extension**: Leverage polymorphism for extending or altering the functionality of vanilla or modded classes, such as `GameComponent`, `ModSettings`, and `Def`.
- **Method Visibility**: Design methods with public accessibility in utility classes to enable easy integration within other scripts or when patching.
- **Namespaces and Modules**: Use clear namespaces to segment and manage different aspects of the project, ensuring easy identification of system-specific functionalities.

## XML Integration
- **Configuration and Customization**: XML files should be utilized to customize and define mod options, ensuring they are readable with RimWorld’s Def structure.
- **Localization**: Manage translations within XML files to support multiple languages, maintaining the mod's multilingual capabilities.

## Harmony Patching
- **Dynamic Runtime Alterations**: Use Harmony to patch existing methods in the game, allowing you to dynamically alter game behavior without modifying core game code directly.
- **Targeted Patching**: Focus patches on key methods that govern power management and workload allowance to integrate features such as automatic power adjustments and active/inactive state management of devices.
- **Collaboration**: Patches can share functionalities with related mods, like RePower, enhancing modularity and cross-compatibility.

## Suggestions for Copilot
- **Code Completion**: Use Copilot to assist with method generation for handling dynamic configurations in `ModSettings` classes, reducing repetitive coding tasks.
- **XML Generation**: Implement functionality for generating XML templates for new device or feature support, leveraging Copilot to ensure consistency.
- **Patch Creation**: Facilitate the creation of Harmony patches by suggesting applicable entry and exit points in the base game code.
- **Refactoring Assistance**: Use Copilot to help refactor legacy code for better performance and readability by suggesting optimized patterns and design structures.
- **Documentation**: Utilize Copilot to aid in creating documentation comments within your codebase, improving maintainability and comprehension for collaborators or future review.

Credits:
- Ryder, Thom Blair III, Fluffy, Xen, EbonJaeger, NubsPixel, Texel for prior work on the "Turn It On and Off" and "RePower" mods.


This `.github/copilot-instructions.md` document offers developers comprehensive guidance on leveraging GitHub Copilot in a RimWorld mod project while maintaining the key features and systems refined through prior development efforts.
