# CycliqueShareTracker - MVP 1

MVP personnel en **.NET / ASP.NET Core** pour analyser l'action **TotalEnergies (Euronext Paris)** avec une approche simple et cyclique.

## Objectifs couverts

- Ingestion journalière des prix (provider Stooq CSV)
- Stockage PostgreSQL
- Calcul indicateurs: SMA50, SMA200, RSI14, drawdown 52 semaines
- Score d'entrée sur 100 + signal textuel (`NO BUY`, `WATCH`, `BUY ZONE`)
- Dashboard web privé (auth cookie + mot de passe unique via variable d'environnement)
- Historique des signaux en base
- Planification quotidienne + déclenchement manuel
- Docker + Docker Compose prêts pour VPS OVH Linux

## Architecture

Monorepo en couches:

- `src/CycliqueShareTracker.Web` : UI MVC, auth, endpoints, background job
- `src/CycliqueShareTracker.Application` : interfaces et logique métier (indicateurs, score/signal, orchestration)
- `src/CycliqueShareTracker.Domain` : entités métier
- `src/CycliqueShareTracker.Infrastructure` : provider données, repositories, EF Core/PostgreSQL
- `tests/CycliqueShareTracker.Tests` : tests unitaires moteur score + cas limites indicateurs

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

## Configuration

1. Copier `.env.example` vers `.env`
2. Modifier au minimum `Auth__Password` et `POSTGRES_PASSWORD`

Configuration importante:
- `ConnectionStrings__Postgres`
- `Auth__Username`
- `Auth__Password`
- `Asset__Symbol` (par défaut `tte.fr`)
- `Scheduler__DailyRunTimeUtc` (heure UTC du job quotidien)


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
- backtest
- stratégies supplémentaires

Sans ajouter de complexité inutile dans ce MVP.
