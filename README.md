# Médiathèque — V1 web

Application web ASP.NET Core en C# avec catalogue et base SQLite locale.

## Lancer le site

1. Installer le SDK .NET 10 si ce n'est pas déjà fait.
2. Ouvrir `Mediatheque.csproj` dans Visual Studio puis appuyer sur **F5**, ou ouvrir un terminal dans le dossier du projet et exécuter :

   ```powershell
   dotnet run --urls http://localhost:5000
   ```

3. Ouvrir `http://localhost:5000` dans un navigateur. Pour arrêter le site, appuyer sur **Ctrl+C** dans le terminal.

Au premier démarrage, l'application crée automatiquement la base, ses tables et quelques documents de démonstration. Aucune installation de SQL Server n'est nécessaire : SQLite stocke les données dans un fichier local.

## Base de données

Le fichier est créé ici : `%LOCALAPPDATA%\MediathequeV1\mediatheque.db` (généralement `C:\Users\<votre-nom>\AppData\Local\MediathequeV1\mediatheque.db`). Les tables `Books`, `Users` et `Loans` sont créées par `LibraryDatabase.cs` au démarrage. Les colonnes `CoverUrl` et `Synopsis` sont ajoutées automatiquement aux bases existantes. Il est donc inutile d'exécuter un script SQL séparément.

Pour inspecter ou modifier le fichier à la main, installer **DB Browser for SQLite**, puis ouvrir `mediatheque.db`. Fermer le site avant de remplacer ou déplacer le fichier. Sauvegarder une copie du fichier avant toute modification. Le fichier SQLite local n'est pas inclus dans le dépôt Git : il peut contenir des comptes adhérents. Au démarrage, l'application crée automatiquement la base, son schéma et les documents de démonstration.

Les recherches et filtres du catalogue sont disponibles dans l'application. Un administrateur définit le nombre total de copies pour chaque livre ; le site suit le nombre restant et indique « Indisponible » lorsqu'il atteint zéro. Un adhérent connecté peut emprunter une copie disponible pour 14 jours ; le stock restant est décrémenté jusqu'au retour. Il peut consulter ses prêts dans l'onglet **Prêts**, prolonger une seule fois de 7 jours maximum ou rendre le livre. L'administration affiche tous les emprunts, leurs adresses e-mail, les retours et une liste distincte des prêts en retard après leur échéance. L'historique des prêts, y compris les prêts rendus, est conservé dans `Loans`; un index SQLite garantit que le stock disponible ne peut pas être emprunté deux fois.

## Comptes adhérents et administration

Les visiteurs peuvent créer un compte depuis **Adhésion / Connexion**. L'inscription crée toujours un compte adhérent (`Role = 'Member'`) et les mots de passe sont stockés sous forme hachée. Pour nommer un adhérent administrateur, créez d'abord son compte, arrêtez l'application, puis ouvrez `mediatheque.db` dans DB Browser for SQLite et exécutez la requête suivante dans l'onglet **Exécuter le SQL** (remplacez l'adresse) :

```sql
UPDATE Users SET Role = 'Admin' WHERE Email = 'adherent@example.com';
```

Cliquez sur **Écrire les changements**, puis relancez l'application. L'administrateur verra le bouton **Administration** à gauche. Cette page permet d'ajouter, modifier et supprimer des documents. Les droits sont contrôlés côté serveur depuis SQLite pour chaque action ; l'inscription ne permet jamais de choisir le rôle admin.
