# Petrichor 1.0.1 visual QA

final result: passed

Scope: public and guest flows captured in `artifacts/ui-refinement/`. This result covers the visual correction round; it does not certify real-account writes or physical devices.

## Reference

The 35 approved HTML screens and `.superdesign/design-system.md` are unchanged. SHA256 fingerprints match the saved approved state. Reference PNGs were rendered at a 412×892 content viewport and inspected beside native Android captures. Android owns its status bar, cutout and navigation gesture area; these were preserved.

## Fixed P1/P2 findings

- Missing icon shortcut cards and inconsistent search controls: restored cards, rounded search field, 36dp chips and selected fills.
- Raw login controls and missing page structure: restored centered identity, tab indicators and outlined floating-label fields; inputs center vertically.
- Detail layouts and playback controls diverged from the reference: restored square artwork, centered metadata, actual Material icon mappings, thin seek track and a vertical thumb.
- Selected chips were clipped or lost their fill: fixed width, background resource precedence and scrolling to the selected end category.
- Menus lacked artwork, icons and handles: restored structured action sheets; create/choose playlist use a dialog/sheet.
- Sleep sheet had excess space and native default purple accents: constrained its height and matched radio colors.
- Multi-select retained the large playlist header and recycled rows changed indentation: replaced it with selection state and fixed bottom actions; checked row alignment after selecting100 tracks.
- Theme changes rebuilt the activity and lost navigation: appearance resources now refresh in place.

## Interaction evidence

Cold launch and update installation succeeded. Public search, program-to-podcast navigation, native play/pause, player/lyrics/queue navigation, long press, public-playlist selection, login field validation, QR generation, settings, dark mode and dynamic color were exercised. The 81 existing business checks pass. Final build log and package fingerprint are in the review directory.

## Remaining P3 / acceptance notes

- Native font rasterization and some icon contours differ from browser rendering; actual artwork, titles, counts and list lengths differ from sample data.
- Some captures belong to earlier builds of the same1.0.1 correction round. The final build additionally preserves artist artwork returned by search/collections; the final package is identified by `report.json`.
- Signed-in screens and account dialogs have been implemented and checked against source structure, but lack real-account screenshot/write acceptance. SMS receipt, account risk controls, playlist writes, comments and ARM64 physical-device behavior remain unverified.
- Podcast category shortcuts perform keyword search; FM's skip is labeled as next track. These preserve real behavior instead of implying unavailable category/dislike operations.
