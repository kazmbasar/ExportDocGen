# Deploying ExportDocGen (Oracle Cloud free tier)

The app ships as a Docker image and runs behind [Caddy](https://caddyserver.com/),
which terminates HTTPS with an automatic Let's Encrypt certificate. One
`docker compose` command brings up both.

**You need:**

- An Oracle Cloud **Always Free** compute instance running **Ubuntu** (Ampere/ARM
  or AMD — the image builds for whatever the VM is).
- A **domain name** with a DNS **A record** pointing a hostname
  (e.g. `exportdocgen.example.com`) at the instance's **public IP**.
- SSH access to the instance.

---

## 1. Open the firewall (two layers on Oracle Cloud)

### a) VCN security list / NSG (Oracle console)

In the OCI console: **Networking → Virtual Cloud Networks → your VCN → Security
Lists → Default Security List** (or the NSG attached to the instance) → **Add
Ingress Rules**:

| Source CIDR | IP Protocol | Destination Port |
|-------------|-------------|------------------|
| `0.0.0.0/0` | TCP | `80` |
| `0.0.0.0/0` | TCP | `443` |

### b) The instance's own firewall (Ubuntu)

Oracle's Ubuntu images ship with `iptables` rules that block everything except
SSH. Add 80/443 and persist:

```bash
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 443 -j ACCEPT
sudo netfilter-persistent save
```

(If the instance uses `firewalld` instead: `sudo firewall-cmd --permanent
--add-service={http,https} && sudo firewall-cmd --reload`.)

---

## 2. Install Docker

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker "$USER"
newgrp docker          # or log out and back in
```

---

## 3. Get the code and configure

```bash
git clone https://github.com/kazmbasar/ExportDocGen.git
cd ExportDocGen
cp .env.example .env
```

Build the image, then generate the password hash with it:

```bash
docker compose build
docker compose run --rm --no-deps -e Auth__PasswordHash=x app hash-password 'choose-a-strong-password'
```

Edit `.env`:

```ini
SITE_ADDRESS=exportdocgen.example.com
AUTH_PASSWORD_HASH=v1.210000.….…      # paste the line from the command above
AUTH_USERNAME=Team
```

---

## 4. Point DNS at the instance

Create an **A record**: `exportdocgen.example.com → <instance public IP>`.
Check it has propagated:

```bash
dig +short exportdocgen.example.com     # should print the instance IP
```

Caddy cannot get a certificate until this resolves.

---

## 5. Start it

```bash
docker compose up -d
docker compose logs -f caddy        # watch the certificate being issued
```

Open `https://exportdocgen.example.com`. You should get the login page over a
valid certificate. Sign in with the password you chose.

---

## 6. Load the stock catalogue

The container starts empty except for the two seller companies and sample
customers. To load the real product catalogue, copy the `.xlsx` export in and run
the import command inside the running container:

```bash
docker compose cp "stocks.xlsx" app:/tmp/stocks.xlsx
docker compose exec app dotnet ExportDocGen.dll import-stock /tmp/stocks.xlsx --replace
```

(Same command as local — see the main README. ClosedXML needs `.xlsx`, so export
the `.ods` first.)

---

## Updating to a new version

```bash
cd ExportDocGen
git pull
docker compose up -d --build
```

The database and login cookies survive (they live in the `app-data` volume, not
the image).

## Backups

Everything stateful is in the `app-data` volume — the SQLite database and the
Data Protection keys:

```bash
docker run --rm -v exportdocgen_app-data:/data -v "$PWD":/backup alpine \
  tar czf /backup/exportdocgen-backup-$(date +%F).tgz -C /data .
```

Keep those files somewhere off the box. Restore by extracting back into the
volume before `docker compose up`.

## Notes

- **Auth model:** one shared password for everyone, hashed (PBKDF2-SHA256) in
  `AUTH_PASSWORD_HASH`. No user accounts, no self-registration. To change the
  password, regenerate the hash (step 3) and `docker compose up -d`.
- **HTTPS:** handled entirely by Caddy. The app itself only listens on plain
  HTTP `8080`, not published to the host — only Caddy can reach it.
- **Data Protection keys** are persisted so the login cookie stays valid across
  restarts and redeploys. Losing the `app-data` volume logs everyone out (and
  loses the database).
- **Resources:** the app idles at ~150 MB RAM. Fine on the smallest free shape.
