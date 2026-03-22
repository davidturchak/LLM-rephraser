#!/bin/bash
set -e

# ──────────────────────────────────────────────
#  LLM-Rephraser: Build, Package, Commit & Push
# ──────────────────────────────────────────────
#
#  Usage:
#    ./build-release.sh "Fixed a bug"         # patch bump (default)
#    ./build-release.sh "New feature" minor    # minor bump
#    ./build-release.sh "Breaking change" major # major bump
#    ./build-release.sh                        # auto patch + generic message
#

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

CSPROJ="LLM-Rephraser.csproj"
WXS="Installer/Product.wxs"
COMMIT_MSG="${1:-}"
BUMP_TYPE="${2:-patch}"

# ── 1. Check for changes ──
if git diff --quiet HEAD && [ -z "$(git ls-files --others --exclude-standard)" ]; then
    echo "No changes to commit."
    exit 0
fi

# ── 2. Read current version from csproj ──
CURRENT=$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$CSPROJ")
if [ -z "$CURRENT" ]; then
    echo "ERROR: Could not read <Version> from $CSPROJ"
    exit 1
fi

IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT"

# ── 3. Bump version ──
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
echo "$CURRENT → $NEW_VERSION ($BUMP_TYPE)"

# ── 4. Update version in csproj and WiX ──
sed -i "s|<Version>$CURRENT</Version>|<Version>$NEW_VERSION</Version>|" "$CSPROJ"
sed -i "s|Version=\"$CURRENT.0\"|Version=\"$NEW_VERSION.0\"|" "$WXS"

# ── 5. Build ──
echo "Building..."
dotnet build -c Release -v quiet

# ── 6. Publish self-contained ──
echo "Publishing..."
dotnet publish -c Release -r win-x64 --self-contained \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -v quiet

# ── 7. Build MSI ──
echo "Packaging MSI..."
cd Installer
wix build Product.wxs \
    -ext WixToolset.UI.wixext \
    -ext WixToolset.Util.wixext \
    -o LLM-Rephraser.msi \
    -arch x64
cd ..

# ── 8. Generate commit message ──
if [ -z "$COMMIT_MSG" ]; then
    COMMIT_MSG="v$NEW_VERSION"
fi

# ── 9. Commit, tag, push ──
echo "Pushing v$NEW_VERSION..."
git add -A
git commit -m "$COMMIT_MSG (v$NEW_VERSION)

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"

git tag "v$NEW_VERSION"
git push origin main --tags

echo ""
echo "✓ v$NEW_VERSION released and pushed."
