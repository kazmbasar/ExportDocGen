# Deploying ExportDocGen (Oracle Cloud + host nginx)

The app runs as **one Docker container** bound to `127.0.0.1:8080`. **nginx**,
installed on the VM, is the public entry point and reverse-proxies to it;
**certbot** obtains and auto-renews the Let's Encrypt certificate.

```
Internet ──:80/:443──▶ nginx (host, apt) ──proxy_pass──▶ 127.0.0.1:8080
                        └ certbot --nginx (auto-TLS)      exportdocgen container
                                                          └ /data volume (SQLite + DP keys)
```

**You need:**

- An Oracle Cloud compute instance running **Ubuntu** (these steps) or Oracle
  Linux (notes inline). Any shape — the app idles ~150 MB.
- A **domain** with a DNS **A record** pointing a hostname
  (e.g. `exportdocgen.example.com`) at the instance's **public IP**.
- SSH access.

---

## 1. Oracle Cloud console

1. Open the instance page and note its **public IP address**.
2. **Networking → the instance's VCN → Security Lists → the default list** (or the
   NSG attached to the instance) → **Add Ingress Rules**:

   | Source CIDR | IP Protocol | Destination Port |
   |-------------|-------------|------------------|
   | `0.0.0.0/0` | TCP | `80` |
   | `0.0.0.0/0` | TCP | `443` |

   Leave `8080` closed — nothing outside the box needs it.

---

## 2. Point DNS at the instance

Create an **A record**: `exportdocgen.example.com → <instance public IP>`.
Confirm it resolves before continuing (certbot will fail otherwise):

```bash
dig +short exportdocgen.example.com     # must print the instance IP
```

---

## 3. Open the instance firewall

Oracle's **Ubuntu** images keep an `iptables` ruleset that blocks everything
except SSH. Add 80/443 and persist:

```bash
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 80  -j ACCEPT
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 443 -j ACCEPT
sudo netfilter-persistent save
```

> **Oracle Linux:** `sudo firewall-cmd --permanent --add-service={http,https} && sudo firewall-cmd --reload`

---

## 4. Install Docker

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker "$USER"
newgrp docker            # or log out and back in
docker run --rm hello-world
```

---

## 5. Install nginx + certbot

```bash
sudo apt update && sudo apt install -y nginx
sudo snap install --classic certbot
sudo ln -sf /snap/bin/certbot /usr/local/bin/certbot
```

> **Oracle Linux:** `sudo dnf install -y nginx certbot python3-certbot-nginx && sudo systemctl enable --now nginx`

---

## 6. Get the code and set the password

```bash
git clone https://github.com/kazmbasar/ExportDocGen.git
cd ExportDocGen
cp .env.example .env

docker compose build
docker compose run --rm --no-deps app hash-password 'choose-a-strong-password'
```

Paste the printed `v1.…` line into `.env`:

```ini
AUTH_PASSWORD_HASH=v1.210000.….…
AUTH_USERNAME=Team
```

---

## 7. Start the container

```bash
docker compose up -d
curl -sI http://127.0.0.1:8080/login       # expect: HTTP/1.1 200 OK
docker compose logs --tail=30 app          # migrations applied, "Now listening on: http://[::]:8080"
```

On first start the app applies its EF Core migrations and seeds the two seller
companies + two sample customers into `/data/exportdocgen.db` (the `app-data`
Docker volume).

---

## 8. Configure nginx

```bash
sudo cp deploy/nginx/exportdocgen.conf /etc/nginx/sites-available/exportdocgen
sudo cp deploy/nginx/upgrade.conf      /etc/nginx/conf.d/upgrade.conf
sudo sed -i 's/exportdocgen\.example\.com/exportdocgen.YOURDOMAIN.com/' \
  /etc/nginx/sites-available/exportdocgen

sudo ln -s /etc/nginx/sites-available/exportdocgen /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default

sudo nginx -t && sudo systemctl reload nginx
```

> **Oracle Linux:** no `sites-available/-enabled` — copy `exportdocgen.conf` to
> `/etc/nginx/conf.d/exportdocgen.conf`, skip the symlink and the `default`
> removal.

Check it from **your laptop** (plain HTTP, pre-certificate):

```bash
curl -sI http://exportdocgen.YOURDOMAIN.com/login     # expect: HTTP/1.1 200 OK
```

---

## 9. Get the HTTPS certificate

```bash
sudo certbot --nginx -d exportdocgen.YOURDOMAIN.com
```

Choose **"redirect"** when asked (forces HTTP → HTTPS). certbot edits the site
config to add the `listen 443 ssl` server block, installs the cert, and sets up a
systemd timer for renewal. Confirm renewal works:

```bash
sudo certbot renew --dry-run
```

---

## 10. Verify

Open `https://exportdocgen.YOURDOMAIN.com` — you should get the login page over a
valid certificate. Sign in, open an order, click a **Documents** download — the
PDF opens and there is no "disconnected" banner (that confirms the Blazor SignalR
circuit works through nginx).

```bash
# from your laptop
curl -skI https://exportdocgen.YOURDOMAIN.com/                       # 302 -> /login
curl -skI https://exportdocgen.YOURDOMAIN.com/orders/1/proforma.pdf  # 302 -> /login (protected)
```

---

## 11. Load the stock catalogue

The container starts with only the seller companies and sample customers. Load
the real product catalogue (export `stocks.ods` → `.xlsx` first — ClosedXML needs
`.xlsx`):

```bash
# copy the file to the VM, then:
docker compose cp stocks.xlsx app:/tmp/stocks.xlsx
docker compose exec app dotnet ExportDocGen.dll import-stock /tmp/stocks.xlsx --replace
```

---

## Updating to a new version

```bash
cd ~/ExportDocGen
git pull
docker compose up -d --build
```

The database and login cookies survive — they live in the `app-data` volume, not
the image. nginx is untouched.

## Backups

Everything stateful is in the `app-data` volume (SQLite database + Data
Protection keys):

```bash
docker run --rm -v exportdocgen_app-data:/data -v "$PWD":/backup alpine \
  tar czf /backup/exportdocgen-backup-$(date +%F).tgz -C /data .
```

Keep those files off the box. Restore by extracting back into the volume before
`docker compose up`.

## Notes

- **Auth model:** one shared password for everyone, hashed (PBKDF2-SHA256) in
  `AUTH_PASSWORD_HASH`. No user accounts. To change it: re-run the
  `hash-password` command, update `.env`, `docker compose up -d`.
- **HTTPS:** nginx terminates TLS; certbot renews automatically. The app listens
  on plain HTTP `127.0.0.1:8080` only — not reachable from the internet.
- **Forwarded headers:** the app trusts `X-Forwarded-Proto` / `-For` from its
  immediate peer (only nginx can reach it), so it builds correct `https://` URLs
  and logs real client IPs.
- **Data Protection keys** are persisted to the volume, so the login cookie stays
  valid across `docker compose up -d --build`. Losing the `app-data` volume logs
  everyone out and loses the database.
- **Logs:** `docker compose logs -f app`, `sudo tail -f /var/log/nginx/exportdocgen.error.log`.
