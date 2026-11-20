# Model desktopa

Odluka 2026-11-20: **pravi desktop** (više prozora, traka, pokretač), ne jedna aplikacija preko celog ekrana.

## Šta to znači na ovom steku

- Na uređaju crta DRM/KMS: **jedan proces** drži ceo ekran, nema X11 ni Waylanda (pravilo: nikakav drugi GUI). Zato je naš shell sam kompozitor: desktop je jedan korenski `MainView`, a prozori žive unutar njega.
- Prozori su **Avalonia kontrole** (`WindowFrame` u `WindowHost`-u), ne sistemski prozori. Avalonia `Window` na uređaju ne postoji (`ISingleViewApplicationLifetime`).
- Aplikacije su **moduli u istom procesu** (View + ViewModel): Terminal, Mreža, Kontejneri, Kubernetes, Sistem, Podešavanja.
- Tuđe GUI aplikacije (Chrome, VS Code...) ovako ne mogu da rade. Za to bi trebao sopstveni Wayland kompozitor: veliki posao i posebna odluka, ne sada.
- **Terminal** je glavna aplikacija za našu publiku (DevOps). Treba mu VT emulator + PTY; otvoreno je da li biblioteka ili svoj.

## Slojevi (odozdo naviše)

1. **Pozadina**
2. **Prozori** (`WindowHost`): z-redosled, fokus, pomeranje, promena veličine, minimizuj / maksimizuj / zatvori
3. **Traka**: pokretač, otvoreni prozori, sistemska oblast (mreža, raspored tastature, baterija, zvuk, sat)
4. **Preklopi**: pokretač (meni), obaveštenja, potvrde (Da/Ne), prebacivač prozora (Alt+Tab)
5. **Kursor miša** (Avalonia ga na uređaju ne crta; rešenje postoji u starom kodu)

## Ulaz

- Tastatura (korak 4 vodiča): Alt+F1 pokretač, Alt+Tab / Alt+Shift+Tab prozori, Alt+F4 zatvori, Alt+Gore / Alt+Dole maksimizuj / vrati / minimizuj, Alt+Shift+strelice pomeri, Alt+Ctrl+strelice veličina. Za pregled na Windowsu postoje Alt+F6 i Alt+W (Windows hvata Alt+Tab i Alt+F4). Super dolazi kad DRM ulaz bude gotov. Aktivan prozor uvek drži tastaturu.
- Miš i touchpad: prevlačenje naslovne trake, promena veličine sa ivica. Sve mora da radi i **bez miša**; fokus uvek vidljiv.

## Odluke koje čekaju

1. ~~Traka~~ **odlučeno 2026-11-20: dole** (varijanta A): pokretač levo, otvoreni prozori u sredini, sistemska oblast i sat desno.
2. Ton izgleda (referenca ili opis), tamna / svetla tema.
3. Gde idu prvo: zaključani ekran i prijava, ili sam desktop.
4. ~~Terminal: biblioteka ili svoj emulator~~ odlučeno: svoj (vidi odeljak Terminal).

## Aplikacije i servisi

Aplikacija = ViewModel + View + servis. Servis je interfejs u `Services/` sa dve izvedbe: **Linux** (čita `/proc`, `/sys`, zove `nmcli` i `systemctl`) i **Fake** (izmišljeni podaci za pregled na Windowsu). `DesktopServices.Create()` bira: na Linuxu prave, drugde lažne. Zato se izgled dizajnira na PC-u bez Debiana, a na uređaju se ništa ne menja u pogledima.

| Aplikacija | Servis | Stanje |
|---|---|---|
| Sistem | `ISystemService` | gotovo: procesor, memorija, disk, baterija, podaci o računaru, restart / isključi / izađi u konzolu sa potvrdom |
| Mreža | `INetworkService` | gotovo: adapteri, Wi-Fi spisak, povezivanje sa lozinkom, prekid veze |
| Terminal | `ITerminalService` | gotovo: svoj emulator (xterm podskup), ljuska na pseudo-terminalu, izbor mišem, istorija, kopiranje i lepljenje |
| Kontejneri | `IContainerService` | gotovo: Docker spisak (stanje u boji), start / stop / restart, brisanje sa potvrdom, poslednje linije ispisa |
| Kubernetes | `IKubeService` | gotovo, samo čitanje: konteksti, čvorovi, podovi svih namespace-ova (stanje u boji), greška klastera se prikazuje |

Linux izvedbe Sistema i Mreže su prenete iz starog `gui/` (koji je radio na laptopu i u VM-u), ali u ovom projektu još nisu pokrenute na Debianu; to se proverava kad bude `CloudAndBear.Device`. Terminal je proveren na pravom Linuxu (vidi niže). Kontejneri i Kubernetes su preneti iz istog starog koda i provereni bez Debiana: `DockerEngineService` protiv lažnog Docker Engine API-ja na unix soketu (isti HTTP kod kao pravi: okviri ispisa, 304, 404/409 sa porukom Dockera, nema soketa), `KubectlService` protiv lažnog kubectl-a sa JSON-om kakav kubectl vraća (CrashLoopBackOff, Terminating, Completed, SchedulingDisabled...) i protiv pravog `kubectl.exe` (konteksti iz kubeconfig-a, poruka kad klaster ne odgovara). Nije proveren protiv pravog Docker-a i klastera.

## Terminal

Odluka (2026-11-21): **svoj emulator**, bez biblioteke. Razlog: nema spoljnih zavisnosti, radi isto na PC-u i na DRM uređaju, a ceo skup sekvenci koji DevOps alati koriste je mali i proverljiv.

- `Terminal/TerminalEmulator.cs` čita bajtove (UTF-8, ESC, CSI, OSC) i menja `TerminalScreen` (mreža ćelija, kursor, oblast skrolovanja, alternativni ekran, istorija 3000 redova). Podržano: boje 16/256/prave, podebljano, kurziv, podvučeno, obrnuto, zatamnjeno, precrtano, DEC grafika, široki znaci, tabulatori, ubaci/briši red i znak, pomeranje kursora, DEC režimi (kursor, strelice, bracketed paste, alt ekran 47/1047/1049), naslov (OSC 0/2), odgovori na upite (pozicija kursora, vrsta terminala).
- `Terminal/PtyTerminalService.cs` (Linux): pseudo-terminal preko `posix_openpt`; ljuska se pokreće sa `sh -c 'exec setsid -c ...'` pa dobija kontrolni terminal (kontrola poslova, Ctrl+C). Zatvaranje prozora ubija ljusku i njene programe.
- `Terminal/FakeTerminalService.cs` (Windows pregled): mala izmišljena ljuska sa komandama `help ls colors unicode kubectl get pods ...`.
- `Views/TerminalControl.cs` crta ćelije; okviri i blokovi (U+2500–U+259F, osim dvostrukih linija) se crtaju linijama da se spajaju bez rupa.
- Prečice terminala: Ctrl+Shift+C kopira, Ctrl+Shift+V i Shift+Insert lepe, srednji klik lepi poslednji izbor, Shift+PageUp/PageDown i točkić skroluju istoriju. Prečice desktopa (`DesktopShortcuts`) terminal ne guta.

**Proveren** na Linuxu (Ubuntu 26.04 u WSL-u): bash, prompt u boji, `ls --color`, UTF-8, promena veličine (SIGWINCH), `jobs`, Ctrl+C, upit pozicije kursora, `less`, `vim`, `top` (alt ekran, povratak glavnog ekrana), `exit`, ubijanje programa pri zatvaranju. **Nije urađeno:** miš za programe (htop, k9s: režimi 1000/1006 se samo pamte), ponovno prelamanje redova pri promeni širine, OSC 8 (linkovi) i OSC 52, ligature. **Na Debianu** treba font: paket `cloudandbear-desktop` mora imati zavisnost `fonts-dejavu-core` (terminal traži "Cascadia Mono, DejaVu Sans Mono, Consolas"); na slici nema drugog GUI-ja, pa se font ne podrazumeva.

## Redosled izgradnje (predlog)

1. ~~Okvir: `WindowHost` + `WindowFrame` sa lažnim sadržajem~~ (gotovo, vodič koraci 1).
2. ~~Traka + pokretač + tastatura~~ (gotovo, koraci 2–4).
3. ~~Sistem i Mreža~~ (gotovo, servisi Linux + Fake).
4. ~~Terminal~~ (gotovo).
5. ~~Kontejneri i Kubernetes~~ (gotovo).
6. ~~`CloudAndBear.Device`: DRM domaćin (ulaz, kursor, servisi) i provera na Debianu u VM-u~~ (gotovo, vidi `docs/DEVICE.md`).
7. Podešavanja, zaključani ekran / prijava, instalacija; paket `.deb` za ovaj projekat; provera na laptopu.
