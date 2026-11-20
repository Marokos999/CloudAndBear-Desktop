# CloudAndBear Desktop

Avalonia 12 / .NET 10 desktop za CloudAndBear (Debian, bez ikakvog drugog GUI-ja). Ovde se dizajnira ceo izgled; sistem (slika, paket, servis) je u repozitorijumu `CloudAndBearLinux`.

## Struktura

| Folder | Šta je |
|---|---|
| `src/CloudAndBear.Ui` | ceo izgled: tema (`Theme/`), pogledi (`Views/`), `App`. Ovde se radi dizajn. |
| `src/CloudAndBear.Preview` | prozor za pregled na PC-u (Windows/Linux) |
| `tools/CloudAndBear.Snapshot` | iscrtava pogled u PNG bez prozora (za proveru i review) |
| `src/CloudAndBear.Device` | DRM domaćin za Debian: ulaz (tastatura, miš), raspored tastature, pravi servisi (vidi `docs/DEVICE.md`) |
| `deploy/` | servis, nalog, PAM i polkit pravila, `install.sh` za uređaj |

## Pokretanje

```powershell
dotnet run --project src/CloudAndBear.Preview
dotnet run --project src/CloudAndBear.Preview -- --size=1920x1080    # veličina prozora, F11 = ceo ekran
dotnet run --project tools/CloudAndBear.Snapshot -- snapshots 1920x1080 1366x768
```

## Pravila dizajna

1. **Jedan korenski pogled** (`DesktopView`), bez dodatnih prozora: na uređaju nema prozora (DRM, jedan ekran). Dijalozi i meniji su preklopi unutar pogleda.
2. **Sve vrednosti iz tokena** (`Theme/Tokens.axaml`): pogledi koriste `{DynamicResource ...}`, nikad ručno upisane boje i veličine.
3. **Izgled odvojen od logike**: `.axaml` pogledi ne sadrže logiku; stanje i radnje idu u ViewModel-e, sistemski pozivi u servise.
4. **Proveri na oba ekrana**: 1920x1080 i 1366x768 (laptop), bez miša (strelice + Enter) i sa mišem.
5. Fluent kontrole prate naše boje preko palete u `App.axaml`; `Accent` i `Background` tamo moraju biti isti kao u tokenima.

## Trenutno stanje

Desktop sa prozorima, trakom, pokretačem i tastaturom, aplikacijama Sistem, Mreža, Terminal, Kontejneri i Kubernetes (servisi: pravi na Linuxu, izmišljeni u pregledu na PC-u) i domaćinom za Debian proveren u VM-u. Model i odluke su u `docs/DESKTOP.md`, uređaj u `docs/DEVICE.md`, istorija građenja u `docs/VODIC.md`. Boje i raspored su privremeni: pravi izgled još nije dizajniran.
