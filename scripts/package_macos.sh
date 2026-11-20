#!/usr/bin/env bash
# 从已有发布产物打包 Kudo macOS .app 与 dmg。
# 发布由工作流（dotnet publish，scripts/publish.ps1）完成，本脚本只做 bundle / dmg / sha256。
# 用法：./package_macos.sh osx-x64|osx-arm64|all
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_NAME="${APP_NAME:-Kudo}"
EXECUTABLE_NAME="${EXECUTABLE_NAME:-Kudo.Demo.Desktop}"
BUNDLE_ID="${BUNDLE_ID:-com.dotnet9.kudo}"
ICON_SOURCE="${ICON_SOURCE:-$ROOT_DIR/logo.ico}"
PUBLISH_ROOT="${PUBLISH_ROOT:-$ROOT_DIR/artifacts/publish}"
ARTIFACTS_ROOT="${ARTIFACTS_ROOT:-$ROOT_DIR/artifacts/macos}"
CODESIGN_IDENTITY="${CODESIGN_IDENTITY:-}"

usage() {
  cat <<USAGE
Usage:
  ./package_macos.sh osx-x64|osx-arm64|all

Environment:
  APP_NAME                 App bundle name. Default: Kudo
  EXECUTABLE_NAME          Executable inside publish dir. Default: Kudo.Demo.Desktop
  BUNDLE_ID                CFBundleIdentifier. Default: com.dotnet9.kudo
  CODESIGN_IDENTITY        Developer ID identity. Empty means ad-hoc signing.
USAGE
}

die() {
  echo "Error: $*" >&2
  exit 1
}

resolve_version() {
  local version
  version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT_DIR/Directory.Build.props" | head -n 1)"
  [[ -n "$version" ]] || die "Unable to resolve project version from Directory.Build.props."
  echo "$version"
}

to_macos_version() {
  local version="$1"
  local core major minor patch
  core="${version%%[-+]*}"
  IFS='.' read -r major minor patch _ <<<"$core"
  major="${major:-0}"; minor="${minor:-0}"; patch="${patch:-0}"
  [[ "$major" =~ ^[0-9]+$ ]] || major="0"
  [[ "$minor" =~ ^[0-9]+$ ]] || minor="0"
  [[ "$patch" =~ ^[0-9]+$ ]] || patch="0"
  echo "$major.$minor.$patch"
}

sign_app() {
  local app_path="$1"
  if [[ -n "$CODESIGN_IDENTITY" ]]; then
    codesign --force --deep --sign "$CODESIGN_IDENTITY" "$app_path"
  else
    codesign --force --deep --sign - "$app_path"
  fi
}

package_one() {
  local rid="$1"
  local version
  version="$(resolve_version)"
  local bundle_version
  bundle_version="$(to_macos_version "$version")"
  local source_dir="$PUBLISH_ROOT/$rid/Kudo.Demo.Desktop"
  [[ -d "$source_dir" ]] || die "Publish output not found: $source_dir"
  local app_name="$APP_NAME.app"
  local app_dir="$ARTIFACTS_ROOT/work/$rid/$app_name"
  rm -rf "$app_dir"
  mkdir -p "$app_dir/Contents/MacOS" "$app_dir/Contents/Resources"

  cp -R "$source_dir/" "$app_dir/Contents/MacOS/"
  local executable="$app_dir/Contents/MacOS/$EXECUTABLE_NAME"
  [[ -f "$executable" ]] || die "Executable not found: $executable"
  chmod 755 "$executable"

  if [[ -f "$ICON_SOURCE" && "$ICON_SOURCE" == *.icns ]]; then
    cp "$ICON_SOURCE" "$app_dir/Contents/Resources/AppIcon.icns"
  fi

  cat > "$app_dir/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>$APP_NAME</string>
  <key>CFBundleDisplayName</key><string>$APP_NAME</string>
  <key>CFBundleIdentifier</key><string>$BUNDLE_ID</string>
  <key>CFBundleVersion</key><string>$bundle_version</string>
  <key>CFBundleShortVersionString</key><string>$bundle_version</string>
  <key>CFBundleExecutable</key><string>$EXECUTABLE_NAME</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

  sign_app "$app_dir"

  local dmg_path="$ARTIFACTS_ROOT/Kudo-v$version-$rid.dmg"
  rm -f "$dmg_path"
  # macOS runner 上 hdiutil 偶发 Resource busy（Spotlight 索引占用镜像），失败后等待重试。
  local attempt
  for attempt in 1 2 3; do
    if hdiutil create -volname "$APP_NAME $bundle_version" -srcfolder "$app_dir" -ov -format UDZO "$dmg_path"; then
      break
    fi
    if [ "$attempt" -eq 3 ]; then
      echo "hdiutil create failed after 3 attempts: $dmg_path" >&2
      return 1
    fi
    echo "hdiutil create busy, retry $((attempt + 1))/3 in 5s..." >&2
    sleep 5
    rm -f "$dmg_path"
  done

  (cd "$ARTIFACTS_ROOT" && shasum -a 256 "$(basename "$dmg_path")" > "$(basename "$dmg_path").sha256")
  echo "DMG: $dmg_path"
}

case "${1:-}" in
  osx-x64|osx-arm64) package_one "$1" ;;
  all) package_one osx-x64; package_one osx-arm64 ;;
  *) usage; exit 2 ;;
esac
