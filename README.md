# CycliqueShareTracker - MVP 3

MVP personnel en **.NET / ASP.NET Core** pour analyser l'action **TotalEnergies (Euronext Paris)** avec une approche simple et cyclique.

## Objectifs couverts

- Ingestion journalière des prix (provider principal Yahoo Finance + fallback Alpha Vantage)
- Stockage PostgreSQL
- Calcul indicateurs: SMA50, SMA200, RSI14, drawdown 52 semaines
- Score d'entrée sur 100 + signal textuel (`NO BUY`, `WATCH`, `BUY ZONE`)
- Score de sortie sur 100 + signal textuel (`HOLD`, `TRIM / TAKE PROFIT`, `SELL ZONE`) + raison principale
- Tooltips explicatifs au survol des signaux (dashboard + historique) avec détail des facteurs de score (validés/non validés) et raison métier principale
- Dashboard web privé (auth cookie + mot de passe unique via variable d'environnement)
- Graphique dashboard `Close + SMA50 + SMA200` (avec marqueurs `BUY ZONE` et `SELL ZONE`)
- Page protégée **Historique des signaux** (date, close, SMA50, SMA200, RSI14, drawdown, entry score/signal, exit score/signal, raison sortie)
- Historique des signaux en base
- Planification quotidienne + déclenchement manuel
- Docker + Docker Compose prêts pour VPS OVH Linux

## Architecture

Monorepo en couches:

- `src/CycliqueShareTracker.Web` : UI MVC, auth, endpoints, background job
- `src/CycliqueShareTracker.Application` : interfaces et logique métier (indicateurs, score/signal, orchestration)
- `src/CycliqueShareTracker.Domain` : entités métier
- `src/CycliqueShareTracker.Infrastructure` : providers de données, fallback, repositories, EF Core/PostgreSQL
- `tests/CycliqueShareTracker.Tests` : tests unitaires moteur score + indicateurs + logique provider/fallback

## Providers de données de marché

### Pourquoi Stooq a été remplacé

Le provider Stooq CSV a été retiré pour améliorer:

- la robustesse (fallback explicite entre providers)
- la maintenabilité (contrat commun et validation uniforme)
- l'évolutivité (changement de provider via configuration)

### Providers utilisés maintenant

- **Provider principal**: `YahooFinance`
- **Provider fallback**: `AlphaVantage`

Flux d'exécution:
1. tentative sur provider principal
2. si échec HTTP, exception, réponse vide ou données invalides/incomplètes: bascule fallback
3. si les deux échouent: log d'erreur explicite, aucune corruption des données existantes

### Modèle de données commun

Tous les providers retournent le même modèle journalier:

- `symbol` (symbole demandé/mappé)
- `date`
- `open`
- `high`
- `low`
- `close`
- `adjusted close` (si disponible)
- `volume`

Le reste du pipeline (stockage, indicateurs, signaux, dashboard) ne dépend pas du format natif Yahoo/Alpha Vantage.

### Configuration providers

Variables clés:

- `MarketData__PrimaryProvider` (ex: `YahooFinance`)
- `MarketData__FallbackProvider` (ex: `AlphaVantage`)
- `MarketData__AlphaVantage__ApiKey` (secret, via variable d'environnement)

Aucun secret n'est hardcodé.

### Mapping des symboles

Le mapping provider est centralisé dans `MarketData:SymbolMap`.

Exemple minimal pour TotalEnergies:

- Yahoo Finance: `TTE.PA`
- Alpha Vantage: `TTE.PA`
- Compatibilité héritée: `TTE.FR` est automatiquement converti vers `TTE.PA` pour éviter les régressions sur des données/config existantes.

Vous pouvez ensuite ajouter d'autres actifs/providers en configuration sans disséminer des symboles en dur.

### Changer le provider principal plus tard

Modifiez simplement:

- `MarketData__PrimaryProvider`
- `MarketData__FallbackProvider`

et conservez/ajustez les mappings dans `MarketData__SymbolMap__...`.

## Règles de scoring MVP 1

- `+30` si `Close > SMA200`
- `+20` si `SMA50 > SMA200`
- `+20` si `RSI14` dans `[35, 55]`
- `+20` si drawdown 52 semaines dans `[-15%, -5%]`
- `+10` si `Close > Close veille`
- borné entre `0..100`

Mapping:
- `0..39` => `NO BUY`
- `40..69` => `WATCH`
- `70..100` => `BUY ZONE`


## Règles de score de sortie MVP 3

Le moteur de sortie est volontairement simple, déterministe et séparé du moteur d'entrée.

Critères (score borné entre `0..100`):

- `+25` si `RSI14 >= 75`
- `+20` si `Close > SMA50` de plus de `12%`
- `+20` si `RSI` était en surachat la veille puis baisse avec une clôture rouge
- `+15` si `Close < Close veille` (dégradation momentum court terme)
- `+25` si `Close < SMA50` (cassure court/moyen terme)
- `+30` si `Close < SMA200` (cassure de tendance majeure)

Mapping:
- `0..34` => `HOLD`
- `35..64` => `TRIM / TAKE PROFIT`
- `65..100` => `SELL ZONE`

Le dashboard affiche également la raison principale de sortie (ex: cassure SMA200, surachat, etc.).
Les signaux d'entrée/sortie affichent un tooltip au survol avec le score total, les contributions détaillées et un résumé métier.

## Configuration

1. Copier `.env.example` vers `.env`
2. Modifier au minimum `Auth__Password`, `POSTGRES_PASSWORD` et `MarketData__AlphaVantage__ApiKey`

Configuration importante:
- `ConnectionStrings__Postgres`
- `Auth__Username`
- `Auth__Password`
- `Asset__Symbol` (par défaut `TTE.PA`)
- `Scheduler__DailyRunTimeUtc` (heure UTC du job quotidien)
- `MarketData__PrimaryProvider`
- `MarketData__FallbackProvider`
- `MarketData__AlphaVantage__ApiKey`

## Codex Setup

Le script `codex/setup.sh` prépare l’environnement Codex quand le conteneur Ubuntu n’a pas .NET préinstallé.

Il :
- installe le SDK **.NET 8.0 (LTS)** si nécessaire (méthode `dotnet-install.sh`)
- vérifie la disponibilité de `dotnet` dans le `PATH`
- détecte automatiquement le fichier `.sln` du repository
- exécute `dotnet restore`, `dotnet build`, puis `dotnet test` (les échecs de tests sont loggés sans faire échouer le setup)

Dans l’environnement Codex, ce script est exécuté pendant la phase de setup **avant** les autres tâches, afin de garantir que le build et les tests .NET peuvent être lancés de manière fiable.

## Lancer en local avec Docker Compose

```bash
docker compose --env-file .env up --build
```

Application:
- URL: `http://localhost:8080`
- Login: `Auth__Username` / `Auth__Password`

## Déploiement OVH VPS (Linux)

1. Installer Docker + Docker Compose plugin
2. Cloner le repo
3. Créer `.env` sécurisé
4. Lancer `docker compose up -d --build`
5. Ajouter Nginx reverse proxy (exemple fourni dans `nginx/default.conf`)
6. (optionnel recommandé) Ajouter TLS via Let's Encrypt

## Migrations

Une migration initiale EF Core est incluse dans `Infrastructure/Persistence/Migrations`.
Au démarrage web, `Database.Migrate()` applique les migrations automatiquement.

## Évolutions futures facilitées

Le code prépare l'ajout futur de:
- multiples actifs
- multiples providers
- alertes (email / Telegram)
- stratégies supplémentaires

Sans ajouter de complexité inutile dans ce MVP.
