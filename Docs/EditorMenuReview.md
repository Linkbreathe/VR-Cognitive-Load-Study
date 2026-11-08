# Evidence VR menu cleanup

2026-11-07 · `editor-menu-v1` · Editor operation only.

## Delivered menu

```text
Evidence VR
  Open Web Console
  Signal Sources
  Advanced
    Use Web Console Workflow
    Quest Link Settings
      Use Oculus for Editor Preview
      Use System Default for Editor Preview
    Eye Tracking Debug
      Target Highlight
      Eye Ray
    Validate Setup
    Build Quest App
```

The top level now has two frequent actions and one Advanced submenu. Nine executable commands replace the previous 22. Eye-debug Enable/Disable pairs are checkmarked toggles. The new menu facade delegates to the original tools; it does not change model rules, task timing, device requirements, session allocation or runtime presentation.

Open Web Console opens `/tests` at the existing configured controller URL, defaulting to the normal local service URL when there is no Editor override. It does not launch or restart Python. Signal Sources opens the existing LSL selector. Use Web Console Workflow calls the existing operator-managed setup, so it is a configuration action rather than a session start.

Target Highlight retains the existing next-session restriction. Eye Ray retains live engineering-session control. Validation callbacks reflect the saved or live debug state and disable unavailable actions; participant restrictions still come from the original methods. Quest Link and workflow changes are unavailable during Play Mode transitions. Validate Setup delegates to the existing software validation runner; it is not headset acceptance. Build Quest App delegates to the existing current Stage 2 build method, including its existing build-support setup.

## Preserved maintenance entry points

Only the old MenuItem attributes were removed from existing scripts. Original method bodies and signatures remain byte-for-byte identical, including:

- `AdaptiveTrainingSetup.EnablePersonalized`, `EnableEditor`, `DisableEditor`, `Configure` and both enable/disable debug pairs.
- `LayerABSetup.Choose`, `Install` and `Restore`.
- `TaskPlatformBuilder` scene, Android, serialized-input and original APK tools.
- `LabEnvironmentUpgrade` and `LabVisualUpgrade` environment tools.
- Existing operator setup, LSL selector, Quest Link setup and validation methods.

Older documentation with Stage 2 or Layer A-B menu paths describes the historical interface. Maintainers can still call those methods through Editor tooling. Reintroducing those tools in the ordinary operator menu is not necessary for the Web Console workflow.

## Validation and evidence

- Unity 6000.3.27f2 recompiled successfully: zero errors and zero warnings.
- The running Editor's menu listing contains exactly the nine expected Evidence VR command paths and none of the previous 22 paths.
- Compared every pre-existing Editor C# source against the restore archive after removing only MenuItem lines; all matched.
- Read-only Editor checks resolved the Console URL to `http://127.0.0.1:8765/tests`. Both engineering debug toggles were available and unchecked, matching the current saved configuration. Play Mode was stopped.
- No debug setting, experiment configuration, Quest runtime selection or scene was changed by verification. No APK build or physical device test was performed.

[Verification record](../Evidence/MenuCleanup-20261107/verification.json) · [Editor menu listing](../Evidence/MenuCleanup-20261107/registered-menus.json) · [Source restore point](../Evidence/MenuCleanup-20261107/source-before.zip).
