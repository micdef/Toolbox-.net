# Directives du Projet Toolbox

## Framework

- **Version** : .NET 10
- **Langage** : C# 14.0

---

## Architecture des Services

### Injection de Dépendances
- Chaque service et classe doit être **injectable** via le conteneur DI de .NET
- Utiliser les extensions `IServiceCollection` pour l'enregistrement

### Disposal
- Chaque service et classe doit implémenter **IDisposable** et/ou **IAsyncDisposable**
- Utiliser les classes de base `BaseDisposableService` ou `BaseAsyncDisposableService`

### Généricité
- Les services doivent être **aussi génériques que possible**
- Favoriser la réutilisabilité et l'extensibilité
- Éviter les implémentations trop spécifiques

---

## Organisation par Catégorie

- Pour chaque catégorie de service : **une seule interface**
- L'interface définit le contrat commun pour tous les services de la catégorie

### Catégories existantes

| Catégorie | Interface | Services |
|-----------|-----------|----------|
| Cryptography | `ICryptographyService` | Base64, AES, RSA |
| FileTransfer | `IFileTransferService` | FTP, FTPS, SFTP |
| Mailing | `IMailingService` | SMTP |
| Api | `IApiService` | HTTP Client |
| Ldap | `ILdapService` | ActiveDirectory, OpenLdap, AzureAd, AppleDirectory |

---

## Documentation Doxygen

### Exigences
- Documentation **complète** pour chaque classe et service
- **Obligatoire** pour toutes les parties, y compris privées :
  - Membres privés
  - Variables privées
  - Fonctions privées
  - Constructeurs
  - Destructeurs
  - Propriétés

### Format
```csharp
/// <summary>
/// Description de la classe/méthode.
/// </summary>
/// <param name="paramName">Description du paramètre.</param>
/// <returns>Description du retour.</returns>
/// <exception cref="ExceptionType">Condition de levée.</exception>
/// <remarks>Remarques additionnelles.</remarks>
/// <example>
/// Exemple d'utilisation.
/// </example>
```

---

## Télémétrie

### OpenTelemetry
- Utilisé dans **tout le projet**
- Chaque opération doit avoir :
  - **Tracing** : Activities pour le suivi distribué
  - **Métriques** : Compteurs et histogrammes

### Métriques standards
- `toolbox.operations.count` : Nombre d'opérations
- `toolbox.operations.duration` : Durée des opérations (ms)
- `toolbox.errors.count` : Nombre d'erreurs

---

## Tests

### Tests Unitaires
- Chaque service doit avoir des tests unitaires
- Couverture des cas nominaux et des cas d'erreur
- Utiliser des mocks pour les dépendances externes

### Tests de Non-Régression
- Tests pour garantir la stabilité des fonctionnalités existantes
- Exécutés à chaque modification

### Framework
- xUnit pour les tests
- Moq ou NSubstitute pour les mocks

---

## Documentation du Projet

### Fichiers à maintenir

| Fichier | Description |
|---------|-------------|
| `README.md` | Présentation du projet, installation, quick start |
| `USAGE.md` | Guide d'utilisation détaillé de chaque service |
| `.gitignore` | Fichiers à ignorer pour Git (approprié .NET) |
| `CLAUDE.md` | Ce fichier - directives de développement |

### Mise à jour
- Ces fichiers doivent être **mis à jour** à chaque création de service ou classe
- Le README doit refléter les fonctionnalités disponibles
- Le USAGE doit documenter l'utilisation de chaque nouveau service

---

## Structure du Projet

```
Toolbox/
├── src/
│   └── Toolbox.Core/
│       ├── Abstractions/Services/   # Interfaces (1 par catégorie)
│       ├── Base/                    # Classes de base disposables
│       ├── Extensions/              # Extensions DI
│       ├── Options/                 # Options de configuration
│       ├── Services/                # Implémentations par catégorie
│       │   ├── Api/
│       │   ├── Cryptography/
│       │   ├── FileTransfer/
│       │   ├── Ldap/
│       │   └── Mailing/
│       └── Telemetry/               # Infrastructure OpenTelemetry
├── tests/
│   └── Toolbox.Tests/               # Tests unitaires et de non-régression
├── samples/
│   └── Toolbox.Sample/              # Application exemple
└── docs/                            # Documentation Doxygen générée
```

---

## Checklist pour Nouveau Service

- [ ] Vérifier si l'interface de la catégorie existe, sinon la créer
- [ ] Implémenter le service en héritant de `BaseDisposableService` ou `BaseAsyncDisposableService`
- [ ] Ajouter la documentation Doxygen complète (publique ET privée)
- [ ] Ajouter le tracing OpenTelemetry
- [ ] Ajouter les métriques OpenTelemetry
- [ ] Créer l'extension DI pour l'enregistrement
- [ ] Écrire les tests unitaires
- [ ] Écrire les tests de non-régression
- [ ] Mettre à jour `README.md`
- [ ] Mettre à jour `USAGE.md`

---

## Git et Commits

### Messages de Commit
- Les messages doivent être **structurés en Markdown**
- Contenir **toutes les actions effectuées**
- Rester un **résumé concis**
