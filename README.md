# RGBABY 0.5 — Studio console

A Windows lighting controller with cream-white keycaps, rotary brightness knobs, and independent keyboard, motherboard, mouse and Corsair RAM channels.

## Use

Open `dist\RGBABY.exe`. Keep the `backend` folder beside it. The bundled portable OpenRGB engine handles the motherboard, mouse and RAM and starts locally when required. Exit SignalRGB and disable its Windows startup before using RGBABY: competing controllers can overwrite colors and cause flashing. RGBABY detects the running SignalRGB lighting process and refuses lighting updates until it exits; it does not terminate other applications automatically.

- **Color:** enter a hex value, use COLOR, or press a colored keycap.
- **Brightness knob:** drag vertically, focus and scroll, or use arrow keys. Home/End select 0/100; Page Up/Down change by 10.
- **Sync Colors:** copies Keyboard's color to every device, keeping brightness and balance.
- **Sync Brightness:** copies Keyboard's brightness to every device, keeping colors and balance.
- **Apply & Save:** sends one device's staged settings and remembers them. Apply & Save All sends all available devices. Sync buttons stage changes; click Apply to send them.
- **Balance:** independent red/green/blue attenuation for every device. Mouse defaults to 100/70/100; motherboard starts at 100/65/100. Calibration affects all colors on that device and may need adjustment for your hardware.
- **Keyboard target:** choose all 94 keys or one key. A single-key apply turns other keys off. The selected target is saved too.
- **Lights Off:** sends and saves zero brightness for connected devices. Staged controls remain unchanged until next launch so Apply can restore them immediately. On the next launch, saved zero brightness is loaded.

## Remembered settings and startup

Successful applies save color, brightness, calibration and keyboard target to `%LOCALAPPDATA%\RGBABY\settings.json`. Unapplied edits are not saved. Failed/missing devices keep their last successful saved state. Atomic updates retain the previous file as `settings.json.bak`. Window position, size and maximized state are saved on Apply or close and restored within an available monitor. Small displays can scroll the console.

Enable **Start with Windows and restore saved lighting** to register RGBABY for the current user's Windows sign-in. Keyboard, motherboard and mouse need no administrator rights; RAM requires the one-time setup below. First apply and save the lighting you want. At sign-in, `--startup` loads the saved profile, starts the local engine, waits for discovery, and restores saved devices. Late or failed devices get bounded retries; already restored devices are not repeatedly written. If a device remains unavailable, reconnect it, Rescan and Apply. Ordinary manual launches load the saved controls without automatically applying them.

The checkbox controls only RGBABY's entry under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Turning it off removes the matching entry. Keep the executable at its registered location; if moving it, turn startup off first, move the whole folder, then enable startup from the new location.

While RGBABY is open, waking from sleep or hibernation restores the motherboard's last successfully saved lighting, including a saved blackout. It waits briefly for USB/engine recovery and retries unavailable devices. Keyboard, mouse and RAM settings and any staged edits are left alone. Closing RGBABY also stops wake restoration.

Starting OpenRGB initializes hardware and can affect an existing effect. RGBABY itself applies staged colors only after Apply, or automatically during the explicitly enabled startup restore. The engine remains running after the console closes. Close other RGB controllers if they overwrite colors.

## Hardware

| Device | Transport |
|---|---|
| Redragon K636CLO-RGB, USB 258A:0049 | Verified direct HID, report 8 / 382 bytes, 94 keys |
| Gigabyte X870 GAMING WIFI6, USB 048D:5711 | OpenRGB hardware Static effect, board and ARGB headers |
| ASUS ROG Gladius II Origin, USB 0B05:1877 | OpenRGB Direct mode, logo/wheel/base |
| Corsair Vengeance RGB memory | OpenRGB DRAM controllers, all detected Corsair sticks; Direct or Static |

For motherboard static color, empty ARGB software zones are enabled with one logical LED. The controller broadcasts the static effect across the header; this does not claim a physical LED count. No per-LED fan effects or firmware/profile commands are offered. Successful sends do not measure the resulting physical color.

## Corsair RAM setup on Windows

OpenRGB 1.1 requires the signed [PawnIO driver](https://pawnio.eu) and administrator access for SMBus memory lighting. An engine log saying `Could not open PawnIO` means the memory cannot be discovered. See the [OpenRGB release requirements](https://openrgb.org/releases.html).

Download the official installer linked from pawnio.eu to `build\PawnIO_setup.exe`. Build RGBABY, then run `setup-ram.ps1` once from an administrator PowerShell under your normal Windows account. The script verifies the installer signature, installs the official driver if missing, and creates a per-user `RGBABY OpenRGB <user SID>` scheduled task running the bundled engine with administrator rights at sign-in. It restarts only this project's portable engine. It does not restart Windows; if the installer requires a reboot it reports that and stops. RGBABY's console stays unelevated and can start the configured backend task when needed.

After setup, Rescan in RGBABY, set the RAM color and Apply & Save. Existing profiles migrate without changing saved lighting; RAM stays unsaved until its first successful apply. RAM support has automated SDK tests; physical compatibility still depends on OpenRGB detecting the modules. To undo elevated backend startup, remove that task in Windows Task Scheduler. PawnIO can be uninstalled from Windows Installed Apps if no other software needs it. Keep the project at the same location, or remove the old task and run setup again after moving it.

## Build and checks

Run `./test.ps1`, then `./build.ps1` in PowerShell. If an executable is open, use `./build.ps1 -OutputName RGBABY-candidate.exe`. The Windows .NET Framework compiler is used; no SDK installation is necessary. The build embeds the ICO and key map, and copies the portable runtime into `dist\backend`. The original image stays in `Images` but is not embedded or displayed.

For a fresh source checkout, download OpenRGB 1.1 Windows 64-bit portable from the official release linked below and extract it so the executable is at `runtime\OpenRGB\OpenRGB Windows 64-bit\OpenRGB.exe` before building. The repository contains no portable backend or PawnIO installer. Hardware-free tests and compilation work without those downloads; lighting control needs the backend.

No-hardware UI checks: `tests\StudioUiTests.ps1 -AssemblyPath <candidate.exe>` and `tests\BalanceUiTests.ps1 -AssemblyPath <candidate.exe>`. Tests cover packet selection/encoding, independent sync and trim, profile validation/atomic replacement, visible window placement, startup quoting, late-device retry, knob input and small-window access.

CLI: `--render <absolute.png>` renders without hardware or saved-profile access. `--diagnose <absolute.json>` reads the connected hardware and an already-running OpenRGB server without applying colors. `--startup` restores saved lighting and therefore writes to hardware. Use a process object's `WaitForExit()` rather than waiting for its entire process tree. Diagnostics write errors beside the output as `.error.txt`.

## Third-party engine

OpenRGB 1.1 portable is from the [official release](https://codeberg.org/OpenRGB/OpenRGB/releases/tag/release_1.1), commit `8e16de2`. It is GPL-2.0 licensed; [corresponding source](https://codeberg.org/OpenRGB/OpenRGB/src/tag/release_1.1) is available from the project. RGBABY communicates with this separate process through the [local SDK](https://openrgb.org/sdk.html), protocol 4. Keep the bundled engine even if uninstalling the separately installed OpenRGB application. Keyboard evidence is in `research\k636-protocol.md`.

Original RGBABY code is [MIT-licensed](LICENSE). See [third-party notices](THIRD_PARTY_NOTICES.md) for the separate engine and driver.
