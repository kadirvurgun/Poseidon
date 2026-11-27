#!/usr/bin/env bash
# POSEIDON UAT VPS ilk kurulum (Ubuntu 24.04 LTS). Root olarak bir kez çalıştırılır:
#   scp infra/uat/bootstrap-vps.sh root@VPS_IP:/root/ && ssh root@VPS_IP 'bash /root/bootstrap-vps.sh "ssh-ed25519 AAAA... deploy-key"'
# Argüman: GitHub Actions'ın deploy için kullanacağı SSH public key (ayrı, yalnız bu iş için üretilmiş anahtar).
# Yaptıkları: güncelleme, otomatik güvenlik yaması, swap, UFW (22/80/443), parola ile SSH kapatma,
# fail2ban, Docker Engine + compose eklentisi, 'deploy' kullanıcısı ve /opt/poseidon-uat dizini.
# Betik tekrar çalıştırılabilir (idempotent). Hiçbir uygulama sırrı üretmez veya yazdırmaz.
set -euo pipefail

DEPLOY_KEY="${1:-}"
if [[ -z "$DEPLOY_KEY" || "$DEPLOY_KEY" != ssh-* ]]; then
  echo "Kullanım: bash bootstrap-vps.sh \"ssh-ed25519 AAAA... deploy-key\"" >&2
  exit 2
fi
if [[ "$(id -u)" -ne 0 ]]; then echo "root olarak çalıştırın" >&2; exit 2; fi
. /etc/os-release
if [[ "${ID}" != "ubuntu" ]]; then echo "Yalnız Ubuntu desteklenir (bulunan: ${ID})" >&2; exit 2; fi

export DEBIAN_FRONTEND=noninteractive
apt-get update -q
apt-get -y -q upgrade
apt-get -y -q install ca-certificates curl gnupg ufw fail2ban unattended-upgrades

# Otomatik güvenlik güncellemeleri
dpkg-reconfigure -f noninteractive unattended-upgrades

# 4 GB swap (yoksa) — .NET + Keycloak + PostgreSQL derleme/başlangıç tepeleri için
if ! swapon --show | grep -q /swapfile; then
  fallocate -l 4G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  grep -q '^/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

# Docker Engine (resmi depo)
if ! command -v docker >/dev/null && \
   ! curl -fsS -o /dev/null "https://download.docker.com/linux/ubuntu/dists/${VERSION_CODENAME}/stable/Release"; then
  # Docker's repository has no packages for this (newer) release yet: use Ubuntu's own packages.
  apt-get -y -q install docker.io docker-compose-v2 docker-buildx
fi
if ! command -v docker >/dev/null; then
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  chmod a+r /etc/apt/keyrings/docker.asc
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" \
    > /etc/apt/sources.list.d/docker.list
  apt-get update -q
  apt-get -y -q install docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi
# Konteyner loglarının diski doldurmaması
mkdir -p /etc/docker
if [[ ! -f /etc/docker/daemon.json ]]; then
  cat > /etc/docker/daemon.json <<'JSON'
{ "log-driver": "local", "log-opts": { "max-size": "20m", "max-file": "5" } }
JSON
  systemctl restart docker
fi
systemctl enable --now docker

# deploy kullanıcısı: yalnız anahtarla giriş, docker grubunda, sudo yok
id deploy >/dev/null 2>&1 || useradd -m -s /bin/bash deploy
usermod -aG docker deploy
install -d -m 700 -o deploy -g deploy /home/deploy/.ssh
touch /home/deploy/.ssh/authorized_keys
grep -qxF "$DEPLOY_KEY" /home/deploy/.ssh/authorized_keys || echo "$DEPLOY_KEY" >> /home/deploy/.ssh/authorized_keys
chown deploy:deploy /home/deploy/.ssh/authorized_keys
chmod 600 /home/deploy/.ssh/authorized_keys

# Uygulama dizini (sırlar /opt/poseidon-uat/secrets altında, yalnız deploy okur)
install -d -m 750 -o deploy -g deploy /opt/poseidon-uat /opt/poseidon-uat/secrets /opt/poseidon-uat/backups

# SSH sertleştirme: parola ile giriş kapalı. Root için yalnız anahtar.
cat > /etc/ssh/sshd_config.d/10-poseidon.conf <<'CONF'
PasswordAuthentication no
KbdInteractiveAuthentication no
PermitRootLogin prohibit-password
CONF
if [[ ! -s /root/.ssh/authorized_keys ]]; then
  echo "UYARI: /root/.ssh/authorized_keys boş. Parola girişi kapatılmadan önce kendi anahtarınızı ekleyin; SSH yeniden başlatılmadı." >&2
else
  systemctl restart ssh
fi

# Güvenlik duvarı
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw allow 443/udp
ufw --force enable
# Not: Docker yayınlanan portları UFW'yi atlayabilir. Compose dosyası yalnız Caddy'nin 80/443'ünü yayınlar;
# PostgreSQL ve Keycloak hiçbir host portuna bağlanmaz.

systemctl enable --now fail2ban

echo "Tamam. Sonraki adım: docs/operations/uat-deployment.md → 'Sırları oluştur'."
