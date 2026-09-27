# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.1] - 2026-09-27

### Fixed

- Shipped the missing meta file for `LICENSE.md`. Without it Unity logged "has no meta file, but it's in an immutable folder" for packages installed from a git URL.

## [2.0.0] - 2026-09-22

### Added

- Scene switcher: a searchable popup on Ctrl+Shift+O (Enter opens, Ctrl+Enter opens additively, Shift+Enter opens and plays).
- Play button on every row: opens the scene alone and enters play mode.
- Row context menu: set active, save, close, open the whole group, show in the Project window.
- Preferences page (Edit > Preferences > Scene Operator): rebindable shortcuts, window and switcher options, and every color of the tool, per editor theme.
- Rows show the state of each scene: open, active, unsaved changes, and scene assets missing from the project.

### Changed

- The window is one flat list: groups are separated by a divider instead of collapsible headers, and their names are hidden by default.
- A click on a row opens the scene, Ctrl+click opens it additively; the wide per-row and per-group load buttons are gone.
- The package is editor-only; the Runtime assembly was removed. Existing collection assets keep working.

### Fixed

- Closing a scene no longer discards unsaved changes when the save dialog is cancelled, and the dialog is shown once instead of twice.
- Scenes are identified by asset path instead of by name, so scenes sharing a name no longer report a wrong state.
- The selected collection is remembered per project by asset GUID: it survives restarts, renames and moves.
- Collections are tracked through an asset postprocessor; creating, deleting, renaming or moving one updates the window immediately.
- Row callbacks are no longer stacked on every refresh.


## [1.0.0] - 2023-11-02

### Added

- Scene Operator

## [1.0.2] - 2023-12-16

### Changed

Updated assembly
