# Windows interface and startup preference

The visual target is the user's existing EZ Across Control screenshots and the current WPF design system. Preserve the dark/light surfaces, brand header, Inter interface font, coral primary actions, rounded cards, and semantic status colors. Refero live research was unavailable; its bundled color and form guidance informed semantic colors and a clickable, keyboard-accessible checkbox.

| Decision | Source | Result |
| --- | --- | --- |
| Compact server controls | User's revised request and existing icon buttons | Start is a play triangle; Stop is a square. Both have 36px targets, localized accessible names and tooltips. Play is green while running; the square is red while stopped. |
| Stable card dimensions | Screenshot 2 | Main layout uses a bounded grid; advanced visibility does not consume width through an outer scrollbar. |
| Remove outer scrollbar | Screenshot 3 and user's revised request | Only the activity log has a scrolling viewport; all six diagnostics are stacked in one vertical group, with aligned labels/values, beside preferences. |
| Short version label | User request and semantic theme tokens | Localized Version label and numeric assembly version; only the number changes status color. |
| Startup preference placement | User request | Checkbox is below Language and above Version. |

Version green means a valid stable release check confirmed the installed version is current. A newer release, missing/invalid release metadata, or a failed check uses the warning color. A localized tooltip distinguishes current, available, and unverified. Existing installer download validation and explicit update acceptance remain in place.

Startup is opt-in. Checking writes only the application's quoted executable command to its own `EZAcrossControl` value in the current user's Windows Run key. Unchecking deletes that value. Initial loading only reads the registration; it does not create an entry. A write failure restores the previous checkbox state and shows a localized error. The Windows startup command length limit is enforced. No administrator privileges, task scheduler, service, or secondary preference file is required.

Windows runs that entry when the user signs in; Windows may delay startup. See [Microsoft Run and RunOnce documentation](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys). A Windows policy or an external startup manager can independently prevent launch.

Validation uses an in-memory registration fixture and HTTP release fixtures. The real WPF XAML is rendered without the MainWindow constructor, input hooks, network, ADB, registry writes, or host startup. Previews cover all 11 languages, both themes, normal/advanced layouts, short viewports, checked/unchecked preferences, and version states. A long activity history must not change the measured advanced window minimum.

Real sign-in startup and behavior on the installed application remain manual acceptance checks. This UI change does not alter connection negotiation or native mouse capture, and it does not replace the published 1.1.26 binaries.
