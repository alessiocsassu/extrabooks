# ExtraBooks

Diario di lettura in Blazor: inserimento, modifica ed eliminazione dei libri, ricerca per titolo o autore e ordinamento. Per ogni libro si possono salvare stato, pagine lette, voto, note e data di completamento.

## Avvio con Docker

Serve Docker Desktop con supporto ai container Linux e Docker Compose. Non occorre installare .NET o MySQL.

Dalla cartella del progetto, in PowerShell:

```powershell
# Solo al primo avvio: non sovrascrivere un .env già configurato.
Copy-Item .env.example .env
```

Modifica `.env` impostando `API_KEY`, `MYSQL_PASSWORD` e `MYSQL_ROOT_PASSWORD`. Per le password locali usa stringhe lunghe di lettere e numeri, evitando i separatori delle connection string. Il file è escluso da Git e dal contesto di build Docker.

```powershell
docker compose up --build -d
docker compose ps
```

- Sito: http://localhost:5174
- API: http://localhost:5152/api/books, con header `X-Api-Key`
- Controllo avvio API: http://localhost:5152/health
- MySQL locale: `127.0.0.1:3307`, database e utente `extrabooks`

Il primo avvio scarica le immagini e crea le tabelle. Il database deve superare il controllo di salute prima che parta l'API. Le porte sono accessibili solo dal PC locale.

```powershell
# Log dei servizi
docker compose logs --tail 100 api web db

# Arresto: i libri restano nel volume mysql_data
docker compose down
```

`docker compose down -v` elimina anche i dati: non usarlo per risolvere problemi di accesso.

### Se MySQL restituisce Access denied

Le variabili `MYSQL_PASSWORD` e `MYSQL_ROOT_PASSWORD` inizializzano gli account solo quando il volume è vuoto. Modificare `.env` non cambia le password di un database esistente. Ripristina i valori originali oppure aggiorna gli account con `ALTER USER` usando le credenziali amministrative, poi riallinea `.env` e ricrea i container. Il controllo di salute esegue una query con l'utente applicativo, quindi rileva anche password errate.

## Avvio senza container applicativi

Servono .NET SDK 9 e MySQL 8.4. Puoi usare il database Docker:

```powershell
docker compose up -d db
```

Apri due terminali PowerShell nella cartella del progetto. In quello dell'API:

```powershell
$env:ApiKey = 'valore-di-API_KEY-in-env'
$env:ConnectionStrings__Default = 'Server=127.0.0.1;Port=3307;Database=extrabooks;User=extrabooks;Password=valore-di-MYSQL_PASSWORD;'
dotnet run --project src/ExtraBooks.Api
```

In quello del frontend:

```powershell
$env:Api__Key = 'valore-di-API_KEY-in-env'
dotnet run --project src/ExtraBooks.Web
```

Gli indirizzi sono gli stessi dell'avvio Docker. `dotnet run` non carica automaticamente il file `.env`.

## Test

Con .NET SDK 9, per i soli test unitari:

```powershell
dotnet test ExtraBooks.sln --filter FullyQualifiedName~SaveBookRequestTests
```

Per l'intera suite, inclusi i test di integrazione con MySQL reale:

```powershell
docker compose --profile tests run --build --rm tests
# Ferma e rimuovi solo il database temporaneo dei test
docker compose --profile tests rm --stop --force db-test
```

Il profilo `tests` usa un database separato, senza porte pubblicate e con dati temporanei in memoria. Non usa il volume dei libri. Le credenziali presenti nel profilo valgono soltanto per questo database.

La suite comprende 24 casi unitari sulla validazione e 3 test di integrazione. Questi ultimi avviano l'API con `WebApplicationFactory` e verificano autenticazione, CRUD, normalizzazione degli input, ricerca, ordinamento, risposte 400/404 e persistenza su MySQL. I dati creati vengono rimossi a fine test.

Eseguendo `dotnet test` senza `TEST_MYSQL_CONNECTION_STRING`, i test di integrazione risultano esplicitamente saltati. Per eseguirli fuori da Docker, la variabile deve indicare un database MySQL dedicato ai test, con permessi per creare le tabelle.

Prima della consegna sono stati verificati anche i flussi nel browser: inserimento e validazione, modifica, ricerca e ordinamento, persistenza dopo il ricaricamento, conferma e annullamento dell'eliminazione. Il layout è stato controllato a 320, 390, 768 e 1440 px. È stato simulato un errore di connessione all'API per verificare che il form conservi i dati. Questi controlli non fanno parte della suite di test inclusa nel repository.

## Struttura e scelte

```text
src/
  ExtraBooks.Api/        Controller HTTP, servizio libri e accesso al database
  ExtraBooks.Web/        Pagina Blazor, form e client HTTP
  ExtraBooks.Contracts/  DTO, stati del libro e regole di validazione condivise
tests/
  ExtraBooks.Tests/      Test unitari e di integrazione
  Dockerfile            Ambiente di esecuzione dei test
```

- **ASP.NET Core 9 con controller**: gli endpoint gestiscono richieste e codici HTTP; `BookService` contiene le query e le operazioni sui libri. La separazione resta piccola e leggibile.
- **EF Core e Pomelo con MySQL 8.4**: mapping e query in C#, operazioni asincrone e letture senza tracking. Non c'è una repository aggiuntiva: per questo CRUD il `DbContext` è sufficiente.
- **Contracts condivisi**: frontend e API usano gli stessi DTO e vincoli di input. Le entità del database restano nell'API. Il mapping è esplicito perché c'è una sola entità.
- **Blazor Interactive Server**: il browser interagisce con il server Blazor, che chiama l'API tramite `HttpClient`. La chiave API rimane sul server. Il compromesso è la necessità di mantenere una connessione attiva per usare l'interfaccia.
- **Bootstrap 5.3.3**: griglia, form e pulsanti standard, con pochi stili per palette e adattamento della tabella su mobile. Il CSS Bootstrap è incluso nel progetto, senza CDN a runtime o build npm.
- **Una pagina Blazor**: elenco e form restano in `Home.razor`, senza introdurre componenti per ogni campo. È adeguato alle dimensioni attuali.
- **API key**: protegge gli endpoint `/api`, ma non è un login. L'interfaccia è pensata per un diario locale a singolo utente: chi accede al sito può modificare i libri.
- **Schema con `EnsureCreatedAsync`**: permette il primo avvio senza comandi manuali. Non aggiorna schemi esistenti; un'evoluzione del modello richiederà migrazioni e un percorso di aggiornamento dei dati.
- **Docker multi-stage**: SDK nella build, runtime nelle immagini finali e processi applicativi eseguiti senza root. Compose coordina database, API e frontend; i dati MySQL rimangono in un volume persistente.

## API

| Metodo | Percorso | Risposta |
| --- | --- | --- |
| GET | `/api/books?search=...&sort=title` | Elenco, ricerca su titolo/autore |
| GET | `/api/books/{id}` | Libro oppure 404 |
| POST | `/api/books` | 201 con libro e header Location |
| PUT | `/api/books/{id}` | 204 oppure 404 |
| DELETE | `/api/books/{id}` | 204 oppure 404 |

`sort=recent` è l'ordinamento predefinito. Gli stati JSON sono `ToRead`, `Reading` e `Finished`. Titolo e autore sono obbligatori; il voto è facoltativo, da 1 a 5. La pagina attuale non può superare il totale, quando indicato. Gli input non validi restituiscono 400; la chiave assente o errata restituisce 401.

## Cosa migliorerei con più tempo

- Migrazioni EF Core e una procedura di backup e aggiornamento del database.
- Test del browser per form, conferma eliminazione, errori di rete e layout mobile.
- Messaggi per i singoli errori di validazione restituiti dall'API, evitando il solo errore generico di salvataggio.
- Paginazione, filtro per stato e ricerca mantenuta nell'URL per elenchi più grandi.
- Separazione del form dalla pagina quando crescono le funzioni.
- Autenticazione e dati separati per utente, HTTPS e gestione dei segreti se il progetto viene pubblicato.
- Aggiornamento a .NET LTS e pipeline CI per build e suite con MySQL.

## Utilizzo di strumenti AI

Ho utilizzato ChatGPT/Codex come supporto alla struttura iniziale, all'implementazione, alla revisione del codice, ai test e alla documentazione. Il supporto ha incluso anche la configurazione Docker e le revisioni dell'interfaccia.
