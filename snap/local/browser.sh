#!/bin/sh
# The browser serves its static assets relative to the working directory.
# It writes only under $HOME (remapped by snapd), so a read-only cwd is fine.
set -e
cd "$SNAP/browser"
exec "$SNAP/browser/DruOpc" "$@"
