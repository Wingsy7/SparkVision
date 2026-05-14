# SparkVision

Application Windows Forms .NET 10 pour analyser des donnees de consommation energetique. Les CSV sont importes automatiquement dans une base SQLite locale pour garder une demo simple et autonome.

Une base PostgreSQL dockerisee est aussi fournie comme bonus d'industrialisation : elle importe les memes CSV, cree les tables SQL, les seuils KPI et des vues de demonstration.

## Aperçu

![Dashboard Technicien](docs/screenshot-tech.png)
![Dashboard RSE](docs/screenshot-rse.png)

## Contenu

- `SparkVision.WinForms` : application principale.
- `SparkVision.WinForms/Data/technician_dataset.csv` : releves horaires en kWh pour la vue technicien.
- `SparkVision.WinForms/Data/rse_dataset.csv` : bilan mensuel par poste pour la vue RSE.
- `sparkvision.db` : base SQLite creee automatiquement a cote de l'executable au premier lancement.
- `sparkvision.log` : journal applicatif genere automatiquement a cote de l'executable, avec purge au-dela de 500 lignes.
- `docker-compose.sql.yml` : PostgreSQL dockerise pour montrer l'automatisation SQL.
- `database/postgres` : schema, import CSV, vues KPI/RSE/anomalies et requetes demo.
- `scripts` : commandes PowerShell pour lancer la demo et la base Docker.

## Lancer l'application

```powershell
cd SparkVision
.\scripts\lancer-demo.ps1
```

Commande directe equivalente :

```powershell
$env:DOTNET_ROLL_FORWARD='Major'
dotnet run --project SparkVision.WinForms\SparkVision.WinForms.csproj
```

## Lancer la base SQL Docker

La base Docker est optionnelle : l'application WinForms continue de fonctionner avec SQLite meme si Docker n'est pas lance.

```powershell
cd SparkVision
.\scripts\start-docker-sql.ps1 -Reset
.\scripts\show-sql-demo.ps1
```

Connexion par defaut :

```text
Host=localhost;Port=5433;Database=sparkvision;Username=sparkvision_user;Password=SparkVision123!
```

Pour personnaliser les identifiants, copie `.env.example` vers `.env` puis modifie les valeurs.

## Interface

- Onglet `Technicien - Diagnostic` : KPI avec alertes couleur, filtre `1 jour / 7 jours / 30 jours`, seuil d'anomalie configurable, export CSV, courbe horaire avec infobulles et points rouges pour les anomalies.
- Onglet `RSE - Bilan mensuel` : total annuel, indicateur de transition energetique, barres empilees par mois, tableau detaille, ligne total en gras et surlignage du mois le plus consommateur.
- Onglet `Vue journaliere` : aggregation par jour sur les 30 derniers jours, total et moyenne journaliere.

## Architecture

La version de demonstration utilise une architecture WinForms -> SQLite pour obtenir un dashboard standalone, plus simple a deployer et plus fiable en soutenance. La couche PostgreSQL Docker ajoute une preuve d'industrialisation : creation de schema, import automatise des CSV et vues SQL pour les KPI, les anomalies et les agregations.

## Demo orale conseillee

1. Lancer `.\scripts\lancer-demo.ps1`.
2. Montrer le seuil d'anomalie : passer de `2.0` a `1.5`, les points rouges augmentent.
3. Changer la periode `1 jour`, `7 jours`, `30 jours`.
4. Passer sur `Vue journaliere` pour montrer l'agregation simple par jour.
5. Passer sur l'onglet RSE : total annuel, mois max en orange, ligne total en gras, fleche de transition.
6. En bonus, lancer `.\scripts\start-docker-sql.ps1 -Reset` puis `.\scripts\show-sql-demo.ps1` pour montrer l'import SQL automatise.

## Publication

```powershell
dotnet publish SparkVision.WinForms\SparkVision.WinForms.csproj -c Release -o publish/desktop -r win-x64 --self-contained true -p:PublishSingleFile=true
```
