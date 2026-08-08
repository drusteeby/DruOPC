#!/bin/sh
# Run the simulator from a writable directory so its certificate stores
# (pki/) and log file land in the snap's per-user data, not the read-only
# snap filesystem. Configuration files are read from $SNAP (the content root).
set -e
mkdir -p "$SNAP_USER_COMMON/simulator"
cd "$SNAP_USER_COMMON/simulator"
exec "$SNAP/opcplc" "$@"
