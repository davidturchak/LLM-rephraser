#!/bin/bash
set -e

# ──────────────────────────────────────────────
#  LLM-Rephraser: Build, Package, Commit & Push
# ──────────────────────────────────────────────
#
#  Usage:
#    ./build-release.sh patch    # 1.1.0 → 1.1.1
#    ./build-release.sh minor    # 1.1.0 → 1.2.0
#    ./build-release.sh major    # 1.1.0 → 2.0.0
#    ./build-release.sh          # defaults to patch
#

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

CSPROJ="LLM-Rephraser.csproj"
WXS="Installer/Product.wxs"
BUMP_TYPE="${1:-patch}"

# ── 1. Read current version from csproj ──
CURRENT=$(grep -oP '(?<=<Version>)[^<]+' "$CSPROJ")
if [ -z "$CURRENT" ]; then
    echo "ERROR: Could not read <Version> from $CSPROJ"
    exit 1
fi

IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT"
echo "Current version: $CURRENT"

# ── 2. Bump version ──
case "$BUMP_TYPE" in
    major) MAJOR=$((MAJOR + 1)); MINOR=0; PATCH=0 ;;
    minor) MINOR=$((MINOR + 1)); PATCH=0 ;;
    patch) PATCH=$((PATCH + 1)) ;;
    *)
        echo "ERROR: Invalid bump type '$BUMP_TYPE'. Use: major, minor, or patch"
        exit 1
        ;;
esac

NEW_VERSION="$MAJOR.$MINOR.$PATCH"
echo "New version:     $NEW_VERSION"

# ── 3. Update version in csproj ──
sed -i "s|<Version>$CURRENT</Version>|<Version>$NEW_VERSION</Version>|" "$CSPROJ"
echo "Updated $CSPROJ"

# ── 4. Update version in WiX installer ──
sed -i "s|Version=\"$CURRENT.0\"|Version=\"$NEW_VERSION.0\"|" "$WXS"
echo "Updated $WXS"

# ── 5. Build ──
echo ""
echo "Building Release..."
dotnet build -c Release -v quiet

# ── 6. Publish self-contained ──
echo "Publishing self-contained single file..."
dotnet publish -c Release -r win-x64 --self-contained \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -v quiet

# ── 7. Build MSI ──
echo "Building MSI installer..."
cd Installer
wix build Product.wxs \
    -ext WixToolset.UI.wixext \
    -ext WixToolset.Util.wixext \
    -o LLM-Rephraser.msi \
    -arch x64
cd ..
echo "MSI built: Installer/LLM-Rephraser.msi"

# ── 8. Git commit & push ──
echo ""
echo "Committing and pushing..."
git add -A
git commit -m "Release v$NEW_VERSION

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"

git tag "v$NEW_VERSION"
git push origin main --tags

echo ""
echo "════════════════════════════════════════"
echo "  Released v$NEW_VERSION successfully!"
echo "════════════════════════════════════════"
