#!/bin/bash
# install.sh: postavlja CloudAndBear Desktop na uredjaj (VM, laptop). Pokrece se sa sudo.
#   sudo bash ~/deploy/install.sh            # program je u ~/cab-device (scp -r publish ...:cab-device)
#   sudo bash ~/deploy/install.sh /putanja   # program je negde drugde
# Postavlja: program u /opt/cloudandbear-desktop, servis, nalog cloudandbear (sysusers), PAM, polkit pravila; pa pokrece servis.
# Stari servis cloudandbear-gui (raniji GUI) se gasi, jer deli nalog i tty1.
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
app=${1:-${SUDO_USER:+$(getent passwd "$SUDO_USER" | cut -d: -f6)/cab-device}}
app=${app:-$here/../cab-device}

[ "$(id -u)" = 0 ] || { echo "pokreni sa sudo"; exit 1; }
[ -f "$app/CloudAndBear.Device" ] || { echo "nema $app/CloudAndBear.Device (prvo scp -r publish ...:cab-device)"; exit 1; }
for f in cloudandbear-desktop.service cloudandbear.sysusers cloudandbear-desktop.pam cloudandbear-desktop.rules; do
  [ -f "$here/$f" ] || { echo "nema $here/$f"; exit 1; }
done

echo "== zaustavljam stari desktop"
systemctl disable --now cloudandbear-gui 2> /dev/null || true
systemctl stop cloudandbear-desktop 2> /dev/null || true

echo "== program u /opt/cloudandbear-desktop"
rm -rf /opt/cloudandbear-desktop
mkdir -p /opt
cp -r "$app" /opt/cloudandbear-desktop
# vlasnik root, ostali smeju samo da citaju i izvrsavaju (scp sa Windowsa pravi foldere samo za vlasnika, a nalog cloudandbear ne bi mogao da ih otvori)
chown -R root:root /opt/cloudandbear-desktop
chmod -R a+rX,go-w /opt/cloudandbear-desktop
chmod 755 /opt/cloudandbear-desktop/CloudAndBear.Device

echo "== servis, nalog, PAM, polkit"
install -D -m 644 "$here/cloudandbear-desktop.service" /etc/systemd/system/cloudandbear-desktop.service
install -D -m 644 "$here/cloudandbear.sysusers" /usr/lib/sysusers.d/cloudandbear.conf
install -D -m 644 "$here/cloudandbear-desktop.pam" /etc/pam.d/cloudandbear-desktop
install -D -m 644 "$here/cloudandbear-desktop.rules" /etc/polkit-1/rules.d/49-cloudandbear-desktop.rules
systemd-sysusers /usr/lib/sysusers.d/cloudandbear.conf
mkdir -p /var/lib/cloudandbear
chown -R cloudandbear:cloudandbear /var/lib/cloudandbear
id cloudandbear

systemctl daemon-reload
systemctl enable cloudandbear-desktop
echo "== pokrecem desktop (kao nalog cloudandbear)"
systemctl start cloudandbear-desktop
sleep 3
systemctl --no-pager --lines=0 status cloudandbear-desktop | sed -n 1,4p
ps -o user=,pid=,args= -C CloudAndBear.Device
