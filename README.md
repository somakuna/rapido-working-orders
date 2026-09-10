# Rapido Working Orders

WPF desktop aplikacija (.NET 8) za vođenje radnih naloga u tiskari. Nalozi se drže u
zajedničkoj MySQL bazi, PDF-ovi se ubacuju drag & dropom uz automatsko očitavanje broja
stranica i kvadrature, a nalog se ispisuje kao PDF u A4 ili A5 formatu.

**Za objavu nove verzije vidi [DEPLOYMENT.md](DEPLOYMENT.md).**

---

## Sadržaj

- [Tehnologije](#tehnologije)
- [Pokretanje iz izvornog koda](#pokretanje-iz-izvornog-koda)
- [Konfiguracija](#konfiguracija)
- [Struktura projekta](#struktura-projekta)
- [Model podataka](#model-podataka)
- [Ekrani i funkcionalnosti](#ekrani-i-funkcionalnosti)
- [Izračun kvadrature](#izračun-kvadrature)
- [Tipkovničke kratice](#tipkovničke-kratice)
- [Auto-update](#auto-update)

---

## Tehnologije

| Sloj | Izbor |
|------|-------|
| UI | WPF, `net8.0-windows`, XAML + code-behind |
| Baza | MySQL 8 preko EF Core 8 (`Pomelo.EntityFrameworkCore.MySql`) |
| PDF izlaz | QuestPDF 2024.10.3 (Community licenca) |
| PDF ulaz | PdfPig 0.1.9 — čita broj stranica i dimenzije |
| Distribucija | Velopack 1.2.0 + `vpk` CLI 1.2.0 (verzije moraju biti iste) |

Aplikacija namjerno **nema MVVM framework ni DI kontejner**. Logika je u code-behindu
dijaloga, a `AppDbContext` se instancira po potrebi u `using` bloku. Za veličinu projekta
to je namjeran izbor — nemoj uvoditi apstrakcije bez konkretnog razloga.

---

## Pokretanje iz izvornog koda

```powershell
git clone https://github.com/somakuna/rapido-working-orders.git
cd rapido-working-orders
dotnet run
```

Preduvjeti: **.NET 8 SDK** i pristup MySQL 8 serveru.

Pri prvom pokretanju aplikacija sama kreira sve tablice (`EnsureCreated()`) i ubaci
default opcije završne obrade (*Montaža*, *Vanjska usluga*).

---

## Konfiguracija

Podaci za spajanje čuvaju se **izvan repoa**, u JSON datoteci u korisničkom profilu:

```
%USERPROFILE%\.rapido_working_orders.json
```

```json
{
  "Server": "192.168.1.10",
  "Port": 3306,
  "Database": "rapido",
  "Username": "rapido",
  "Password": "..."
}
```

Datoteka se stvara i uređuje kroz **MySQL postavke** (`Ctrl+M`) — nema potrebe za ručnim
editiranjem. Ako konekcija ne uspije pri startu, [App.xaml.cs](App.xaml.cs) otvori dijalog
za postavke prije nego što se pokaže glavni prozor.

> Lozinka se sprema u plain textu, a connection string koristi `SslMode=None`. Prihvatljivo
> za zatvorenu mrežu tiskare; **ne izlaži MySQL na internet.**

### Identifikacija korisnika

Nema logina. Aplikacija čita `Environment.UserName` (Windows nalog) i pri prvom pokretanju
sama upiše korisnika u tablicu `users`. Ime za prikaz na nalozima može se postaviti kao
alias u MySQL postavkama (`show_name`).

### `.env` — tajne za release

Odvojeno od gornje konfiguracije, u korenu repoa stoji `.env` s **GitHub PAT-om za objavu
verzija** (`GH_PAT`). Aplikacija ga ne čita — koristi ga samo `vpk upload` iz
[DEPLOYMENT.md](DEPLOYMENT.md#korak-6--upload-na-github). Datoteka je izvan gita preko
`*.env` u [.gitignore](.gitignore) i **ne smije se commitati**.

---

## Struktura projekta

```
Program.cs             Ulazna točka — VelopackApp.Build().Run() mora biti prva linija
App.xaml.cs            Startup: config, konekcija, seed, global exception handler
Data/
  AppDbContext.cs      EF Core kontekst, relacije, NextWorkNumber(), FindKeywordMatch()
Models/                EF entiteti, 1:1 s tablicama
ViewModels/
  WorkFileRow.cs       Red u tablici stavki — INotifyPropertyChanged + Recalc()
  FinishingOptionCheck.cs
Views/
  MainWindow.xaml      Lista naloga, filter, toolbar
Dialogs/
  WorkFormDialog       Unos/ispravak naloga — glavni ekran aplikacije
  PrintPreviewDialog   Izbor A4/A5, otvori ili spremi PDF
  ClientsDialog        CRUD klijenata
  CatalogDialog        Materijali, tehnologije, završne obrade, keywordovi
  DbSettingsDialog     MySQL konekcija + aliasi korisnika
Services/
  AppConfig.cs             Load/Save JSON konfiguracije
  PdfReaderService.cs      PdfPig -> (broj stranica, m2)
  WorkOrderPdfGenerator.cs QuestPDF -> radni nalog A4/A5
Resources/Theme.xaml   Zajednički stilovi
```

---

## Model podataka

Tablice se kreiraju automatski iz EF modela. Nema migracija — shema se mijenja kroz
atribute na entitetima.

| Tablica | Sadržaj | Ključne veze |
|---------|---------|--------------|
| `works` | Radni nalog | `client_id` -> `clients`, `user_id` -> `users` (oba **SET NULL**) |
| `files` | Stavke naloga (PDF ili ručni red) | `work_id` -> `works` (**CASCADE**), `technology_id`, `material_id` (SET NULL) |
| `clients` | Klijenti (naziv, OIB, adresa, kontakt) | <- `works` |
| `users` | Operateri, po Windows imenu | `windows_name` UNIQUE |
| `materials`, `technologies` | Šifrarnici | `name` UNIQUE |
| `keywords` | Pravila za automatsko prepoznavanje | -> materijal i/ili tehnologija |
| `finishing_options` | Opcije završne obrade + redoslijed | — |
| `work_finishing_options` | Odabrane obrade po nalogu (M:N) | CASCADE na obje strane |

**Broj naloga** je par `(work_number, work_year)` s UNIQUE indeksom `uniq_work_year`.
Sljedeći slobodni broj daje `AppDbContext.NextWorkNumber(year)` (`MAX + 1` unutar godine),
a duplikat se hvata prije spremanja uz poruku korisniku.

> **Iznimka od `EnsureCreated()`:** stupac `works.file_location` dodan je nakon što su baze
> već bile u produkciji, pa ga `AppDbContext.EnsureFileLocationColumn()` dodaje ručnim
> `ALTER TABLE` pri svakom startu. Isti obrazac koristi za buduće stupce na postojećim
> bazama — `EnsureCreated()` **ne** mijenja tablice koje već postoje.

---

## Ekrani i funkcionalnosti

### Glavni prozor — lista naloga

Prikazuje **zadnjih 500** naloga, sortiranih po godini pa broju silazno.

- **Filter** radi server-side (`WHERE` na bazi), pa je ispravan i kad ima 100 000 naloga.
  Unos se debounce-a 300 ms. Numerički unos traži broj naloga ili godinu; tekst traži po
  klijentu, operateru, lokaciji i isporuci. Ako upit padne, pada na klijentski filter nad
  učitanih 500.
- **Auto-refresh** svakih 30 s — usporedi `MAX(id)` i osvježi ako je netko drugi dodao nalog.
- Nalozi se otvaraju **nemodalno** (`Show()`), pa može biti otvoreno više naloga odjednom.

### Unos radnog naloga (`WorkFormDialog`)

Tri načina otvaranja: **novi**, **ispravak**, **dupliciranje** (kopira stavke i završne
obrade u novi broj naloga).

Stavke se dodaju na tri načina:

1. **Drag & drop PDF-ova** na tablicu
2. **Uvoz PDF-ova** kroz dijalog (`Ctrl+I` / `F5`)
3. **Ručni red** (`Ctrl+N` / `F4`) — za stavke koje nisu PDF

Kod uvoza PDF-a aplikacija automatski popuni broj stranica i m², te kroz
`FindKeywordMatch()` pokuša pogoditi materijal i tehnologiju iz **naziva datoteke** —
ako naziv sadrži neki keyword iz šifrarnika, preuzimaju se njegov materijal i tehnologija.

Spremanje kod ispravka radi **brisanje i ponovni upis** svih stavki naloga, ne diff.

### Ispis

`PrintPreviewDialog` nudi **A4** ili **A5**, pa *Otvori* (generira u temp i otvori u default
PDF čitaču) ili *Spremi kao*. Nalog sadrži zaglavlje s brojem/datumom/klijentom/operaterom,
logo, podatke o narudžbi i isporuci, odabrane završne obrade i tablicu stavki s totalima.

---

## Izračun kvadrature

`PdfReaderService` mjeri **svaku stranicu zasebno** i zbraja, pa višestranični PDF s
različitim formatima daje točan ukupan m².

```
m2 po stavci = suma (sirina_pt * 0.0254/72) * (visina_pt * 0.0254/72)   -> zaokruzeno na 4 decimale
Uk. m2       = m2 * Kolicina                                            -> zaokruzeno na 4 decimale
```

**Prikaz:** `m²` se prikazuje na **4 decimale**, `Uk.m²` i totali na **3 decimale**
(u tablici naloga i u ispisanom PDF-u). Stupci `files.m2 decimal(10,4)` i
`files.total_m2 decimal(12,4)` drže punu preciznost.

Četiri decimale su nužne zbog sitnih formata. Naljepnica 50×50 mm ima 0.0025 m²:

| | 4 decimale (trenutno) | 2 decimale (do v1.0.4) |
|---|---|---|
| m² po komadu | `0.0025` | `0.00` — kriv |
| × 100 kom | `0.250` | `0.000` — kriv |

Zaokruživanje na 3 decimale ovdje **nije dovoljno** — `0.0025` bi postalo `0.003`, pa bi
100 komada dalo 0.300 m² umjesto točnih 0.250 (odstupanje od 20%).

Ako se ikad mijenja preciznost, mijenjaju se sva četiri mjesta zajedno:
[PdfReaderService.cs](Services/PdfReaderService.cs) (`Math.Round`),
[WorkFileRow.cs](ViewModels/WorkFileRow.cs) (`Recalc()`),
[WorkFormDialog.xaml](Dialogs/WorkFormDialog.xaml) (`StringFormat`) i
[WorkOrderPdfGenerator.cs](Services/WorkOrderPdfGenerator.cs) (`ToString("F…")`).

---

## Tipkovničke kratice

### Glavni prozor

| Kratica | Radnja |
|---------|--------|
| `Ctrl+N` | Novi nalog |
| `Ctrl+D` | Dupliciraj odabrani |
| `Ctrl+P` | Print odabranog |
| `Ctrl+K` | Klijenti |
| `Ctrl+T` | Šifrarnici (postavke) |
| `Ctrl+M` | MySQL postavke |
| `F5` | Osvježi listu |
| `Enter` / dvoklik | Uredi odabrani |
| `Delete` | Obriši odabrani (uz potvrdu) |

### Radni nalog

| Kratica | Alternativa | Radnja |
|---------|-------------|--------|
| `Ctrl+S` | `F2` | Spremi |
| `Ctrl+P` | `F3` | Spremi i printaj |
| `Ctrl+N` | `F4` | Dodaj prazan red |
| `Ctrl+I` | `F5` | Uvezi PDF-ove |
| `Ctrl+D` | `F6` | Dupliciraj odabrani red |
| — | `F7` | Obriši odabrane redove |
| — | `F1` | Odustani / zatvori |

`Delete` briše odabrane redove kad tablica **nije** u modu uređivanja ćelije.

---

## Auto-update

Aplikacija pri svakom pokretanju provjerava GitHub Releases i, ako postoji novija verzija,
preuzme je u pozadini i ponudi restart. Detalji i postupak objave: **[DEPLOYMENT.md](DEPLOYMENT.md)**.

Instalacija je framework-dependent — korisničko računalo mora imati
**[.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0)**.
