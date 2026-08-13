#!/bin/sh
# DruOPC installer for Linux and macOS.
#   curl -fsSL https://raw.githubusercontent.com/drusteeby/DruOPC/main/install.sh | sh
#
# Linux with snapd installs the snap; anything else gets the self-contained
# release binaries. Knobs (set as env vars):
#   DRUOPC_HOME     install directory for the binaries (default: ~/druopc)
#   DRUOPC_VERSION  release tag to install (default: latest)
#   DRUOPC_NO_SNAP  set to 1 to skip the snap and use the binaries on Linux
set -eu

REPO=drusteeby/DruOPC
DIR="${DRUOPC_HOME:-$HOME/druopc}"

case "$(uname -s)" in
  Linux)
    if [ -z "${DRUOPC_NO_SNAP:-}" ] && command -v snap >/dev/null 2>&1; then
      echo "Installing the druopc snap (asks for sudo)..."
      sudo snap install druopc
      echo
      echo "Done. Start it:"
      echo "  druopc.simulator   # terminal 1 - the simulated PLC"
      echo "  druopc.browser     # terminal 2 - DruOPC on http://localhost:5000"
      exit 0
    fi
    OS=linux ;;
  Darwin)
    OS=osx ;;
  *)
    echo "Unsupported OS: $(uname -s)." >&2
    echo "On Windows, download the win-x64 zips from https://github.com/$REPO/releases" >&2
    exit 1 ;;
esac

case "$(uname -m)" in
  arm64|aarch64) ARCH=arm64 ;;
  x86_64|amd64)  ARCH=x64 ;;
  *) echo "No prebuilt binaries for CPU '$(uname -m)'." >&2; exit 1 ;;
esac

VERSION="${DRUOPC_VERSION:-$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" \
  | sed -n 's/.*"tag_name": *"\([^"]*\)".*/\1/p')}"
[ -n "$VERSION" ] || { echo "Could not determine the latest release tag." >&2; exit 1; }

echo "Installing DruOPC $VERSION ($OS-$ARCH) to $DIR ..."
for APP in simulator browser; do
  mkdir -p "$DIR/$APP"
  curl -fL --progress-bar "https://github.com/$REPO/releases/download/$VERSION/druopc-$APP-$VERSION-$OS-$ARCH.tar.gz" \
    | tar -xz -C "$DIR/$APP"
done

if [ "$OS" = osx ]; then
  # The binaries are not notarized; clear Gatekeeper's quarantine flag.
  xattr -dr com.apple.quarantine "$DIR" 2>/dev/null || true
fi

echo
echo "Done. Start it:"
echo "  $DIR/simulator/opcplc   # terminal 1 - the simulated PLC"
echo "  $DIR/browser/DruOpc     # terminal 2 - DruOPC on http://localhost:5000"
