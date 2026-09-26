# Erweiterungsarchitektur

## Home-Lebenszyklus

`DinoHomeService` setzt den eigenständigen State `DinoState.Home`. Vor dem Ausblenden wird die Desktopposition gespeichert. Der Prozess, das Tray, Einstellungen und GPU Dino bleiben aktiv.

Für spätere Übergangsanimationen stehen diese Events bereit:

- `GoingHome`
- `ArrivedHome`
- `ReturningHome`
- `ReturnedHome`

Zusätzlich meldet `DinoStateMachine` jeden Wechsel über `Transitioning` und `Transitioned`.

## Fortschritt

`ProgressService` ist die einzige Schreibstelle für XP und Dino Coins:

```csharp
App.Current.Progress.AddXP(10, "CodingTime");
App.Current.Progress.AddCoins(5, "Task:data-cave-expedition");
```

Level-Ups behalten überschüssige XP und lösen `LevelUp` aus. Die Daten werden atomar in `Data/progress.json` gespeichert.

## Inventory und Anpassungen

Statische Definitionen stehen in `GameData/skins.json` und `GameData/accessories.json`. Besitz und Ausrüstung speichert `InventoryService` getrennt in `Data/inventory.json`. Skins, Accessoire-Slots, besondere Gegenstände und Zuhause-Dekorationen hängen nicht von einer UI ab.

## Aufgaben

`DinoTaskService` lädt Definitionen aus `GameData/tasks.json` und persistiert den Laufzeitstatus in `Data/task-progress.json`. Der Ablauf ist:

`Available` → `Running` → `Completed` → `Claimed`

Belohnungen werden beim Claim ausschließlich über `ProgressService.AddXP` und `ProgressService.AddCoins` vergeben. Eine Aufgaben-UI oder große Gameplay-Schleife ist absichtlich noch nicht enthalten.

## Fenster, Theme und Vordergrund

Farben und wiederverwendbare Control-Templates liegen zentral in `UI/Themes/DinoTheme.xaml`. Chat und Einstellungen nutzen rahmenlose, abgerundete Shells; nur das Companion-Hauptfenster erscheint in der Taskleiste.

`WindowZOrderService` setzt bei aktivierter Option sowohl WPF `Topmost` als auch die native Windows-Z-Order. Der Status wird nach Laden, Sichtbarwerden, Dialogende, Rückruf und Deaktivierung erneut angewendet. `Home` und die optionale Vollbilderkennung bleiben explizite Ausnahmen.

## Mausfolge

`MouseFollowService` wird bei „Aus“ vollständig gestoppt. In den Stufen `Schwach`, `Normal` und `Neugierig` prüft er sparsam in Intervallen von 1,4 bis 0,85 Sekunden, ob der Mauszeiger kurz ruhig geblieben ist. Erst ab einer stufenabhängigen Mindestdistanz fordert er einen kleinen Schritt an. Die Fensterbewegung selbst wird nur für 480 ms weich animiert und anschließend wieder beendet.

## Bild- und Icon-Pipeline

Zustandsbilder stehen unter `Assets/Sprites` und werden über `sprites.json` geladen. Das App-Icon liegt als transparenter PNG-Master und Multi-Size-ICO unter `Assets/Icons`. Die reproduzierbaren lokalen Chroma-Key- und Konvertierungsschritte befinden sich in `tools/process-sprites.ps1` und `tools/process-icon.ps1`.
