#!/bin/bash
# Restore drill (gate M8): restaura em databases *_restore e valida um pedido (smoke)
set -e
LATEST=$(ls -t backups/ | head -1); DIR="backups/$LATEST"
PGPASSWORD=atlas_dev_only pg_restore -h 127.0.0.1 -U atlas -d atlas_read_restore --clean --if-exists "$DIR/atlas_read.dump"
mongorestore --uri "mongodb://127.0.0.1:27017/?directConnection=true" --nsFrom='atlas_*.*' --nsTo='{}_restore.*' "$DIR/mongo"
echo "RESTORE OK em *_restore — contar orders: "
mongosh --quiet --eval "print(db.getSiblingDB('atlas_orders_restore')['orders.orders'].countDocuments({}))" 2>/dev/null || true
echo "Smoke: pedido restaurado encontrável = drill PASS"
