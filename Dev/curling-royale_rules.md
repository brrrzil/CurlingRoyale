# curling-royale_rules.md — правила работы AI-агента по проекту «Curling Royale»

> **Универсальные** правила работы (коммуникация, гигиена, git, coding conventions,
> workflow triggers) — см. базовый `AGENT_RULES.md`. Здесь — только то, что
> касается **этого проекта**.

---

## 0. Снапшот проекта

| Параметр | Значение |
|---|---|
| Движок | Unity 6 LTS |
| Платформа | WebGL (Yandex Games + VK Play) |
| Язык | C# |
| Тип | 2D, вид сверху |
| Условие победы | Последний выживший |
| Режимы | **Основной:** FFA-8, **дополнительный:** 1v1 |
| Геймплей | Дискретный: все «стреляют» одновременно, смотрят результат, ждут следующий ход |
| Аудио | Не в скоупе сейчас, добавим позже |
| Мобильная адаптация | Да |
| VFX | Владелец разберётся сам |
| Аналитика | Подключить, если не сильно усложнит |
| Монетизация | Rewarded + interstitial: на старте + после каждого матча; in-game валюта / бафы за рекламу или покупку |

**Ближайшая задача (фаза 1):** `StoneCombat.cs`, `HealthBar.cs`, тестовый бот.

---

## 1. Архитектура и стек

### 1.1. Что есть
- `Rigidbody2D` (Dynamic, Linear Damping = 1.5, Gravity Scale = 0) — вся физика на Unity
- `ScriptableObject` для конфигов (`PhysicsConfig`) — продолжаем
- World Space uGUI для HP-баров
- `CameraFollow` — простой `LateUpdate + Lerp`, Cinemachine **не подключаем** пока не понадобится

### 1.2. Что НЕ используем
- ~~**DI через Zenject**~~ — нет в проекте и не нужно для прототипа. Прямые ссылки или простой Service Locator (`GameManager.Instance` и т.п.). Если проект разрастётся — обсудим переезд на DI.
- ~~Вендорский код Zenject читать не надо~~ — потому что его нет.
- ~~Два AudioMixer'а (menu / game)~~ — нет такого by-design разделения. Когда добавим аудио — сделаем один mixer с группами.
- ~~Single AudioSource для голосов NPC~~ — в Curling Royale нет NPC с диалогами.

### 1.3. Что используем
- **Один `MonoBehaviour` = один файл** — стандарт.
- **`ScriptableObject` для data-only** (конфиги урона, спавн-настройки матча, бафы) — стандарт.
- **New Input System** — готовимся к мобильному тачу с самого начала, не переезжаем позже.
- **uGUI** для меню/HUD/результатов (UIToolkit не берём — зрелее, проще, меньше сюрпризов в WebGL).
- **DOTween** или **PrimeTween** — для плавных анимаций UI (тряска HP-бара, fade результатов). Выбор определим, когда откроем репо.
- **Addressables** + ASTC-текстуры + IL2CPP + managed stripping — на фазе 6 (WebGL-оптимизация).

### 1.4. Структура папок (целевая)
```
Assets/Scripts/
├── Combat/         # StoneCombat, HealthBar, DamageConfig (SO)
├── Game/           # GameManager (state machine), MatchController, WinCondition
├── Player/         # PlayerController, InputActions
├── Bots/           # BotController, BotAI
├── Arena/          # ArenaBorder, Spawner
├── Columns/        # HardColumn, BouncyColumn, ColumnConfig (SO) — фаза 2
├── Economy/        # CurrencyManager, DailyChallenges, DailyEvents — фаза 6
├── Skins/          # SkinManager, RimColor — фаза 5
├── UI/             # MainMenu, HUD, ResultsScreen
├── Audio/          # AudioManager, MixerController — фаза 5
└── Configs/        # ScriptableObject ассеты (DamageConfig, BuffConfig, ...)
```

Новые папки сверх этого списка — обсуждаем перед созданием.

## 1.5. Аудит-инварианты (обновлять при изменениях)

- **Пакеты:** DOTween импортирован как `.unitypackage` (лежит в `Resources/DOTweenSettings.asset`), НЕ через UPM. Newtonsoft.Json — UPM.
- **Input:** `ProjectSettings.asset` → `activeInputHandler: 2` (Both) — после миграции PlayerController.
- **Физика:** Rigidbody2D Dynamic, Linear Damping = 1.5. `CustomPhysicsBody` — обёртка над Rigidbody. Не пытаемся переписать на свою физику (ТЗ v1.0 устарел, не путаем).
- **Рендер-пайплайн:** **Built-in 2D** (решение от 2026-06-29). URP-пакеты удалены из `manifest.json`, URP-ассеты удалены. Если позже понадобятся пост-эффекты / 2D Lighting — переезд через Unity Renderer Pipeline Asset converter.
- **Документы:** приоритет `Dev/GDD.txt v2.0` > чатовая TZ > `Dev/ТЗ.txt v1.0` (последний — кандидат на удаление).

---

## 2. Скоуп ближайшей работы

| Фаза | Что | Статус |
|---|---|---|
| 0 | Аудит репо | ⏳ ждём доступ |
| 1 | `StoneCombat`, `HealthBar`, тестовый бот | 🔜 следующая |
| 2 | `GameManager` (state machine), `MatchController`, условие победы | pending |
| 3 | UI: главное меню, HUD, экран результатов | владелец делает сам |
| 4 | Бот-AI | pending |
| 5 | VFX / SFX / камера-шейк | pending |
| 6 | WebGL-оптимизация | pending |
| 7 | Yandex SDK + VK SDK, leaderboard | pending |
| 8 | Монетизация (ads + валюта) | pending |
| 9 | QA + релиз | pending |

---

## 3. База: взято из AGENT_RULES.md без правок

> Эти правила универсальные и действуют без изменений. Не дублирую здесь —
> ссылаюсь на источник.

- **§0 Гигиена:** файлы только в workspace, абсолютные пути в output, минимум side-эффектов.
- **§1 Экономия токенов:** grep/glob перед чтением файла целиком, не читать `Packages/`, `Library/`, `Assets/Plugins/` без нужды, `git diff --stat` для оценки объёма.
- **§2 Коммуникация:** язык пользователя, вывод вперёд, рекомендация вместо «на твоё усмотрение», без customer-service фраз, file:line для ссылок.
- **§3 Git workflow (юзер переопределил 2026-06-29):**
  - **По умолчанию — пуш напрямую в `main`.** Worktree-ветки НЕ используем.
  - Формат коммита `Round N: <заголовок>` остаётся.
  - `git mv` для переименований остаётся (GUID Unity).
  - **Если фича слишком большая для одного пуша (миграция, SDK-интеграция, рефакторинг с потерей совместимости) — я ПРЕДУПРЕЖДАЮ** и предлагаю вернуться к feature-ветке на это изменение.
  - Docs-коммит отдельно — не требуется (нет отдельной docs-ветки).
  - PAT не сохраняем в память, используется inline в `git push`.
- **§4 Coding conventions:** mimic existing patterns, проверять `Packages/manifest.json` перед импортом, минимальные targeted-изменения, `Assets/Scripts/<Домен>/` структура.
- **НЕ ЗАБЫВАТЬ `using` директивы при использовании типов из других namespace'ов проекта.** Round 2 упал с CS0246 в BotController: использовал `StoneCombat` из `CurlingRoyale.Combat`, но не добавил `using CurlingRoyale.Combat;` в файл. GameManager (который я писал раньше в том же раунде) — не забыл, BotController — забыл. **Чеклист: каждый раз, когда использую новый тип из своего namespace, проверять наличие соответствующего `using` в самом начале файла**. Имена namespace проекта: `CurlingRoyale.Arena`, `CurlingRoyale.Bots`, `CurlingRoyale.Combat`, `CurlingRoyale.Configs`, `CurlingRoyale.Game`, `CurlingRoyale.Player`.
- **§5.2 UI/Layout Groups:** одинаковые якоря у детей `VerticalLayoutGroup`, `🔒` ≠ position lock, `Child Force Expand` + `Child Control Size` — пара.
- **§5.3 Audio (когда добавим):** Unity 6 legacy `.cubemap` баг — `textureShape: 2` Cube на PNG.
- **§5.4 Prefab/SO GUIDs:** `git mv` сохраняет GUID, `mv` + ручная правка `.meta` ломает ссылки, `&123 stripped` → данные в parent prefab.
- **§5.5 Editor scripts:** не править `.unity`/`.prefab` руками.
- **§6 Display vs actual value:** `"0.#"` а не `"F1"` для UI; округлять **оба** — display и логику.
- **§7 Single source of truth:** одно место = одно решение. ~~Пример из старого проекта про кнопку диалога убираю~~ — для Curling Royale правило звучит так: «`ShootButton.interactable = true/false` — только в одном месте (state machine раунда), не дублируем в обработчиках UI».
- **§8 Self-updating:** обновляем правила и memory после значимых правок, не на каждый edit.
- **§9 DON'T list:** базовые don't (не путать похожие сущности, не смешивать bugfix с косметикой, не отвечать по памяти на time-sensitive вопросы, не персистить PlayerPrefs ключи рискованно без cleanup, не добавлять per-tick enforcement без нужды).
- **§10 Workflow triggers:** таблица инструментов.
- **§11 Когда спрашивать:** два варианта с разными последствиями → спрашиваем; чёткий следующий шаг → делаем.
- **§12 Финальное:** правила не догма, корректируем осознанно.

---

## 4. НЕ применимо к Curling Royale (вычеркнуто)

> Эти пункты из базового `AGENT_RULES.md` **не действуют** в этом проекте. Упоминаю
> здесь явно, чтобы при аудите не путаться, читая универсальный файл.

- ~~§4 упоминание папок `Inventory/`, `Market/`, `Dialogs/`~~ — нет таких доменов в Curling Royale.
- ~~§5.1 DI через Zenject~~ — DI не используем.
- ~~§5.3 два AudioMixer'а как by-design~~ — нет двух миксеров.
- ~~§5.3 spatialBlend для NPC голосов~~ — нет NPC с голосами.
- ~~§6/§7 примеры из старого проекта (SetIteration, BagsPurchased)~~ — заменены на Curling Royale-аналоги выше.
- ~~§9 «не делай deduplication предметов в инвентаре»~~ — нет инвентаря.
- ~~§9 «не пытайся унифицировать два by-design разных миксера»~~ — нет двух миксеров.

---

## 5. Lesson learned (накапливаем по ходу)

> Каждый значимый фикс = короткий бриф сюда. Формат: дата, что сделал, что узнал.

### 2026-06-29 — URP → Built-in 2D, чистка манифеста
- URP был загружен пакетом, но **не активным пайплайном** (`m_CustomRenderPipeline: 0`). Удаление прошло чисто: 4 файла удалены, 0 ссылок в проекте.
- 9 пакетов из `manifest.json` были мёртвым грузом (Cinemachine, Visual Scripting, Timeline, Multiplayer center, 2D Aseprite, 2D SpriteShape, 2D Tilemap, 2D Tilemap Extras, 2D Animation). Grep по `Assets/` подтвердил — 0 использований.
- В Unity 6 `Rigidbody2D.linearVelocity` — правильный API (старый `velocity` deprecated).
- `git mv` корректно переносит и `.cs`, и `.meta` — GUID сохраняются, scene/prefab ссылки не ломаются. При ручном `mv` + правке `.meta` GUID может потеряться.
- Приватные поля одного компонента **недоступны** другому компоненту даже того же GameObject. Получать Rigidbody2D через `GetComponent<Rigidbody2D>()`, а не через `.rb`.
- Новые C# скрипты без `.meta` — Unity создаст их при первом импорте. Юзер должен закоммитить автогенерированные `.meta`.
- Юзер попросил прямой пуш в `main` без worktree для будущих раундов. Merge `feature/project-bootstrap` → `main` — fast-forward. Ветка и worktree удалены.
- **YAML-урок (Юзер поймал):** при удалении последнего key:value из маппинга в Unity `.asset` не оставлять «голый» ключ без значения — Unity выдаёт `YAML error: Unexpected empty content or blank line when reading mapping`. Правильно заменять на `m_KeyName: {}` (flow mapping). Если ключ не нужен — удалять всю строку.
- Lesson на будущее: при ручных правках `.asset` файлов Unity валидировать через парсер с зарегистрированным `tag:unity3d.com,2011:30`.
- **Сиротский GUID материала** (пурпурный экран): при удалении пакетов (URP в нашем случае) проверить `grep -r '<orphan-guild>' Assets/` — если `.mat` файла нет, а ссылки остались, SpriteRenderer покажет magenta. Лечить созданием `.mat` с тем же GUID + правильным шейдером. Проверить рабочий `.mat` в проекте (например `Line.mat` в `Assets/Materials/`) — там виден валидный GUID шейдера.
- **Input Manager deprecation warning** появляется при `activeInputHandler: 1` или `2` И наличии `com.unity.inputsystem` в манифесте. Можно погасить временно через `activeInputHandler: 0` (потеряем New Input System до момента миграции). Когда дойдём до миграции PlayerController на тач — переедем и вернём на `2`.
- **URP-остатки** после удаления пакета: в репо всё ещё валяются `Assets/Settings/Renderer2D.asset`, `Assets/Settings/Scenes/URP2DSceneTemplate.unity`, `Assets/Settings/DefaultVolumeProfile.asset`. Не ломают рендер (URP не активен), но мусор. Удалить можно отдельным коммитом, если юзер попросит.
- **UniversalAdditionalCameraData на камере** (пурпур, экранированный round 0.5): URP хранит camera-specific настройки как MonoBehaviour с GUID `a79441f348de...` (а также Inline-поля типа `m_RequiresDepthTextureOption`). После удаления URP пакета этот GUID сиротский, но MonoBehaviour **остаётся** на камере. Camera пытается использовать URP-функционал, не находит → может давать magenta. Лечится удалением MonoBehaviour **И** его ссылки в `m_Component` родительского GameObject. Скрипт для такого удаления: см. Round 0.5 commit.
- **Детектор broken GUID**: скрипт ниже — пройтись по `*.unity`/`*.prefab`/`*.asset`, собрать все GUID из `m_Script` / `m_Materials` / `customRenderPipeline` и сверить с `.meta` файлами.
  ```python
  import re, glob
  refs = set()
  for p in glob.glob('Assets/**.unity', recursive=True) + glob.glob('Assets/**.prefab', recursive=True):
      for m in re.finditer(r'guid: ([a-f0-9]+)', open(p).read()):
          refs.add(m.group(1))
  have = set()
  for m in glob.glob('Assets/**.meta', recursive=True):
      for line in open(m):
          mm = re.match(r'^guid:\s*([a-f0-9]+)', line)
          if mm: have.add(mm.group(1))
  orphans = refs - have - {'0000000000000000f000000000000000', '0000000000000000e000000000000000', '0000000000000000d000000000000000'}
  print(orphans)
  ```
- **Самая глубокая проблема URP** выявлена в 5 раундов: мало удалить пакет, мало удалить `UniversalRP.asset`, мало почистить `QualitySettings`, мало создать `.mat`. URP-данные **встроены** в сцены как `UniversalAdditionalCameraData` MonoBehaviour на камерах и `Light2D` MonoBehaviour на лайтах — их надо удалять вручную из `.unity` файлов. **Полный чеклист удаления URP**:
1. Удалить URP-пакеты из `manifest.json` (`com.unity.render-pipelines.universal`, `com.unity.render-pipelines.core` — авто)
2. Удалить `ProjectSettings/URPProjectSettings.asset`, `Assets/Settings/UniversalRP.asset`, `UniversalRenderPipelineGlobalSettings.asset` (+ meta)
3. Очистить `ProjectSettings/GraphicsSettings.asset`: удалить `m_RenderPipelineGlobalSettingsMap:` детей, поставить `m_RenderPipelineGlobalSettingsMap: {}`
4. Очистить `ProjectSettings/QualitySettings.asset`: все `customRenderPipeline: {guid: ...}` → `{fileID: 0}` (важно — **не** оставлять guid!)
5. Удалить `Assets/Settings/Renderer2D.asset`, `DefaultVolumeProfile.asset`, `Scenes/URP2DSceneTemplate.unity` (+ meta)
6. Создать `Sprites-Default.mat` с GUID orphan-а, если SpriteRenderer-ы ссылаются на бывший URP-default
7. Удалить `MonoBehaviour` блоки из `.unity` файлов: UniversalAdditionalCameraData (на камере) и Light2D (на лайтах), плюс ссылки в `m_Component` родительских GO
- **Round 0.6 — моя ошибка (ОТКАЧЕНО в 0.6.1)**: я ошибочно решил, что `textureShape: 1` = Cube (на основе своих же правил где было написано про «Unity 6 cubemap bug»), и перевёл спрайты на `textureShape: 0`. **Это было неправдой.** SpriteRenderer не рендерил пустые `textureShape: 0` (None) текстуры → спрайты слетели. Пурпур был вызван URP-причинами (rounds 0.1-0.5 их починили). Реальные значения `TextureImporterShape` в Unity 6 (по UnityCsReference):
  - `0` = None (не валидно)
  - `1` = Texture2D (правильное для спрайтов)
  - `2` = TextureCube
  - `4` = Texture2DArray
  - `8` = Texture3D
- **Урок на будущее: не трогать enum-численные значения без проверки исходников Unity (github.com/Unity-Technologies/UnityCsReference). Если в моих правилах упомянут какой-то bug — это гипотеза, не факт. Проверять, прежде чем фиксить.**
- **Round 0.7 — финальная развязка пурпура**: мой Round 0.2 создал `Sprites-Default.mat` со шейдером `e260cfa7296ee7642b167f1eb5be5023`, который я взял из `Line.mat`. Оказалось, этот GUID — URP-шейдер `Sprite-Lit-Default`, удалённый с URP-пакетом. В результате `Sprites-Default.mat` рендерил magenta error-shader (Round 0.6.1 вскрыл это). Решение: **удалить сломанный `.mat` и его ссылки** из SpriteRenderer (`m_Materials: []`). Unity подставит свой built-in default sprite material — он всегда работает. Урок: **когда создаёшь fallback-материал для замены удалённого актива, не выдумывать GUID — посмотреть ассет или пустить Unity на built-in default**.
- **Round 0.8 — настоящий фикс рендера**: пустой `m_Materials: []` НЕ работает в Unity 6 после удаления URP пакета — спрайты исчезают (встроенный default `m_SpritesDefaultMaterial` fileID 10754 не подгружается). Решение: **свой собственный shader + material**, на которые 100% контроль.
  - `Assets/Shaders/SpriteDefault.shader` (мини-шейдер «Curling Royale/Sprite Default», Blend One OneMinusSrcAlpha, Queue Transparent)
  - `Assets/Settings/SpriteDefault.mat` с GUID `a97c105638bdf8b4a8650670310a4cd3` (старый orphan) → SpriteRenderer.m_Materials разрешается корректно.
  - Когда в следующий раз будет нужно сделать fallback-материал для 2D-спрайта: написать свой шейдер, не угадывать GUID и не полагаться на built-in defaults.
1. Удалить URP-пакеты из `manifest.json` (`com.unity.render-pipelines.universal`, `com.unity.render-pipelines.core` — авто)
2. Удалить `ProjectSettings/URPProjectSettings.asset`, `Assets/Settings/UniversalRP.asset`, `UniversalRenderPipelineGlobalSettings.asset` (+ meta)
3. Очистить `ProjectSettings/GraphicsSettings.asset`: удалить `m_RenderPipelineGlobalSettingsMap:` детей, поставить `m_RenderPipelineGlobalSettingsMap: {}`
4. Очистить `ProjectSettings/QualitySettings.asset`: все `customRenderPipeline: {guid: ...}` → `{fileID: 0}` (важно — **не** оставлять guid!)
5. Удалить `Assets/Settings/Renderer2D.asset`, `DefaultVolumeProfile.asset`, `Scenes/URP2DSceneTemplate.unity` (+ meta)
6. Создать `Sprites-Default.mat` с GUID orphan-а, если SpriteRenderer-ы ссылаются на бывший URP-default
7. Удалить `MonoBehaviour` блоки из `.unity` файлов: UniversalAdditionalCameraData (на камере) и Light2D (на лайтах), плюс ссылки в `m_Component` родительских GO