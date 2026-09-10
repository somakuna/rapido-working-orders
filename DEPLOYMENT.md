# Deployment — Rapido Working Orders

Distribucija ide preko **[Velopack](https://velopack.io/)** (`vpk` CLI), a paketi se objavljuju kao
**GitHub Release** na `somakuna/rapido-working-orders`. Instalirane aplikacije se same
ažuriraju iz tih releaseova.

> **Nema build skripte u repou — postupak je ručan.** Ovaj dokument je izvor istine.

---

## 1. Preduvjeti

### Na razvojnom stroju

| Alat | Verzija | Provjera |
|------|---------|----------|
| .NET SDK | 8.0.x | `dotnet --version` |
| `vpk` (Velopack CLI) | 1.2.0 | `dotnet tool list -g` |
| `Velopack` NuGet paket | 1.2.0 — **ista kao CLI** | `RapidoWorkingOrders.csproj` |
| GitHub PAT | scope `repo` | `.env` u korenu repoa |

Instalacija / update `vpk` alata:

```powershell
dotnet tool install -g vpk
dotnet tool update  -g vpk
```

### Na korisničkom računalu

Build je **framework-dependent** (`--no-self-contained`), pa korisnik **mora imati instaliran
[.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0)**.
Runtime je Desktop varijanta, ne obična — aplikacija je WPF.

Ovo je namjeran izbor: self-contained build digne `nupkg` s ~15 MB na preko 70 MB, što
usporava svaki update svakom korisniku.

---

## 2. Postupak za novu verziju

Primjer u nastavku objavljuje verziju `1.0.5`. Zamijeni broj gdje god se pojavi.

### Korak 1 — Podigni verziju

U [RapidoWorkingOrders.csproj](RapidoWorkingOrders.csproj):

```xml
<Version>1.0.5</Version>
```

Velopack traži rastuću SemVer verziju. Verzija iz `csproj` završi i u naslovu glavnog
prozora ([MainWindow.xaml.cs](Views/MainWindow.xaml.cs)).

### Korak 2 — Očisti publish folder

Zaostale datoteke iz prethodnog builda završe u paketu ako se folder ne očisti:

```powershell
Get-ChildItem publish\win-x64 -Force -Recurse | Remove-Item -Force -Recurse
```

> `Remove-Item 'publish\win-x64\*'` ne prolazi kroz harness — koristi gornji oblik.

### Korak 3 — Publish

```powershell
dotnet publish RapidoWorkingOrders.csproj -c Release -r win-x64 `
  --no-self-contained `
  -p:PublishSingleFile=false `
  -p:PublishReadyToRun=false `
  -o publish\win-x64
```

**Sva tri flaga su obavezna.** Bez njih `nupkg` naraste s ~15 MB na ~33 MB, jer Velopack
ne može dijeliti nepromijenjene datoteke između verzija (delta update prestaje raditi).

Publish profil [Properties/PublishProfiles/win-x64.pubxml](Properties/PublishProfiles/win-x64.pubxml)
već ima ispravne postavke (od v1.0.4), pa je ekvivalentno i:

```powershell
dotnet publish RapidoWorkingOrders.csproj -p:PublishProfile=win-x64
```

### Korak 4 — Pack

```powershell
vpk pack -u RapidoWorkingOrders -v 1.0.5 `
  -p publish\win-x64 `
  -e RapidoWorkingOrders.exe `
  -i Resources\logo.ico `
  --packTitle "Rapido Working Orders" `
  --packAuthors Rapido
```

Rezultat u `Releases\` (channel je default `win`):

| Datoteka | Namjena |
|----------|---------|
| `RapidoWorkingOrders-1.0.5-full.nupkg` | Puni paket — izvor za auto-update |
| `RapidoWorkingOrders-1.0.5-delta.nupkg` | Delta u odnosu na prethodnu verziju (samo ako je stari paket u `Releases\`) |
| `RapidoWorkingOrders-win-Setup.exe` | Instalater za nove korisnike |
| `RapidoWorkingOrders-win-Portable.zip` | Portable verzija |
| `RELEASES`, `releases.win.json`, `assets.win.json` | Manifesti koje čita `UpdateManager` |

> `vpk pack` gradi delta pakete iz onoga što zatekne u `Releases\`. Ako je folder prazan,
> dobiješ samo full paket — korisnici tada skidaju cijelih ~15 MB umjesto par stotina KB.
> **Nemoj brisati `Releases\` prije packa.**

### Korak 5 — Commit i tag

```powershell
git add -A
git commit -m "v1.0.5 - <kratki opis promjena>"
git tag v1.0.5
git push origin main
git push origin v1.0.5
```

`Releases\` i `publish\` su u [.gitignore](.gitignore) — paketi ne idu u repo, samo na Release.

### Korak 6 — Upload na GitHub

Token stoji u `.env` u korenu repoa (gitignoriran). Učitaj ga u sesiju:

```powershell
Get-Content .env | Where-Object { $_ -match '^\s*[^#\s]' } | ForEach-Object {
    $k, $v = $_ -split '=', 2
    Set-Item "env:$($k.Trim())" $v.Trim()
}
```

Pa upload:

```powershell
vpk upload github `
  --repoUrl https://github.com/somakuna/rapido-working-orders `
  --token $env:GH_PAT `
  --publish `
  --tag v1.0.5 `
  --releaseName "Rapido Working Orders 1.0.5"
```

`--publish` objavljuje release odmah (bez njega ostaje draft i **klijenti ga neće vidjeti**).

---

## 3. Kako izgleda update kod korisnika

Provjera se pokreće pri startu glavnog prozora, u
[`MainWindow.CheckForUpdatesAsync()`](Views/MainWindow.xaml.cs):

```csharp
var mgr = new UpdateManager(new GithubSource("https://github.com/somakuna/rapido-working-orders", null, false));
var update = await mgr.CheckForUpdatesAsync();
```

Tijek:

1. `UpdateManager` čita zadnji GitHub Release i uspoređuje verzije.
2. Ako postoji novija, paket se **preuzima u pozadini**.
3. Korisnik dobije `MessageBox` s ponudom restarta.
4. Na potvrdu → `ApplyUpdatesAndRestart()`; na odbijanje → update se primijeni pri sljedećem pokretanju.

Cijeli blok je u `try/catch` koji šuti — ako GitHub nije dostupan, aplikacija normalno radi.

`GithubSource(..., null, false)` znači: bez tokena (javni repo), bez pre-release verzija.
Ako repo ikad postane privatan, ovdje treba token.

Velopack bootstrapper se inicijalizira **prvom linijom** `Main()` u [Program.cs](Program.cs):

```csharp
VelopackApp.Build().Run();
```

Ta linija mora ostati prije svega ostalog — obrađuje install/update/uninstall hookove.

---

## 4. Provjera prije objave

- [ ] `<Version>` podignut i veći od prethodnog
- [ ] `publish\win-x64` očišćen prije publisha
- [ ] `nupkg` je **~15 MB**, ne 33 MB+ (ako je veći → provjeri flagove iz Koraka 3)
- [ ] `Releases\` sadrži i delta paket (osim za prvi release u praznom folderu)
- [ ] Setup.exe se instalira i pokreće na čistom stroju s .NET 8 Desktop Runtime
- [ ] Release na GitHubu je **published**, ne draft
- [ ] Instalirana starija verzija ponudi update unutar par sekundi od pokretanja

---

## 5. Gotchas

**`vpk` nije na PATH-u u neinteraktivnim ljuskama** (skripte, CI, agentski terminali) —
`dotnet tool` PATH unos se učita tek u interaktivnoj sesiji. Zovi ga punom putanjom:

```powershell
$vpk = Join-Path $env:USERPROFILE ".dotnet\tools\vpk.exe"
& $vpk pack -u RapidoWorkingOrders -v 1.0.5 ...
```

**Git push preko HTTPS traži token u ispravnom formatu.**
Goli PAT kao username daje `Password authentication not supported`. Koristi:

```
https://x-access-token:<PAT>@github.com/somakuna/rapido-working-orders.git
```

**Velopack NuGet paket i `vpk` CLI drži na istoj verziji.** Od v1.0.5 su oba `1.2.0`.
Kad podižeš jedno, podigni i drugo u istom commitu:

```powershell
dotnet tool update -g vpk
dotnet add package Velopack --version <ista verzija>
```

Nakon takvog podizanja **testiraj update sa stare instalirane verzije**, ne samo čistu
instalaciju — regresije se pokazuju upravo na putu stara → nova.

**Ne commitaj PAT.** Token je u `.env` u korenu repoa, koji `*.env` u [.gitignore](.gitignore)
drži izvan gita. Provjera da nigdje nije procurio:

```powershell
git grep -I "ghp_"    # ne smije vratiti ništa
```

Ako token ikad završi u commitu, logu ili chatu — **odmah ga povuci** na
GitHub → Settings → Developer settings → Personal access tokens, generiraj novi
i zamijeni vrijednost u `.env`.

**Verzija u tagu i u `csproj` moraju se poklapati.** `vpk upload` veže assete uz tag; ako se
razilaze, klijent vidi jednu verziju a skine drugu.

**Prvi start nakon instalacije traži MySQL postavke.** Novo računalo nema
`%USERPROFILE%\.rapido_working_orders.json`, pa se otvara dijalog za konekciju.
Vidi [README.md](README.md#konfiguracija).
