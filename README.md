# LexiCall

LexiCall est une application Windows personnelle pour collecter, organiser,
rechercher et mémoriser le vocabulaire rencontré pendant la lecture.

Le projet privilégie une approche simple : une application utile rapidement,
un stockage local lisible, peu de dépendances, et une architecture suffisamment
claire pour rester maintenable par une seule personne.

## État actuel

LexiCall est utilisable en Phase 1 (application desktop locale, complète) et sa Phase 2
(backend FastAPI + MongoDB) est désormais implémentée, déployée en production sur un VPS et câblée au client Windows : chaque mutation locale est poussée vers l'API en tâche de fond, best-effort, en plus de la sauvegarde JSON locale qui reste la seule source de vérité pour l'affichage. Voir la Roadmap
plus bas pour le détail de ce qui est fait et ce qui reste hors périmètre pour cette phase
(lecture depuis l'API, résolution de conflits — pertinentes seulement à partir d'un deuxième
utilisateur, l'app Android de la Phase 3).

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
  pour voir un maximum d'entrées sans défiler ; cliquer un chip de catégorie
  (liste ou détail) sélectionne cette catégorie dans l'arbre ;
- icône (emoji) par catégorie, choisie dans un catalogue thématique filtrable
  par mot-clé ;
- image par entrée (upload, redimensionnement/compression automatiques,
  aperçu agrandi au clic), stockée en base64 dans `vocabulary.json` ;
- assignation optionnelle de plusieurs catégories à une entrée, via un
  formulaire où le bloc catégories est mis en avant (grande hauteur, en haut) ;
- entrées autorisées sans catégorie ;
- recherche locale sur mots, définitions, notes, source, exemples, tags et catégories ;
- recherche tolérante aux accents ;
- fenêtre « Options » (thème clair/sombre, accès au dossier de données, configuration de
  la synchronisation API avec test de connexion) ; position/taille de la fenêtre et largeur
  des colonnes mémorisées entre les sessions ;
- synchronisation best-effort en tâche de fond vers l'API (Phase 2) : chaque ajout/
  modification/suppression est poussé vers le serveur sans jamais bloquer l'interface, avec
  rattrapage automatique au démarrage si l'API était injoignable entretemps ; le JSON local
  reste la seule source de vérité affichée, la synchronisation est un miroir best-effort ;
- confirmations de suppression via une boîte de dialogue stylée (thème
  clair/sombre cohérent, plus de MessageBox système) ;
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
- image optionnelle (JPEG encodé en base64) ;
- dates de création et modification.

Une catégorie contient :

- nom ;
- description optionnelle ;
- icône optionnelle (emoji) ;
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

La préférence de thème, ainsi que la position/taille de la fenêtre et la
largeur des colonnes, sont stockées à côté, dans `settings.json` (fichier
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
| Backend (Phase 2) | FastAPI |
| Base de données (Phase 2) | MongoDB |
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

Pour l'API (`api/`, Python/FastAPI), voir `api/README.md` pour l'installation complète ;
en résumé, depuis la racine :

```bash
just install-api
just dev-mongo-up-api
just run-api                # HOST=127.0.0.1 par défaut (LAN : just run-api 0.0.0.0)
```

L'app Windows n'utilise l'API que si `ApiBaseUrl`/`ApiKey` sont renseignés dans Options —
sans configuration, elle fonctionne exactement comme en Phase 1 pure.

## Structure du dépôt

LexiCall est un monorepo : un dossier par surface applicative. `apps/windows`
et `api` contiennent du code aujourd'hui ; `apps/android` reste un dossier
réservé (un simple README) pour la Phase 3, à venir.

```text
apps/
├── windows/   application desktop WPF (Phase 1, actuelle — détail ci-dessous)
└── android/   application mobile React Native/Expo (Phase 3, à venir)
api/           backend FastAPI + MongoDB (Phase 2, implémenté — voir api/README.md)
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
    ├── Services/             Persistance locale JSON, gestion du thème, client API (sync)
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

Depuis la Phase 2, chaque mutation déclenche en plus un envoi best-effort vers l'API
(`VocabularyApiClient`, en tâche de fond, jamais attendu) : le fichier JSON local reste
l'unique source de vérité affichée par l'interface, l'API n'est qu'un miroir tenu à jour de
façon best-effort — voir `.claude/CLAUDE.md` (section « Client sync ») pour le détail du
modèle de synchronisation.

## Roadmap

### Phase 1 — Desktop local

Objectif : application Windows utilisable sans serveur.

- CRUD entrées ;
- CRUD catégories, sous-catégories comprises, avec icône (emoji) ;
- navigation et filtrage par arbre de catégories ;
- recherche locale ;
- thème clair/sombre persisté ; disposition de la fenêtre (taille, position,
  colonnes) mémorisée ;
- persistance JSON ;
- illustration des mots par image (upload, redimensionnement/compression,
  stockage encodé en base64 directement dans `vocabulary.json` — pas de
  fichiers séparés à gérer/lier).

Prochaines améliorations probables :

- import/export ;
- raccourcis clavier (au-delà de F2) ;

### Phase 2 — Backend partagé

- API REST FastAPI + MongoDB (`api/`), authentification par clé API (`X-API-Key`) ;
- migration ponctuelle des données existantes (`vocabulary.json` → MongoDB), idempotente ;
- client Windows câblé : chaque mutation locale pousse une synchronisation best-effort vers
  l'API en tâche de fond, avec rattrapage automatique au démarrage si l'API a été injoignable ;
  configuration (URL, clé) depuis la fenêtre Options, avec test de connexion ;
- déploiement en production sur un VPS (Docker Compose, HTTPS via reverse proxy avec
  certificat automatique) — détails d'infrastructure documentés séparément, dans un dépôt
  privé distinct de celui-ci.

Volontairement hors périmètre pour l'instant :

- bascule des lectures de l'UI vers l'API (le JSON local reste la source de vérité tant
  qu'un vrai mécanisme de résolution de conflits n'existe pas) ;
- résolution de conflits entre éditions concurrentes (pas encore nécessaire à un seul
  utilisateur).

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
