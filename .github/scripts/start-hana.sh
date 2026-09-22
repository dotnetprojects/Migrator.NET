#!/usr/bin/env bash
set -euo pipefail
sudo sysctl -w fs.file-max=20000000 fs.aio-max-nr=262144 vm.memory_failure_early_kill=1 vm.max_map_count=135217728
mkdir -p "$RUNNER_TEMP/hana"
printf '{"master_password":"MgT9ci7Q4xZ2"}' > "$RUNNER_TEMP/hana/password.json"
sudo chown -R 12000:79 "$RUNNER_TEMP/hana"
sudo chmod 600 "$RUNNER_TEMP/hana/password.json"
docker pull saplabs/hanaexpress:2.00.088.00.20251110.1
docker run -d --name migrator-db --hostname hxe -p 39041:39041 \
  --ulimit nofile=1048576:1048576 \
  --sysctl kernel.shmmax=1073741824 --sysctl kernel.shmmni=32768 --sysctl kernel.shmall=8388608 \
  --sysctl net.ipv4.ip_local_port_range="40000 60999" \
  -v "$RUNNER_TEMP/hana:/hana/mounts" \
  saplabs/hanaexpress:2.00.088.00.20251110.1 \
  --passwords-url file:///hana/mounts/password.json --agree-to-sap-license
for attempt in $(seq 1 180); do
  if docker exec migrator-db /usr/sap/HXE/HDB90/exe/hdbsql -i 90 -d HXE -u SYSTEM -p MgT9ci7Q4xZ2 'SELECT 1 FROM DUMMY' >/dev/null 2>&1; then
    echo "MIGRATOR_HANA=Server=localhost:39041;UserID=SYSTEM;Password=MgT9ci7Q4xZ2" >> "$GITHUB_ENV"
    exit 0
  fi
  if [ "$(docker inspect -f '{{.State.Running}}' migrator-db)" != true ]; then
    docker logs migrator-db
    exit 1
  fi
  sleep 5
done
docker logs migrator-db
exit 1
