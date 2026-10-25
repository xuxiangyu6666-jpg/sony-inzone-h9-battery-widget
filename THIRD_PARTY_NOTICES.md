# Protocol references and attribution

## InZoneH9Tray

- Author/project: janit88 / InZoneH9Tray
- Repository: https://github.com/janit88/InZoneH9Tray
- Referenced revision: `4780303dbee4bf1a86acf37c2cbc96b1774eee00`
- Referenced materials: serial setup, initialization/query commands and battery payload interpretation in `inzone_tray.py`.
- The upstream README declares MIT licensing. That referenced snapshot does not contain a separate LICENSE file.

The serial-reading approach was adapted into C# with additional validation of event source/type, checksum and percentage range. The upstream Python application, its dependencies and its binaries are not bundled. Credit for the H9 serial protocol findings belongs to the upstream project.

## openinzone

- Author/project: penguinwokrs / openinzone
- Repository: https://github.com/penguinwokrs/openinzone
- Referenced revision: `167108990aa674a57a5ba8a6d288d7d4383253b8`
- Reference: `docs/PROTOCOL.md` and the documented HCI packet layout/checksum behavior.
- Upstream project license: GPL-3.0-only.

This project uses the published protocol information as an interoperability reference. It does not link, vendor or distribute openinzone source, libraries or binaries. The HID implementation in openinzone is not used by this serial-based widget.

## Sony

Sony and INZONE are trademarks of their respective owner. This project is independent and unofficial. It includes no Sony software, images, firmware or INZONE Hub resources.
