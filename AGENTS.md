# RGBABY project handoff

Updated 2026-11-07. Read this before changing the app. This records the current
code and repository evidence, rather than assuming access to earlier chats.
User-facing setup and usage are in [README.md](README.md).

## What this app is

RGBABY is a Windows x64 .NET Framework WinForms lighting controller. It has four
independent channels: Redragon keyboard, Gigabyte motherboard/ARGB headers, ASUS
mouse, and Corsair RGB RAM. It is a local desktop app, with no web stack or cloud
service. Sources are compiled directly by the Windows Framework `csc.exe`;
there is no `.csproj`, NuGet dependency, or required modern .NET SDK.

The keyboard uses a verified direct HID packet. Other channels use a separate
portable OpenRGB 1.1 process over its localhost SDK, protocol 4, port 6742.
Do not replace the direct keyboard protocol with guessed packets or broaden
device matching just to make a different device appear connected.

## Latest change: live in the system tray

The user wants RGBABY tucked into the bottom-right Windows notification area
until they click it. Both a manual launch and `--startup` now start hidden.

- `Program.cs` runs `TrayApplicationContext`, not `Application.Run(form)`.
- `TrayApplicationContext.cs` owns the icon and menu. A single left click or
  **Open RGBABY** opens the existing console. Right click exposes Open and Exit.
- Close/X and minimize hide the console, retaining staged edits and the process.
  **Exit RGBABY** actually quits; an active apply finishes before exit.
- `MainForm.StartInTray` restores placement, creates a hidden top-level HWND,
  and queues initialization on the UI loop. Never require `Shown` for background
  startup: it would postpone saved lighting until the user opened the console.
- The hidden HWND still receives wake broadcasts. Minimize/reopen retains the
  pre-minimize normal/maximized state. Changing `ShowInTaskbar` can recreate the
  handle and trigger Resize, so `changingTrayVisibility` guards those transitions.
- A second launch keeps the single-instance mutex behavior and points the user
  to the existing tray icon. It does not create another controller or open the UI.
- Windows decides whether the icon is directly visible or inside its `^` overflow.
- Exiting RGBABY removes its icon and stops its wake restoration. It deliberately
  leaves the separate OpenRGB engine running, as before.

## Code map

| File | Responsibility |
|---|---|
| `src/Program.cs` | Entry point, CLI branches, per-session mutex, tray lifetime |
| `src/TrayApplicationContext.cs` | Tray icon, menu, application lifetime/cleanup |
| `src/MainForm.cs` | Console layout, staged controls, discovery, apply/save, tray visibility, wake messages |
| `src/LightingState.cs` | DeviceKind, independent color/brightness/balance, snapshots, output scaling |
| `src/Settings.cs` | Profile validation/migration, atomic save, monitor fit, HKCU startup registration |
| `src/StartupRestore.cs` | Bounded retries for saved devices, removing successful devices from pending |
| `src/ResumeRestore.cs` | Wait for other work, snapshot saved motherboard state, delay/retry after wake |
| `src/Keyboard.cs` | Windows HID enumeration/descriptor inspection, key map, guarded feature report send |
| `src/Protocol.cs` | Exact keyboard identity match and RGB packet encoding/validation |
| `src/OpenRgb.cs` | SDK parser/client, device selection, mode/LED writes, backend startup, SignalRGB guard |
| `src/Theme.cs` | Palette, custom keycap buttons, instrument panel painting |
| `src/ConsoleControls.cs` | Device strips, hex editor, color swatches, balance display |
| `src/Knob.cs`, `src/BalanceDialog.cs` | Brightness input and per-channel calibration dialog |
| `setup-ram.ps1` | One-time elevated PawnIO and scheduled backend setup |
| `research/k636-protocol.md`, `research/k636-keymap.json` | Keyboard evidence and 94-key mapping |

## Supported hardware and how it works

| Channel | Identity and behavior |
|---|---|
| Keyboard | Redragon K636CLO-RGB, USB `258A:0049`; usage page `FF00`, usage 1, feature report ID 8, 382 bytes, 381 eight-bit values. Require exactly one matching interface. |
| Motherboard | SDK type 0, exact name `X870 GAMING WIFI6`, location contains `VID_048D&PID_5711`. Hardware **Static** mode covers board and ARGB headers. |
| Mouse | SDK type 6, exact name `ASUS ROG Gladius II Origin`, location contains `VID_0B05&PID_1877`. **Direct** mode covers logo, wheel and base. |
| RAM | All SDK type-1 controllers with vendor Corsair and distinct nonempty locations. Prefer **Direct**, fall back to **Static**. One shared RAM channel controls all detected Corsair sticks. |

Keyboard packet header is `08 0A 7A 01`; remaining bytes are 126 RGB slots.
The embedded map identifies 94 keys. All-key mode fills mapped slots only;
single-key mode leaves every other slot black. Do not confuse the vendor matrix
index with its LED slot. Basic primary colors were checked on hardware according
to the research notes; full individual-key mapping, exact color fidelity and
firmware persistence are not fully verified.

For motherboard zones named `ARGB_V2_*`, an empty resizable zone is assigned
one logical LED to enable hardware static broadcast. This is not a physical LED
count and must not be treated as support for per-LED strip effects.

SDK applies rediscover controllers, re-read the selected identity before writes,
select the supported mode, and send LED colors when the mode needs them.
Never cache controller indices across scans/connections. A confirming SDK
round trip means commands were consumed, not that physical color was measured.
RAM can partially change if a later stick fails; the shared RAM profile is saved
only if the channel apply returns successfully.

SignalRGB is checked before backend startup and writes. The app refuses updates
while its lighting process runs; it never automatically kills competing apps.
Other controllers may also compete even though they are not explicitly detected.

## Profiles, color, and restore semantics

- Settings live at `%LOCALAPPDATA%\RGBABY\settings.json`; atomic replacement keeps
  `settings.json.bak`. There is no automatic backup recovery. A corrupt profile
  is preserved on close until the user successfully applies and saves a new one.
- Schema version 2 has four device profiles. Version 1 migrates by appending
  unsaved RAM. `HasApplied` controls whether automatic restore may write a device.
- Defaults: cyan, 50% brightness, RGB balance 100/100/100; motherboard green is
  65 and mouse green is 70. Existing saved values take precedence.
- Output channel = `(channel * brightness * balance + 5000) / 10000`, integer
  arithmetic. The keyboard packet uses the same scale. SDK mode hardware
  brightness is set to full because intensity is already encoded into RGB.
- Sync Colors copies only Keyboard's color; Sync Brightness copies only its
  brightness. Neither changes calibration, nor sends/saves until Apply.
- Only successful applies update that device's saved state. Unavailable/failed
  channels keep previous saved state. Staged changes are not saved on close.
- Lights Off sends and saves brightness zero but keeps the current staged
  controls so Apply can immediately restore them. Next launch loads saved zero.
- Window bounds/maximized state are saved on Apply and hide/close, fitted to
  available monitor work areas. The console scrolls on small displays.

Two independent startup mechanisms exist:

1. The console checkbox registers the quoted executable plus `--startup` at
   `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `RGBABY`.
   It is per-user and needs no elevation. Manual launch discovers devices and
   loads controls without applying saved lighting. `--startup` restores applied
   profiles in the background, up to six discovery attempts with two-second gaps;
   successful devices are removed from pending so they are not repeatedly sent.
2. Optional RAM setup creates `RGBABY OpenRGB <user SID>` in Task Scheduler,
   interactive/highest privilege at sign-in. Only this backend needs elevation.
   The console can request that task when the SDK is unavailable; otherwise it
   starts the portable engine hidden itself.

`OpenRgbBackend` tries `dist/backend/OpenRGB.exe` then the repository runtime
fallback. It uses `--server --server-host 127.0.0.1 --server-port 6742
--startminimized --config <backend>/rgbaby-config`. Starting the engine can
initialize hardware and affect an existing effect even without an RGBABY Apply.

RAM requires PawnIO and elevated SMBus access for this bundled engine. The setup
script checks the official installer's Authenticode signature/publisher, installs
if needed, registers the per-user task and restarts only this project's engine.
Do not rerun installation or task registration as an incidental code check.
Moving the folder requires updating both the console startup entry and backend
task; README has setup and removal instructions.

Wake restoration listens only for `WM_POWERBROADCAST` / `PBT_APMRESUMEAUTOMATIC`
(`0x0218` / `0x12`) to avoid duplicate wake events. It waits for existing work,
snapshots the last successfully saved motherboard profile, waits two seconds,
then uses bounded retries. Saved blackouts count. Keyboard, mouse, RAM and
unsaved staged controls are intentionally untouched. Tray residence keeps this
behavior alive; Exit stops it.

## Theme and UI decisions

The current theme is the cream Studio console, not the earlier CRT branding
still present in one SDK client-name string. Four instrument panels use cream
keycap buttons, rotary knobs, restrained orange actions, and subtle shading.
The original generated artwork is not displayed inside the console; its ICO is
embedded for the executable/window/tray. No runtime light/dark theme picker exists.

Palette in `Theme.cs`: background `#E3DED2`, panel `#F5F1E8`, line `#B6AFA0`,
text `#36332C`, muted `#706A5E`, accent `#A74D26`. Segoe UI is used for controls,
Arial for the wordmark, Consolas for hex entry. Layout is DPI-scaled with a
scrolling viewport; knobs support drag, focused wheel, arrows, Home/End and
Page Up/Down. Change shared theme primitives first when adjusting the look.

## Adding hardware

Prefer extending the SDK path for a device OpenRGB already supports. Capture
real read-only discovery evidence and confirm type/name/vendor/location, modes,
zones and LED counts. Do not assume a shared USB vendor ID means compatibility.

For an alternative model in an existing channel, add an explicit verified
identity/mode strategy in `OpenRgbCodec`/`OpenRgbClient`, update UI labels and
tests, and retain ambiguity rejection. The current architecture supports one
keyboard/board/mouse channel each, not arbitrary independent instances. Multiple
different devices or independent sticks need stable identity-based profiles and
UI selection; do not silently pick the first device returned.

For a new independent channel:

1. Append to `DeviceKind` without renumbering existing serialized enum values.
2. Add defaults/profile migration in `Settings.cs`; its validation currently
   enforces exact version/device counts. Advance the schema with a migration
   that preserves old settings and marks the new channel `HasApplied=false`.
3. Add discovery and safe apply routing; expand SDK selection or introduce an
   isolated, evidence-backed transport. Keep device identity revalidation and
   the ownership guard. Never send firmware, flash, or guessed profile commands.
4. Add a `DeviceStrip` in `MainForm`, connection handling and apply routing.
   Update model labels, channel count/edition text and layout for the new width.
   Review hardcoded Keyboard references (`strips[0]`) and keyboard-only target.
5. Check Sync, Apply All, Lights Off, successful-only persistence and startup
   restore. Wake currently restores only motherboard: expanding that is a
   separate behavior decision, not automatic for new channels.
6. Add tests for identity ambiguity, wrong/truncated responses, modes, color
   encoding, profile migration and missing/late devices. Test UI overflow too.
   Then verify physical output explicitly and record what remains unverified.

For a new direct-HID device, obtain descriptor/report and packet evidence first.
Keep its codec and key map separate from the K636 implementation; enforce exact
report length, ID, usage and bounds before sending. See the research notes.

## Build, tests, and diagnostics

Run from the repository root in PowerShell:

```powershell
./test.ps1
./build.ps1
powershell -NoProfile -File tests/TrayUiTests.ps1
powershell -NoProfile -File tests/StudioUiTests.ps1
powershell -NoProfile -File tests/BalanceUiTests.ps1
powershell -NoProfile -File tests/ProfileUiTests.ps1
```

Use separate PowerShell processes for UI suites so loaded assemblies cannot
lock/stale-cache a later build. UI checks may briefly show test windows/icons.
They do not apply hardware colors. Tray tests cover hidden launch/handle,
click/open, close preserving edits, minimize/maximize restore, icon disposal,
and deferred exit during an active apply. `test.ps1` runs protocol, lighting SDK,
settings/startup and resume suites; it does not run the UI suites automatically.

If RGBABY is running, Exit through its tray menu before rebuilding its filename,
or use `./build.ps1 -OutputName RGBABY-candidate.exe` and pass that absolute path
as `-AssemblyPath` to UI scripts. Do not kill a process with unsaved staged edits
just to free the output executable. The build copies the portable backend when
available; a fresh checkout needs the OpenRGB runtime downloaded separately.

CLI: `--render <absolute.png>` creates a hardware/profile-free preview.
`--diagnose <absolute.json>` reads HID and an already-running SDK server without
applying lighting, but currently requires board and mouse selection to succeed.
It is not yet a universal discovery tool for unrelated machines. Errors from
these output-file commands go to `<output>.error.txt`. `--startup` writes saved
lighting, so do not use it as a harmless smoke test. Wait on the launched process
itself, not a process tree that can include the persistent backend.

Do not commit generated `build/`, `dist/`, runtime binaries, local profiles,
installer files or machine-specific diagnostics. RGBABY is MIT; OpenRGB is a
separate GPL-2.0 program. Keep `THIRD_PARTY_NOTICES.md` and licenses with releases.
