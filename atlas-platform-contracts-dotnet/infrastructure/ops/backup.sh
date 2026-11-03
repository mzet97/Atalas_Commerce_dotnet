#!/bin/bash
# Backup M8 (SPEC-008): pg_dump + mongodump com timestamp
set -e
TS=$(date -u +%Y%m%dT%H%M%SZ)
OUT="backups/$TS"; mkdir -p "$OUT"
PGPASSWORD=atlas_dev_only pg_dump -h 127.0.0.1 -U atlas -d atlas_read -F c -f "$OUT/atlas_read.dump"
mongodump --uri "mongodb://127.0.0.1:27017/?directConnection=true" --out "$OUT/mongo" > /dev/null
echo "BACKUP OK: $OUT"
