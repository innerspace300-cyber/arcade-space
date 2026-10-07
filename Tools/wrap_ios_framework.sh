#!/bin/bash
# wrap_ios_framework.sh — wraps the libretro-built spatial_libretro_ios.dylib
# into a minimal SpatialEmulatorCore.framework bundle Unity's iOS plugin
# importer can auto-embed (Unity does not auto-link a bare .dylib dropped
# into Assets/Plugins/iOS, but it does auto-embed a proper .framework).
#
# Usage: ./wrap_ios_framework.sh [dylib_path] [output_dir]
#   dylib_path  defaults to ../libretro-mame/spatial_libretro_ios.dylib
#   output_dir  defaults to ../../SPATIAL EMULATOR/Assets/Plugins/iOS
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DYLIB="${1:-$SCRIPT_DIR/../libretro-mame/spatial_libretro_ios.dylib}"
OUT_DIR="${2:-$SCRIPT_DIR/../../SPATIAL EMULATOR/Assets/Plugins/iOS}"
FW_NAME="SpatialEmulatorCore"
FW_DIR="$OUT_DIR/$FW_NAME.framework"

if [ ! -f "$DYLIB" ]; then
	echo "error: dylib not found at $DYLIB" >&2
	exit 1
fi

rm -rf "$FW_DIR"
mkdir -p "$FW_DIR"

# The framework's main binary must be named exactly like the bundle
# (minus .framework) with no extension - that's what makes it loadable
# as a framework rather than a loose dylib, and what DllImport("SpatialEmulatorCore")
# resolves against once Unity embeds it.
cp "$DYLIB" "$FW_DIR/$FW_NAME"

# install_name must match how the binary will be loaded once embedded
# (@rpath-relative, inside its own framework directory), or dyld can't
# find it at runtime even though the file is physically present.
install_name_tool -id "@rpath/$FW_NAME.framework/$FW_NAME" "$FW_DIR/$FW_NAME"

cat > "$FW_DIR/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleDevelopmentRegion</key>
	<string>en</string>
	<key>CFBundleExecutable</key>
	<string>$FW_NAME</string>
	<key>CFBundleIdentifier</key>
	<string>com.spatialemulator.core</string>
	<key>CFBundleInfoDictionaryVersion</key>
	<string>6.0</string>
	<key>CFBundleName</key>
	<string>$FW_NAME</string>
	<key>CFBundlePackageType</key>
	<string>FMWK</string>
	<key>CFBundleShortVersionString</key>
	<string>1.0</string>
	<key>CFBundleVersion</key>
	<string>1</string>
	<key>CFBundleSupportedPlatforms</key>
	<array>
		<string>iPhoneOS</string>
	</array>
	<key>MinimumOSVersion</key>
	<string>12.0</string>
</dict>
</plist>
PLIST

echo "wrapped -> $FW_DIR"
file "$FW_DIR/$FW_NAME"
otool -L "$FW_DIR/$FW_NAME" | head -3
