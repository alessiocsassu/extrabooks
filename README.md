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

Il primo avvio scarica le immagini e applica le migrazioni EF Core. Se stai aggiornando un database creato dalla versione precedente, segui prima la procedura di adozione dello schema descritta sotto. Il database deve superare il controllo di salute prima che parta l'API. Le porte sono accessibili solo dal PC locale.

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

## Swagger

Apri http://localhost:5152/swagger e premi **Authorize**. Inserisci soltanto il valore `API_KEY` del file `.env`: Swagger lo invia nell'header `X-Api-Key`. Puoi poi usare **Try it out** per provare CRUD, ricerca, filtri e ordinamento. Le operazioni di scrittura eseguite qui modificano i libri dell'applicazione.

La specifica OpenAPI è disponibile su http://localhost:5152/swagger/v1/swagger.json. Swagger è attivo in Development e nel Compose locale tramite `Swagger__Enabled=true`; altrove resta disattivato salvo configurazione esplicita. Le API continuano a richiedere la chiave anche quando Swagger è abilitato.

## Test

Con .NET SDK 9, esegui la suite rapida senza avviare MySQL o l'applicazione:

```powershell
dotnet test tests/ExtraBooks.Tests
```

Oppure usa il runner Docker:

```powershell
docker compose --profile tests run --build --rm tests
```

La suite rapida comprende 31 casi unitari e 7 test HTTP con `WebApplicationFactory`. Il client HTTP dei test unitari usa un handler finto. I test degli endpoint sostituiscono completamente il contesto MySQL con EF Core InMemory, con un archivio diverso per ogni host di test. Non leggono `.env`, non usano connection string esterne e non aprono connessioni al database dell'applicazione. Il container dei test viene eseguito senza rete e senza volumi dati.

I test verificano autenticazione, CRUD, validazione, filtri, ordinamento, gestione degli errori, Swagger e isolamento della persistenza. La suite rapida non richiede servizi esterni e nessun suo caso viene saltato per mancanza di MySQL. EF Core InMemory è una persistenza finta: questi test non verificano la traduzione SQL, la collation o i vincoli specifici di MySQL.

### Test realistici con MySQL

La suite separata richiede Docker attivo e .NET SDK 9:

```powershell
dotnet test tests/ExtraBooks.MySqlTests -c Release
```

Testcontainers crea un MySQL 8.4 temporaneo, con una porta assegnata dinamicamente, e lo rimuove al termine. La connessione viene ottenuta soltanto da quel container: la suite non legge `.env` e non accetta connection string esterne. I cinque test verificano schema e cronologia delle migrazioni, applicazione ripetuta senza perdita di dati, adozione dello schema precedente, rifiuto di schemi incompatibili, vincoli di lunghezza, CRUD e traduzione SQL di ricerca e ordinamenti. La ricerca con maiuscole e accenti verifica anche il comportamento della collation MySQL predefinita.

`dotnet test ExtraBooks.sln -c Release` esegue entrambe le suite e richiede quindi Docker. Se Docker non è disponibile, la suite MySQL fallisce esplicitamente: non viene saltata e non usa un database alternativo.

Prima della consegna sono stati verificati anche i flussi nel browser: inserimento e validazione, modifica, ricerca e ordinamento, persistenza dopo il ricaricamento, conferma e annullamento dell'eliminazione. Il layout è stato controllato a 320, 390, 768 e 1440 px. È stato simulato un errore di connessione all'API per verificare che il form conservi i dati. Questi controlli non fanno parte della suite di test inclusa nel repository.

## Migrazioni del database

La migrazione `InitialCreate` e lo snapshot del modello sono in `src/ExtraBooks.Api/Data/Migrations`. Per aggiungere una modifica allo schema:

```powershell
dotnet tool restore
dotnet ef migrations add NomeModifica --project src/ExtraBooks.Api
dotnet ef migrations has-pending-model-changes --project src/ExtraBooks.Api
```

Rivedi il codice generato prima di applicarlo. L'API esegue `MigrateAsync` all'avvio sul database configurato. In alternativa, imposta `ConnectionStrings__Default` nel terminale ed esegui `dotnet ef database update --project src/ExtraBooks.Api`. Non usare più `EnsureCreated` su MySQL; resta solo nei test con persistenza in memoria e nella simulazione dello schema precedente.

### Database della versione precedente

Uno schema creato con `EnsureCreated` non ha la cronologia EF. Non cancellare i libri e non eseguire la migrazione iniziale sopra le tabelle esistenti.

1. Ferma API e frontend con `docker compose stop api web`.
2. Esegui un backup del database con `mysqldump`, conservandolo fuori da Git.
3. Compila la nuova API e adotta lo schema:

```powershell
docker compose build api
docker compose run --rm --no-deps api --baseline-existing-database
docker compose up --build -d
```

Il comando controlla colonne, tipi, nullabilità, valori predefiniti, charset, indici e motore della tabella iniziale. Se corrispondono alla versione originaria registra `InitialCreate` nella cronologia, senza modificare i libri. Rifiuta database già migrati o schemi differenti: in quel caso occorre verificare lo schema e preparare un aggiornamento specifico. Per installazioni nuove non serve questo comando.

## Continuous Integration

Il workflow `.github/workflows/ci.yml` parte su push, pull request e avvio manuale. Non richiede `.env` o segreti del database dell'applicazione.

- **Build e test rapidi**: restore, build Release, verifica che il modello non abbia modifiche prive di migrazione e suite in memoria.
- **Integrazione MySQL**: esegue i test con Testcontainers su un MySQL temporaneo del runner.
- **Docker**: costruisce separatamente le immagini API e frontend, senza pubblicarle.

I risultati dei test vengono salvati come artifact TRX anche in caso di errore. I job hanno timeout espliciti e permessi di sola lettura sul repository. L'esecuzione su GitHub sarà verificabile nella scheda Actions dopo il push.

## Struttura e scelte

```text
src/
  ExtraBooks.Api/        Controller HTTP, servizio libri e accesso al database
  ExtraBooks.Web/        Pagina Blazor, form e client HTTP
  ExtraBooks.Contracts/  DTO, stati del libro e regole di validazione condivise
tests/
  ExtraBooks.Tests/      Test unitari e HTTP con persistenza finta
  ExtraBooks.MySqlTests/ Migrazioni e API con MySQL temporaneo
  Dockerfile            Ambiente di esecuzione dei test
```

- **ASP.NET Core 9 con controller**: gli endpoint gestiscono richieste e codici HTTP; `BookService` contiene le query e le operazioni sui libri. La separazione resta piccola e leggibile.
- **EF Core e Pomelo con MySQL 8.4**: mapping e query in C#, operazioni asincrone e letture senza tracking. Non c'è una repository aggiuntiva: per questo CRUD il `DbContext` è sufficiente.
- **Contracts condivisi**: frontend e API usano gli stessi DTO e vincoli di input. Le entità del database restano nell'API. Il mapping è esplicito perché c'è una sola entità.
- **Blazor Interactive Server**: il browser interagisce con il server Blazor, che chiama l'API tramite `HttpClient`. La chiave API rimane sul server. Il compromesso è la necessità di mantenere una connessione attiva per usare l'interfaccia.
- **Bootstrap 5.3.3**: griglia, form e pulsanti standard, con pochi stili per palette e adattamento della tabella su mobile. Il CSS Bootstrap è incluso nel progetto, senza CDN a runtime o build npm.
- **Una pagina Blazor**: elenco e form restano in `Home.razor`, senza introdurre componenti per ogni campo. È adeguato alle dimensioni attuali.
- **API key**: protegge gli endpoint `/api`, ma non è un login. L'interfaccia è pensata per un diario locale a singolo utente: chi accede al sito può modificare i libri.
- **Migrazioni EF Core**: schema e cronologia sono versionati. L'API applica le migrazioni all'avvio per semplificare la demo locale. Per un deployment con più istanze userei un passaggio di migrazione separato, prima di avviare i servizi.
- **Docker multi-stage**: SDK nella build, runtime nelle immagini finali e processi applicativi eseguiti senza root. Compose coordina database, API e frontend; i dati MySQL rimangono in un volume persistente.

## API

| Metodo | Percorso | Risposta |
| --- | --- | --- |
| GET | `/api/books?search=...&sort=title` | Elenco, ricerca su titolo/autore |
| GET | `/api/books/{id}` | Libro oppure 404 |
| POST | `/api/books` | 201 con libro e header Location |
| PUT | `/api/books/{id}` | 204 oppure 404 |
| DELETE | `/api/books/{id}` | 204 oppure 404 |

`sort=recent` è l'ordinamento predefinito. Sono disponibili anche `title`, `status`, `status-desc`, `rating` (voto decrescente) e `rating-asc` (voto crescente). I libri senza voto vengono sempre mostrati dopo quelli valutati; a parità si ordina per titolo. L'ordine degli stati è Da leggere, In lettura, Terminato. Il parametro facoltativo `status=ToRead`, `status=Reading` o `status=Finished` filtra lo stato e si combina con ricerca e ordinamento. Gli stati JSON sono `ToRead`, `Reading` e `Finished`. Titolo e autore sono obbligatori; il voto è facoltativo, da 1 a 5. La pagina attuale non può superare il totale, quando indicato. Gli input non validi restituiscono 400; la chiave assente o errata restituisce 401.

## Gestione degli errori

`ApiExceptionHandler` implementa `IExceptionHandler` e centralizza la conversione delle eccezioni in risposte `ProblemDetails`. `BookService` solleva `BookNotFoundException`, derivata da `DomainException`, quando un libro richiesto non esiste. L'handler restituisce 404; i controller gestiscono soltanto il percorso di successo.

Gli errori inattesi restituiscono 500 con un messaggio generico. I dettagli tecnici vengono registrati nei log e la risposta include un `traceId` per collegarla alla richiesta. Le eccezioni non contengono codici HTTP: la traduzione resta nell'API. Gli errori di configurazione o connessione durante l'avvio fermano l'applicazione e restano nei log, perché la pipeline HTTP non è ancora attiva.

La validazione degli input usa `ValidationProblemDetails` con stato 400, senza eccezioni dedicate. Il middleware della chiave restituisce `ProblemDetails` con stato 401. Nel frontend, `BooksApiClient` trasforma le risposte HTTP fallite in `ApiException`: Blazor mostra gli errori di validazione e messaggi specifici per accesso negato, libro mancante o servizio indisponibile. Il form conserva i dati in caso di errore; timeout e problemi di rete mantengono messaggi dedicati.

## Cosa migliorerei con più tempo

- Backup pianificati e una procedura di ripristino verificata.
- Test del browser per form, conferma eliminazione, errori di rete e layout mobile.
- Associare gli errori di validazione restituiti dall'API ai singoli campi del form.
- Paginazione e ricerca mantenuta nell'URL per elenchi più grandi.
- Separazione del form dalla pagina quando crescono le funzioni.
- Autenticazione e dati separati per utente, HTTPS e gestione dei segreti se il progetto viene pubblicato.
- Aggiornamento a .NET LTS e controllo delle dipendenze nella pipeline.

## Utilizzo di strumenti AI

Ho utilizzato ChatGPT/Codex come supporto alla struttura iniziale, all'implementazione, alla revisione del codice, ai test e alla documentazione. Il supporto ha incluso anche la configurazione Docker e le revisioni dell'interfaccia.
