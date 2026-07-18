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
- CRUD des catégories, avec sous-catégories (hiérarchie par parent) ;
- navigation par arbre de catégories dans la fenêtre principale : compteurs
  par catégorie (descendants inclus), filtres « Toutes les entrées » et
  « Sans catégorie », renommage inline (F2 ou menu contextuel) ;
- repères visuels dans l'arbre : une couleur distincte par catégorie racine
  (répartition par angle d'or, lisible même avec une quinzaine de racines),
  héritée en atténué par ses sous-catégories, séparateur marqué entre les
  grandes familles ;
- panneaux latéraux (catégories / liste / détail) redimensionnables par
  glisser-déposer, pour les noms de catégories longs ;
- filtrage par catégorie combinable avec la recherche texte ; fil d'ariane
  compact (dernier niveau affiché, chemin complet en infobulle) ;
- liste centrale volontairement compacte (mot, chips de catégories, source)
  pour voir un maximum d'entrées sans défiler ;
- assignation optionnelle de plusieurs catégories à une entrée, via un
  formulaire où le bloc catégories est mis en avant (grande hauteur, en haut) ;
- entrées autorisées sans catégorie ;
- recherche locale sur mots, définitions, notes, source, exemples, tags et catégories ;
- recherche tolérante aux accents ;
- thème clair/sombre (palette dérivée de l’icône), bascule à chaud et
  préférence persistée ;
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
- parent optionnel (sous-catégorie) ;
- dates de création et modification.

La hiérarchie interdit les cycles : le sélecteur de parent exclut la catégorie
éditée et ses descendantes, et le ViewModel revalide avant de persister.

Les catégories sont des données d’organisation, pas des données obligatoires.
Une entrée reste valide même si elle n’a aucune catégorie.

## Stockage local

Les données sont sauvegardées dans un seul fichier JSON :

```text
%LOCALAPPDATA%\LexiCall\vocabulary.json
```

La préférence de thème est stockée à côté, dans `settings.json` (fichier
distinct des données : perdre l’un n’affecte pas l’autre).

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

`apps/windows/` est autonome : son `.sln` et son `global.json` (version de SDK
épinglée) y vivent directement. La résolution du SDK par `dotnet` remonte
l'arborescence depuis le **répertoire courant** — place-toi dans le dossier
pour que l'épinglage soit respecté (lancer depuis la racine avec juste
`--project` ne suffit pas) :

```powershell
cd apps/windows
dotnet restore
dotnet build LexiCall.sln
dotnet run --project src/LexiCall.Desktop.csproj
```

Si `just` est installé, le `justfile` racine fait ce déplacement pour toi
(voir aussi `apps/windows/justfile`, utilisable directement une fois dans
le dossier) :

```powershell
just run-app-windows
just build-app-windows
```

## Structure du dépôt

LexiCall est un monorepo : un dossier par surface applicative. Seul
`apps/windows` contient du code aujourd'hui ; `apps/android` et `api` sont des
dossiers réservés (un simple README chacun) pour les phases à venir.

```text
apps/
├── windows/   application desktop WPF (Phase 1, actuelle — détail ci-dessous)
└── android/   application mobile React Native/Expo (Phase 3, à venir)
api/           backend FastAPI + MongoDB (Phase 2, à venir)
```

## Structure de `apps/windows/`

```text
apps/windows/
├── LexiCall.sln          Solution .NET (autonome, propre à cette app)
├── global.json           Version de SDK .NET épinglée
├── justfile              Commandes locales (run, build, clean, test)
└── src/
    ├── LexiCall.Desktop.csproj
    ├── Models/               Modèles métier : entrée, catégorie, base JSON
    ├── ViewModels/           État et logique de présentation MVVM
    ├── Services/             Persistance locale JSON, gestion du thème
    ├── Commands/             Commandes WPF réutilisables
    ├── Converters/           Convertisseurs XAML (chips, indentation, couleur des catégories)
    ├── Utilities/            Helpers texte et hiérarchie de catégories
    ├── Themes/               Couleurs clair/sombre et styles partagés
    ├── MainWindow.xaml       Vue principale : arbre de catégories, liste, détail
    ├── EntryEditorWindow     Modale d’ajout/modification d’entrée
    └── CategoryEditorWindow  Modale d’ajout/modification de catégorie
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

Les fenêtres modales (`EntryEditorWindow`, `CategoryEditorWindow`) travaillent
sur un ViewModel dédié. Quand l’utilisateur valide, elles renvoient le résultat
à `MainWindowViewModel`, qui met à jour les collections et sauvegarde le JSON.

La gestion des catégories (création, renommage, reparentage, suppression) se
fait directement dans le panneau latéral de la fenêtre principale ; toute
mutation déclenche une sauvegarde complète immédiate du fichier JSON.

## Roadmap

### Phase 1 — Desktop local

Objectif : application Windows utilisable sans serveur.

- CRUD entrées ;
- CRUD catégories, sous-catégories comprises ;
- navigation et filtrage par arbre de catégories ;
- recherche locale ;
- thème clair/sombre persisté ;
- persistance JSON ;
- illustration des mots par image (upload, redimensionnement/compression,
  stockage encodé en base64 directement dans `vocabulary.json` — pas de
  fichiers séparés à gérer/lier).

Prochaines améliorations probables :

- import/export ;
- raccourcis clavier (au-delà de F2) ;

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
- génération automatique d'une icône par catégorie, via un LLM (Mistral API)
  lors d'une étape finale d'enrichissement (nécessite forcément un appel LLM :
  contrairement aux autres idées de cette section, pas d'équivalent local/manuel
  raisonnable pour cette fonctionnalité) ;
- enrichissement local via Ollama ou service serveur optionnel.

L’IA ne doit jamais devenir une dépendance obligatoire : LexiCall doit rester
utilisable sans modèle de langage.
