# CloudAndBear.Device: desktop na Debianu (DRM)

`src/CloudAndBear.Device` je domaćin koji pokreće isti desktop (`CloudAndBear.Ui`) na pravom ekranu Debiana, bez X11 i Waylanda: Avalonia crta preko DRM/KMS, a ulaz čitamo sami.

| Fajl | Šta radi |
|---|---|
| `Program.cs` | bira grafičku karticu na kojoj je ekran (laptop ima dve), pokreće Avaloniju na DRM-u, daje servisima zajednički raspored tastature |
| `KeyboardInputBackend.cs` | čita tastature iz `/dev/input` (hot-plug na sekundu), gasi tastere virtuelnog terminala iza desktopa (`K_OFF`), vraća ih pri izlasku |
| `KeyTranslator.cs` | čista logika: evdev kod → Key, tekst, modifikatori, Caps Lock, AltGr, rasporedi, Alt+Shift (proverena običnim testom) |
| `PointerInput.cs` | miš i touchpad preko libinput-a (hot-plug na sekundu, tap-to-click, točkić i skrol sa dva prsta) |

Kursor miša crta `DesktopView` (Avalonia ga na DRM-u ne crta). Rasporedi tastature (US, SR, SR-US) su u `CloudAndBear.Ui/Input`; izbor se pamti u `/var/lib/cloudandbear/layout`.

## Opcije

`CloudAndBear.Device --card=/dev/dri/card1 --scale=1.5` (bez opcija: kartica sa priključenim ekranom, razmera 1.0).

## Prečice (uređaj i pregled)

Ctrl+Alt+Q izlazi u konzolu (servis vraća prijavu na tty1), Alt+Shift menja raspored tastature (ili klik na oznaku u traci), ostalo je u `docs/DESKTOP.md`. Desni Alt je AltGr na srpskim rasporedima (treći nivo slova, bez Alt-a), a na US rasporedu običan Alt.

## Paketi na Debianu

Biblioteke koje DRM stek traži: `libgl1 libfontconfig1 libdrm2 libegl1 libgbm1 libinput10 libevdev2 libgl1-mesa-dri libegl-mesa0`, **font `fonts-dejavu-core`** (terminal i Kontejneri koriste monospace), `kbd` (`kbd_mode` pri izlasku), `polkitd`, `systemd`. Po želji `network-manager` (Mreža), `docker.io` (Kontejneri), `kubectl` (Kubernetes). Ovo treba da bude `Depends` paketa `cloudandbear-desktop` (paket još nije napravljen za ovaj projekat; stari je u repozitorijumu `CloudAndBearLinux`, `gui/package`).

## Postavljanje na uređaj

```powershell
dotnet publish src/CloudAndBear.Device -r linux-x64 --self-contained -c Release -o publish
scp -r publish korisnik@uredjaj:cab-device
scp -r deploy korisnik@uredjaj:deploy
```

```bash
sudo bash ~/deploy/install.sh        # program u /opt/cloudandbear-desktop, servis, nalog cloudandbear, PAM, polkit
```

`deploy/`: `cloudandbear-desktop.service` (tty1, nalog `cloudandbear`, vraća getty pri izlazu), `cloudandbear.sysusers` (grupe video, input, render, docker), PAM za logind sesiju, polkit pravila (restart, isključenje, NetworkManager bez lozinke). Stari servis `cloudandbear-gui` install.sh gasi, jer deli nalog i tty1.

**Pazi:** `systemctl restart cloudandbear-desktop` zaglavi servis pre pokretanja programa (trka sa `OnSuccess=getty`). Koristi `stop`, pauzu od par sekundi, pa `start`.

## Šta je provereno (VM `cab-debian-test`, Debian 13, VMSVGA, 2026-11-21)

- Servis kao nalog `cloudandbear`, desktop na DRM-u (1280x800), izlaz Ctrl+Alt+Q i povratak konzole.
- Tastatura iz `/dev/input` (VirtualBox PS/2): Alt+F1, kucanje u pretragu, Enter, Alt+Shift menja US → SR → SR-US i pamti izbor (preživljava ponovno pokretanje servisa), `č ć š đ ž` u terminalu.
- Miš (virtuelni, `/dev/uinput`, priključen posle pokretanja): kursor se crta, zadržavanje preko dugmeta, klik zatvara prozor, prevlačenje pomera prozor.
- Prave aplikacije: **Sistem** (procesor, memorija, disk, jezgro, procesor računara), **Kontejneri** (pravi Docker: spisak, stanje, port 8080->80), **Kubernetes** (pravi kubectl: konteksti iz `/var/lib/cloudandbear/kubeconfig`, greška klastera), **Terminal** (prava bash ljuska kao nalog cloudandbear, boje, `tty`, `exit`).

**Nije provereno:** pravi laptop (Acer Nitro: dve grafike, HiDPI), touchpad (tap-to-click, skrol sa dva prsta), USB tastatura priključena u hodu, točkić miša, Mreža sa pravim Wi-Fi-jem, pravi klaster, Docker akcije (start/stop/brisanje), restart i isključenje kroz polkit.

## Poznata ograničenja

- Terminal radi kao nalog `cloudandbear` (grupa docker, kubeconfig u `/var/lib/cloudandbear`), ne kao korisnik koji je instalirao sistem. Pravi korisnik dolazi sa ekranom za prijavu (desktop kao korisnik preko logind sesije).
- Kursor je uvek strelica (nema oblika za ivice prozora ni tekst).
- Razmera se zadaje ručno (`--scale`), nema automatske za velike ekrane.
- Terminal: bez miša za programe (htop, k9s) i bez ponovnog prelamanja pri promeni širine.

## Probni postupak (VM)

1. `VBoxManage startvm cab-debian-test --type headless`, pa Enter na GRUB meniju (`controlvm keyboardputscancode 1c 9c`).
2. Live sistem je prazan pri svakom pokretanju: ključ za SSH se ukuca na konzoli (`controlvm keyboardputstring`), pa `sudo apt-get install` paketi iz gornjeg spiska.
3. `scp` publish i deploy, `sudo bash ~/deploy/install.sh`, pa slike ekrana (`controlvm screenshotpng`), tasteri (`keyboardputscancode`, `keyboardputstring`) i miš preko `/dev/uinput`.
