# Connecting to the Production PostgreSQL Server via Azure Bastion

The production **Azure Database for PostgreSQL flexible server** (`skojjt-server`) is deployed
with **private access (VNet integration)** and is **not reachable from the public internet**.

To run ad-hoc SQL (migrations, inspections, exports) an administrator connects through an
**Azure Bastion** host to a small **jump VM** that lives in the same virtual network as the
database, and then runs `psql` from that VM.

```
Your workstation ──(Azure Bastion / SSH)──▶ jumpbox VM ──(private VNet, TCP 5432)──▶ skojjt-server (PostgreSQL 18)
```

## Environment reference

| Item | Value |
|---|---|
| Subscription | `<subscription-id>` (from the team secret store) |
| Resource group | `skojjt-v2` |
| Region | `swedencentral` |
| Virtual network | `skojjt-vnet` (`10.0.0.0/16`) |
| Bastion host | `skojjt-bastion` (Standard SKU, tunneling enabled) |
| Jump VM | `jumpbox` (Ubuntu 22.04, private IP `10.0.5.4`, no public IP) |
| Jump VM subnet | `JumpVmSubnet` (`10.0.5.0/24`) |
| Bastion subnet | `AzureBastionSubnet` (`10.0.4.0/26`) |
| DB FQDN | `skojjt-server.postgres.database.azure.com` |
| DB name | `skojjt-database` |
| DB admin user | `<db-admin-user>` (from the team secret store) |
| DB port | `5432` (TLS required) |

## Prerequisites

- **Azure CLI** installed locally (`az version`).
- **Azure RBAC access** to the `skojjt-v2` resource group with at least:
  - *Reader* on the Bastion and VM, and
  - permission to start the VM if it is deallocated.
- **Access to the DB admin password** (stored in the team secret store / Key Vault — not in this doc).
- Log in and select the subscription:
  ```powershell
  az login
  az account set --subscription <subscription-id>
  ```

## Step 1 — Start the jump VM (if it was deallocated to save cost)

```powershell
az vm start -g skojjt-v2 -n jumpbox
```

Check its state:
```powershell
az vm get-instance-view -g skojjt-v2 -n jumpbox --query "instanceView.statuses[?starts_with(code,'PowerState')].displayStatus" -o tsv
```

## Step 2 — Connect to the jump VM through Bastion

### Option A — Native client from your terminal (recommended)

Requires Bastion tunneling (already enabled on `skojjt-bastion`):

```powershell
az network bastion ssh `
  --resource-group skojjt-v2 `
  --name skojjt-bastion `
  --target-resource-id $(az vm show -g skojjt-v2 -n jumpbox --query id -o tsv) `
  --auth-type ssh-key `
  --username azureuser `
  --ssh-key $HOME\.ssh\id_rsa
```

> The SSH private key must correspond to the public key registered on the VM. If you are a
> new administrator, an existing admin must add your public key to the VM
> (`az vm user update -g skojjt-v2 -n jumpbox --username azureuser --ssh-key-value "<your-public-key>"`)
> or provision a per-user account.

### Option B — Browser

Azure Portal → **Virtual machines** → `jumpbox` → **Connect** → **Bastion**, then authenticate
with the VM username/key. Use the Bastion clipboard panel for copy/paste.

## Step 3 — Ensure the PostgreSQL client is installed (one-time per VM)

The Ubuntu default repo does not carry the PostgreSQL 18 client, so use the PGDG repo:

```sh
sudo apt-get install -y curl ca-certificates
sudo install -d /usr/share/postgresql-common/pgdg
sudo curl -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc --fail \
  https://www.postgresql.org/media/keys/ACCC4CF8.asc
echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt jammy-pgdg main" \
  | sudo tee /etc/apt/sources.list.d/pgdg.list
sudo apt-get update
sudo apt-get install -y postgresql-client-18
```

Verify: `psql --version` should report `18.x`.

> If the VM has no outbound internet (apt cannot reach `apt.postgresql.org`), attach a NAT
> gateway to `JumpVmSubnet`, or install the older `postgresql-client-14` from Ubuntu's repo —
> a v14 client connects to the v18 server fine for running SQL.

## Step 4 — Connect with psql

Avoid the interactive password prompt (it is unreliable over Bastion) by using `PGPASSWORD`:

```sh
export PGPASSWORD='<db-admin-password>'
psql "host=skojjt-server.postgres.database.azure.com port=5432 dbname=skojjt-database user=<db-admin-user> sslmode=require"
unset PGPASSWORD   # when finished
```

Sanity check once connected:
```sql
SELECT version();
\conninfo   -- confirms SSL + private host
```

> The `skojjt-database` name contains a hyphen. In a connection string the `dbname=` form
> handles it fine; if you use the `-d` flag instead, quote it: `-d 'skojjt-database'`.

## Step 5 — Run SQL

Interactive statements at the `skojjt-database=>` prompt, or from a file:
```sh
psql "host=skojjt-server.postgres.database.azure.com port=5432 dbname=skojjt-database user=<db-admin-user> sslmode=require" -f migration.sql
```

For production changes, wrap in a transaction and verify before committing:
```sql
BEGIN;
  -- statements
  -- verify with SELECTs
COMMIT;   -- or ROLLBACK;
```

### Exporting a query to CSV

Use client-side `\copy` (server-side `COPY TO 'file'` is not available on Azure Flexible Server):
```sql
\copy (SELECT id, name FROM badges ORDER BY id) TO 'result.csv' WITH (FORMAT csv, HEADER true)
```

Copy the file back to your workstation via a Bastion tunnel — see the next section.

### Transferring files with a Bastion tunnel

Because the jump VM has no public IP, use `az network bastion tunnel` to forward its SSH port
(22) to a local port (e.g. 5022), then use ordinary `scp` against `127.0.0.1`. This requires
Bastion **tunneling** (Standard SKU + `enableTunneling=true`).

> **The tunnel is a long-running, blocking process.** It must stay open in its own terminal
> window while you run `scp` in a **separate** window. Running both in one window causes
> `scp` to fail with `Connection refused` because nothing is listening on the local port yet.

**Window A** — start the tunnel and leave it running:
```powershell
az network bastion tunnel `
  --resource-group skojjt-v2 `
  --name skojjt-bastion `
  --target-resource-id $(az vm show -g skojjt-v2 -n jumpbox --query id -o tsv) `
  --resource-port 22 --port 5022
```
Wait until it prints `Tunnel is ready, connection can be started ...`.

**Window B** — verify the port is listening, then copy the file:
```powershell
Test-NetConnection -ComputerName 127.0.0.1 -Port 5022   # expect TcpTestSucceeded : True
scp -P 5022 azureuser@127.0.0.1:/home/azureuser/result.csv .
```

When the transfer is done, press `Ctrl+C` in window A to close the tunnel.

> If port 5022 is already in use (e.g. a previous tunnel didn't close), pick another local
> port with `--port 5023` and match it in `scp -P 5023`.

#### Alternative — stream a file over the SSH session (no separate tunnel)

If the tunnel is troublesome, pipe the file straight down through the Bastion SSH session.
Use the **exact** filename (remote wildcards are not expanded) and redirect to a local file:
```powershell
az network bastion ssh `
  --resource-group skojjt-v2 `
  --name skojjt-bastion `
  --target-resource-id $(az vm show -g skojjt-v2 -n jumpbox --query id -o tsv) `
  --auth-type ssh-key --username azureuser --ssh-key $HOME\.ssh\id_rsa `
  "cat /home/azureuser/result.csv" > result.csv
```

## Step 6 — Clean up to stop hourly charges

The Bastion host and jump VM incur cost while running. When finished:

```powershell
az vm deallocate -g skojjt-v2 -n jumpbox
```

The Bastion host (`skojjt-bastion`) and its public IP (`bastion-pip`) can be left in place for
future use, or deleted if access is only needed occasionally:
```powershell
az network bastion delete -g skojjt-v2 -n skojjt-bastion
az network public-ip delete -g skojjt-v2 -n bastion-pip
```

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `psql` hangs / connection times out | NSG on the DB subnet blocks TCP 5432 from `10.0.5.0/24` | Add an inbound allow rule for port 5432 from the jump VM subnet |
| Host name does not resolve | Private DNS zone not linked to `skojjt-vnet` | Verify with `az network private-dns link vnet list -g skojjt-v2 --zone-name privatelink.postgres.database.azure.com` |
| `az network bastion ssh/tunnel` fails | Tunneling not enabled on Bastion | `az network bastion update -g skojjt-v2 -n skojjt-bastion --location swedencentral --enable-tunneling true` |
| `scp ... Connection refused` on port 5022 | Tunnel not running / in same window as scp | Start `az network bastion tunnel` in a separate window, wait for `Tunnel is ready`, verify with `Test-NetConnection -Port 5022` |
| Password paste ignored at prompt | Hidden prompts drop pasted input | Use `PGPASSWORD` env var or a `~/.pgpass` file |
| `Unable to locate package postgresql-client-18` | PGDG repo not added | Re-run the repo setup in Step 3, confirm `/etc/apt/sources.list.d/pgdg.list` exists |

## Security notes

- Never commit the DB password or a `.pgpass` file to source control.
- Prefer per-administrator SSH keys / accounts on the jump VM over a shared key.
- Deallocate the jump VM when not in use to reduce both cost and attack surface.
- All connections use `sslmode=require`; use `sslmode=verify-full` with a CA cert for stricter validation.
