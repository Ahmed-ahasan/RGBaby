# K636CLO-RGB lighting protocol

The keyboard uses a 382-byte HID feature report containing 126 RGB slots.
The layout in `k636-keymap.json` maps 94 keys to distinct slots.

## Interface selection

Match all of these properties when enumerating devices:

| Property | Value |
|---|---|
| USB vendor/product | `258A:0049` |
| HID usage page | `0xFF00` |
| HID usage | `0x0001` |
| Feature report size, including ID | 382 bytes |
| Feature report ID | `0x08` |
| Descriptor value shape | 381 eight-bit values |
| Interface/collection | `MI_01`, `COL08` |

Discover Windows device paths at runtime. Other collections share the same
vendor/product and usage values, so report size and ID must also match.

## RGB report

| Byte offset | Content |
|---|---|
| 0 | `08`, HID report ID |
| 1 | `0A`, lighting command |
| 2-3 | `7A 01`, 378-byte payload length in little-endian form |
| 4-381 | 126 consecutive three-byte RGB slots |

For slot `s`, red is at `4 + 3*s`, green at `5 + 3*s`, and blue at `6 + 3*s`.
Unused slots are zero. Slot order differs from physical key order.

The mapping was traced from the vendor configuration and RGB sender. Its
zero-based field 7 is the LED slot; field 8 is a separate matrix index.
Basic primary-color output was checked on hardware. Individual key mapping,
exact color fidelity and firmware persistence have not been fully verified.
A black report requests black; it does not restore a previous effect.
