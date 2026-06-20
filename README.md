# LexiCall

LexiCall est une application personnelle pour collecter, organiser et mémoriser
le vocabulaire rencontré pendant la lecture.

Le projet privilégie une application simple, rapide et agréable à utiliser, et peu coûteuse à héberger. Il sert également de projet d'apprentissage full-stack construit progressivement.

## État du projet

LexiCall est au début de sa première phase. Le dépôt contient actuellement le
socle de l'application Windows WPF ; les fonctionnalités métier et le stockage
JSON restent à implémenter.

## Fonctionnalités prévues

Une entrée de vocabulaire pourra contenir :

- le mot et sa définition ;
- des synonymes et des phrases d'exemple ;
- des notes personnelles ;
- sa source, par exemple un livre et son auteur ;
- ses dates de création et de modification ;
- une ou plusieurs catégories, avec sous-catégories éventuelles ;
- des tags facultatifs.

L'application permettra également de gérer les catégories et d'effectuer une
recherche rapide dans les mots, définitions, notes, exemples et catégories.

## Roadmap

### Phase 1 — Application Windows locale

- C# et WPF ;
- architecture MVVM simple ;
- fichier JSON unique comme stockage local ;
- aucun backend et aucune synchronisation.

### Phase 2 — Backend partagé

- API REST avec FastAPI ;
- MongoDB comme source de vérité ;
- auto-hébergement sur un petit VPS OVH ;
- migration du client Windows vers l'API.

### Phase 3 — Application Android

- React Native avec Expo ;
- ajout, modification, révision et recherche du vocabulaire ;
- utilisation de la même API que l'application Windows.

### Phase 4 — Enrichissement facultatif par IA

L'IA pourra suggérer des catégories, synonymes, exemples ou tags. Elle restera
strictement optionnelle : LexiCall devra continuer à fonctionner sans modèle de
langage. Une première intégration pourra utiliser Ollama sur la machine locale.

## Technologies

| Composant | Technologie |
| --- | --- |
| Client Windows | .NET 10, C#, WPF |
| Stockage initial | JSON |
| Backend futur | Python, FastAPI |
| Base de données future | MongoDB |
| Client Android futur | React Native, Expo |

Les bibliothèques supplémentaires seront choisies au moment où un besoin concret
apparaîtra. Le projet évite volontairement les microservices, Kubernetes et les
abstractions prématurées.

## Prérequis

- Windows 10 ou 11 ;
- SDK [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) ;
- VS Code avec **C# Dev Kit**, ou Visual Studio avec la charge de travail
  « Développement Desktop en .NET ».

Le fichier `global.json` sélectionne le SDK attendu par le dépôt. Vérifier
l'installation avec :

```powershell
dotnet --version
```

## Lancer le projet

Depuis la racine du dépôt :

```powershell
dotnet restore
dotnet build
dotnet run --project src/LexiCall.Desktop
```

## Structure actuelle

```text
LexiCall.sln
src/
└── LexiCall.Desktop/    Application Windows WPF
```

La prochaine étape consiste à réaliser une première tranche fonctionnelle :
afficher une liste de mots, ajouter un mot, puis sauvegarder et recharger cette
liste depuis un fichier JSON.
