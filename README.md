# LexiCall

LexiCall est une application Windows personnelle pour collecter, organiser,
rechercher et mémoriser le vocabulaire rencontré pendant la lecture.

Le projet privilégie une approche simple : une application utile rapidement,
un stockage local lisible, peu de dépendances, et une architecture suffisamment
claire pour rester maintenable par une seule personne.

## État actuel

LexiCall est dans sa Phase 1 : application desktop locale, sans backend.

Fonctionnalités disponibles :

- CRUD des entrées de vocabulaire ;
- CRUD des catégories ;
- assignation optionnelle de plusieurs catégories à une entrée ;
- entrées autorisées sans catégorie ;
- recherche locale sur mots, définitions, notes, source, exemples, tags et catégories ;
- recherche tolérante aux accents ;
- sauvegarde et rechargement depuis un fichier JSON local ;
- migration automatique de l’ancien format JSON si nécessaire.

## Modèle fonctionnel

Une entrée de vocabulaire contient :

- mot ;
- définition ;
- synonymes ;
- phrases d’exemple ;
- notes personnelles ;
- source ;
- tags ;
- zéro, une ou plusieurs catégories ;
- dates de création et modification.

Une catégorie contient :

- nom ;
- description optionnelle ;
- identifiant stable ;
- parent optionnel, prévu pour de futures sous-catégories ;
- dates de création et modification.

Les catégories sont des données d’organisation, pas des données obligatoires.
Une entrée reste valide même si elle n’a aucune catégorie.

## Stockage local

Les données sont sauvegardées dans un seul fichier JSON :

```text
%LOCALAPPDATA%\LexiCall\vocabulary.json
```

Le fichier contient un document racine :

```json
{
  "Entries": [],
  "Categories": []
}
```

Les entrées référencent les catégories par `CategoryIds`. Cela permet de
renommer une catégorie sans modifier toutes les entrées qui l’utilisent.

## Technologies

| Composant | Technologie |
| --- | --- |
| Application desktop | .NET 10, C#, WPF |
| Architecture UI | MVVM simple |
| Stockage Phase 1 | JSON local |
| Backend futur | FastAPI |
| Base de données future | MongoDB |
| Mobile futur | React Native, Expo |

## Prérequis

- Windows 10 ou 11 ;
- SDK [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) ;
- VS Code avec C# Dev Kit, ou Visual Studio avec la charge Desktop .NET.

Vérifier le SDK :

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

Si `just` est installé :

```powershell
just run
```

## Structure du projet

```text
src/
└── LexiCall.Desktop/
    ├── Models/             Modèles métier : entrée, catégorie, base JSON
    ├── ViewModels/         État et logique de présentation MVVM
    ├── Services/           Persistance locale JSON
    ├── Commands/           Commandes WPF réutilisables
    ├── Utilities/          Helpers texte simples
    ├── MainWindow.xaml     Vue principale : recherche, liste, détail
    ├── EntryEditorWindow   Modale d’ajout/modification d’entrée
    └── CategoriesWindow    Modale de gestion des catégories
```

## Architecture actuelle

Le flux principal est :

```text
MainWindow.xaml
  ↕ bindings
MainWindowViewModel
  ↕
VocabularyRepository
  ↕
vocabulary.json
```

Les fenêtres modales (`EntryEditorWindow`, `CategoriesWindow`) travaillent sur
un ViewModel dédié. Quand l’utilisateur valide, elles renvoient le résultat à
`MainWindowViewModel`, qui met à jour les collections et sauvegarde le JSON.

## Roadmap

### Phase 1 — Desktop local

Objectif : application Windows utilisable sans serveur.

Déjà fait :

- CRUD entrées ;
- CRUD catégories ;
- recherche locale ;
- persistance JSON.

Prochaines améliorations probables :

- meilleure présentation visuelle des catégories ;
- sous-catégories ;
- import/export ;
- raccourcis clavier ;
- tests unitaires sur repository, recherche et validation.

### Phase 2 — Backend partagé

- FastAPI ;
- MongoDB ;
- API REST ;
- hébergement léger sur VPS OVH ;
- migration progressive du stockage local vers une source de vérité serveur.

### Phase 3 — Android

- React Native avec Expo ;
- ajout rapide de mots pendant la lecture ;
- recherche, édition et révision depuis mobile ;
- synchronisation via l’API.

### Phase 4 — IA optionnelle

Idées possibles :

- suggestions de synonymes ;
- exemples de phrases ;
- suggestions de tags ou catégories ;
- enrichissement local via Ollama ou service serveur optionnel.

L’IA ne doit jamais devenir une dépendance obligatoire : LexiCall doit rester
utilisable sans modèle de langage.
