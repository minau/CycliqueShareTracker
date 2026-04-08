# Cyclique Share Tracker

Application web ASP.NET Core pour suivre des actions cycliques avec un focus sur les prix et les indicateurs techniques.

## Fonctionnalités actuelles
- Ingestion quotidienne des prix (providers avec fallback)
- Calcul d'indicateurs techniques (SMA, EMA, RSI, MACD, Bollinger, Parabolic SAR)
- Watchlist protégée avec vue synthétique
- Vue détail avec graphiques prix + indicateurs
- Synchronisation quotidienne via tâche de fond

## Architecture
- `src/CycliqueShareTracker.Web` : UI MVC, auth, pages
- `src/CycliqueShareTracker.Application` : orchestration, cas d'usage, calculs applicatifs
- `src/CycliqueShareTracker.Domain` : entités métier
- `src/CycliqueShareTracker.Infrastructure` : persistence PostgreSQL, providers de données
- `tests/CycliqueShareTracker.Tests` : tests unitaires

## Lancement local
```bash
docker compose up --build
```

## Notes
L'application n'exécute pas d'ordres. Elle fournit des données et indicateurs pour une analyse manuelle.
