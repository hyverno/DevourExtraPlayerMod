# DevourMorePlayers

Mod [MelonLoader](https://github.com/LavaGang/MelonLoader) pour **DEVOUR** (Straight Back Games) qui lève la limite de 4 joueurs par partie. La limite par défaut passe à **8 joueurs**, et elle est configurable.

> Mod non officiel. Les développeurs du jeu ne fournissent aucun support pour les jeux modifiés.

## Fonctionnalités

- **Lobby jusqu'à 8 joueurs.** La limite à 4 est relevée partout où le jeu l'impose :
  - le lobby Steam ;
  - la room Photon ;
  - la limite de connexions Bolt (au démarrage du serveur et dans la config utilisée en jeu) ;
  - la session UdpKit ;
  - le contrôle « lobby plein » du jeu.
- **Bouton d'invitation toujours actif** pour l'hôte, même au-delà de 4 joueurs.
- **IA des ennemis compatible au-delà de 4 joueurs.** Les tâches d'IA (ouïe, vue, lampe UV, mise KO) gardent un cache par joueur dimensionné pour 4. Au 5e joueur, l'IA plantait (`IndexOutOfRangeException` dans `CanHearPlayer`) : les ennemis ne frappaient plus et la lampe UV ne les brûlait plus. Le mod agrandit ces caches.
- **Mode test multi-instance** (pour le développement) : lancer plusieurs DEVOUR sur un seul PC avec un seul compte Steam. Voir plus bas.

## Installation

1. Installer **MelonLoader 0.7.x** sur DEVOUR, puis lancer le jeu une fois pour que MelonLoader génère ses fichiers.
2. Copier `DevourMorePlayers.dll` dans le dossier `Mods\` du jeu (par exemple `...\steamapps\common\Devour\Mods\`).
3. Lancer le jeu. La console MelonLoader doit afficher `DevourMorePlayers actif — limite lobby : 8`.

C'est **l'hôte** qui doit avoir le mod pour accueillir plus de 4 joueurs, car l'IA tourne chez lui. Pour les joueurs qui rejoignent, l'installer est recommandé. Leur jeu sans mod n'a pas encore été testé au-delà de 4 joueurs.

## Configuration

Dans `UserData\MelonPreferences.cfg` (fichier créé au premier lancement) :

```toml
[DevourMorePlayers]
MaxPlayers = 8
```

## Compilation

Prérequis : le SDK .NET 6 ou plus récent, et DEVOUR avec MelonLoader déjà lancé une fois. Les références viennent de `MelonLoader\net6` et de `MelonLoader\Il2CppAssemblies` du jeu.

```bash
dotnet build -c Release -p:GamePath="D:\Steam\steamapps\common\Devour"
```

- La variable d'environnement `DEVOUR_PATH` peut aussi indiquer le dossier du jeu.
- Ajouter `-p:InstallMod=true` copie directement la DLL dans le dossier `Mods\`.
- La DLL compilée est produite dans `bin\Release\net6.0\DevourMorePlayers.dll`.

## Tester à plusieurs sur un seul PC

Le script `tools\Lancer-Multi.ps1` lance N instances du jeu en fenêtré et les dispose en mosaïque. Les fenêtres sont nommées `DEVOUR #1 (HOTE)`, `DEVOUR #2`, etc.

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Lancer-Multi.ps1 -Count 6
```

| Option | Rôle |
|---|---|
| `-Count N` | Nombre d'instances, de 1 à 8 (4 par défaut) |
| `-DelaySeconds S` | Délai entre deux lancements (20 s par défaut). Des lancements trop rapprochés peuvent faire planter une instance. |
| `-GamePath "..."` | Dossier du jeu, s'il n'est pas trouvé automatiquement via Steam |
| `-Sound` | Garde le son sur toutes les instances (par défaut, les instances #2 et suivantes sont muettes) |
| `-NoTile` | Ne range pas les fenêtres en mosaïque |

Ensuite, dans le jeu :
1. Dans **#1**, faire *Héberger une partie*, de préférence en **privé** pour éviter les inconnus.
2. Dans chaque autre instance, faire *Rejoindre une partie*, ouvrir la liste des serveurs et choisir ce lobby.

Pour lancer une seule instance à la main :

```bash
DEVOUR.exe -screen-fullscreen 0 -screen-width 640 -screen-height 480 --devour-instance=3
```

Le numéro `--devour-instance=N` doit être **unique** pour chaque instance (1 = hôte). Le mode test n'est actif qu'avec cet argument : le jeu normal n'est jamais affecté. Avec cet argument, le mod :
- rend chaque client unique aux yeux de l'hôte (SteamID64 + N-1 comme `playerId`, ID matériel différent) ;
- désactive `CheckUserOnJoin` dans la room Photon de l'hôte ;
- ajoute ` #N` au pseudo ;
- garde les instances actives en arrière-plan, à 30 FPS et sans son pour les clients.

Les logs Unity de chaque instance sont écrits dans `%TEMP%\DevourMultiTest\unity-N.log`. Le script force le mode fenêtré, ce qui modifie les réglages d'écran du jeu. `tools\Restaurer-Ecran.ps1` remet les réglages d'origine.

## Limites connues

- L'écran de statistiques de fin de partie n'affiche que 4 joueurs (`GameStatsToken` a 4 emplacements en dur).
- Dans le lobby, les emplacements des personnages sont prévus pour 4 joueurs.

## Crédits

Merci aux testeurs :

- Wylfore
- bot cool
- sinaro
- sundae
- Claude
- pac pac city

## Licence

À définir.
