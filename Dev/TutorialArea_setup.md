# TutorialArea — настройка в Editor

## Что это
Туториал-зона в MainMenu: маленькая арена с фиксированной камерой. Показывает как выглядит геймплей (игрок + бот) без входа в GameScene.

## Шаги в Editor

### 1. Создать GameObject "TutorialArea" внутри Canvas (или рядом с Canvas)
- Add Component → `TutorialArea` (CurlingRoyale.UI)
- В инспекторе задать:

| Поле | Значение |
|---|---|
| Player Prefab | `Assets/Prefabs/PlayerDrone.prefab` |
| Bot Prefab | `Assets/Prefabs/BotDrone.prefab` |
| Spawn Point | пустой GameObject (Transform) в центре будущей арены. Например `(-2, 0, 0)` |
| Bot Spawn Offset | `(2, 0, 0)` — бот справа от игрока на расстоянии 2 |
| Camera Point | (опционально) Transform с позицией камеры, например `(0, 0, -10)` и rotation `(0, 0, 0)` |
| Camera Size | `5` (orthographic size, чтобы арена помещалась) |
| Infinite HP | `true` — оба дрона бессмертные |
| Infinite HP Value | `99999` |

### 2. Создать пустой GameObject "SpawnPoint" под TutorialArea
- Это Transform-объект. В инспекторе: position = `(-2, 0, 0)`, rotation = `(0, 0, 0)`.
- Перетащить его в поле "Spawn Point" у TutorialArea.

### 3. Создать "CameraPoint" (опционально)
- Если указано — камера перейдёт туда при старте MainMenu.
- Если null — камера останется как есть (Main Camera в сцене).
- Position: `(0, 0, -10)`, Rotation: `(0, 0, 0)`.
- Camera Size в TutorialArea = `5` даст orthographic size 5 (примерно 10 единиц высоты).

### 4. Зачем это нужно
- Юзер видит мини-демо прямо в меню
- Игрок (PlayerController) активен, можно постреливать мышкой
- Бот (BotController) отключён, Rigidbody2D = Static, HP = 99999
- Камера фиксирована → ничего не дёргается

### 5. Что НЕ трогать
- НЕ удаляй DroneSkinApplier с PlayerDrone (он есть в префабе)
- НЕ включай EnableAutoSkin если не хочешь чтобы дроны красились скином (по умолчанию они серые, как в префабе)

### 6. Если не работает
- Проверь что TutorialArea GameObject находится в сцене MainMenu
- Проверь что SpawnPoint / CameraPoint — это Transform'ы (не null)
- В Console должны появиться Debug логи от PlayerController (или другие, если есть ошибки)
- Если дроны спавнятся но бесконечно падают -- у TutorialArea на SpawnPoint может быть scale 0; поставь scale (1,1,1)

### 7. Связь с MenuButtonBinder
TutorialArea и MenuButtonBinder — независимые компоненты. TutorialArea работает всегда (при старте сцены). MenuButtonBinder отвечает только за кнопки/панели. Они не пересекаются.
