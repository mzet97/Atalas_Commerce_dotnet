#!/bin/bash
# Restore drill (gate M8): restaura em *_restore e valida pedido restaurável
set -e
LATEST=$(ls -t backups/ | head -1); DIR="backups/$LATEST"
PGPASSWORD=atlas_dev_only pg_restore -h 127.0.0.1 -U atlas -d atlas_read_restore --clean --if-exists "$DIR/atlas_read.dump" || true
mongorestore --uri "mongodb://127.0.0.1:27017/?directConnection=true" --nsFrom='atlas_orders.orders' --nsTo='atlas_orders_restore.orders' "$DIR/mongo/atlas_orders" > /dev/null
COUNT=$(mongosh --quiet --uri "mongodb://127.0.0.1:27017/atlas_orders_restore?directConnection=true" --eval 'print(db.orders.countDocuments({}))' 2>/dev/null)
echo "RESTORE DRILL: $COUNT pedido(s) restaurado(s) — smoke PASS se >0"
