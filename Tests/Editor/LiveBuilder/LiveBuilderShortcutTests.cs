using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Клавиши Live Builder через Shortcut Manager: ярлыки на месте и с нужными клавишами, контексты следуют состоянию сцены,
    /// цепочка Esc, цифры при рисовании — в число, а не в смену инструмента. Ярлыки зовутся напрямую, как их зовёт Unity, — окна сцены в тестах нет.
    /// Профили клавиш общие на все проекты человека: проверки их не меняют, подписи — во временном профиле с возвратом активного.
    /// </summary>
    public class LiveBuilderShortcutTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private Object[] selectionBefore;
        private LiveBuilderTool toolBefore;
        private LiveBuilderBoxMode boxModeBefore;
        private LiveBuilderCutterShape cutterShapeBefore;
        private LiveBuilderSceneTool sceneTool;
        private LiveBuilderSceneKeyboard keyboard;


        [SetUp]
        public void OpenScene()
        {
            selectionBefore = Selection.objects;
            toolBefore = LiveBuilderToolPalette.Current;
            boxModeBefore = LiveBuilderBoxModeSettings.Mode;
            cutterShapeBefore = LiveBuilderCutterSettings.Shape;
            LiveBuilderBoxModeSettings.Mode = LiveBuilderBoxMode.Solid;
            Selection.objects = new Object[0];
            testScene = new LiveBuilderTestScene();
            sceneTool = ScriptableObject.CreateInstance<LiveBuilderSceneTool>();
            keyboard = sceneTool.Keyboard;
        }


        [TearDown]
        public void CloseScene()
        {
            foreach (var tool in LiveBuilderToolPalette.Tools)
                tool.Cancel();

            LiveBuilderBoxTool.IsNewModelRequested = false;
            LiveBuilderContourSelection.SetOwner(null);
            Object.DestroyImmediate(sceneTool);
            testScene.Dispose();
            LiveBuilderToolPalette.Current = toolBefore;
            LiveBuilderBoxModeSettings.Mode = boxModeBefore;
            LiveBuilderCutterSettings.Shape = cutterShapeBefore;
            Selection.objects = selectionBefore;
        }


        // ── Ярлыки ───────────────────────────────────────────────────────

        [Test]
        public void AllLiveBuilderShortcuts_AreRegistered()
        {
            var available = ShortcutManager.instance.GetAvailableShortcutIds().ToList();
            var expected = LiveBuilderShortcutIds.AllIds();

            foreach (var id in expected)
                Assert.Contains(id, available, "ярлык есть в окне Shortcuts");

            var stray = available.Where(id => id.StartsWith(LiveBuilderShortcutIds.Category) && !expected.Contains(id)).ToList();
            Assert.IsEmpty(stray, "в категории Live Builder нет лишнего: " + string.Join(", ", stray));
            Assert.IsFalse(available.Contains("FeatherKit/Live Builder"), "старая B под FeatherKit убрана");
            Assert.AreEqual(8, LiveBuilderToolPalette.Tools.Count, "по два ярлыка на каждый из восьми инструментов");
        }


        [Test]
        public void DefaultBindings_AreAsSpecified()
        {
            if (ShortcutManager.instance.activeProfileId != ShortcutManager.defaultProfileId)
                Assert.Ignore("активен не профиль по умолчанию — привязки могли переназначить");

            var expected = new Dictionary<string, KeyCode>
            {
                [LiveBuilderShortcutIds.ToggleLiveBuilder] = KeyCode.B,
                [LiveBuilderShortcutIds.GridStepDown] = KeyCode.LeftBracket,
                [LiveBuilderShortcutIds.GridStepUp] = KeyCode.RightBracket,
                [LiveBuilderShortcutIds.StoreyDown] = KeyCode.PageDown,
                [LiveBuilderShortcutIds.StoreyUp] = KeyCode.PageUp,
                [LiveBuilderShortcutIds.Rotate] = KeyCode.Tab,
                [LiveBuilderShortcutIds.TogglePointSnap] = KeyCode.S,
                [LiveBuilderShortcutIds.ToggleKeyHints] = KeyCode.Slash,
                [LiveBuilderShortcutIds.Times] = KeyCode.X,
                [LiveBuilderShortcutIds.ContourCloseOrOpen] = KeyCode.C,
                [LiveBuilderShortcutIds.ContourNudgeLeft] = KeyCode.LeftArrow,
                [LiveBuilderShortcutIds.ContourNudgeRight] = KeyCode.RightArrow,
                [LiveBuilderShortcutIds.ContourNudgeForward] = KeyCode.UpArrow,
                [LiveBuilderShortcutIds.ContourNudgeBack] = KeyCode.DownArrow
            };

            for (var digit = 0; digit <= 9; digit++)
                expected[LiveBuilderShortcutIds.DigitOf(digit)] = KeyCode.Alpha0 + digit;

            for (var index = 0; index < LiveBuilderToolPalette.Tools.Count; index++)
            {
                var tool = LiveBuilderToolPalette.Tools[index];
                expected[LiveBuilderShortcutIds.ToolOf(tool)] = KeyCode.Alpha1 + index;
                Assert.AreEqual(new ShortcutBinding(new KeyCombination(KeyCode.Alpha1 + index, ShortcutModifiers.Shift)),
                    ShortcutManager.instance.GetShortcutBinding(LiveBuilderShortcutIds.PreviousModeOf(tool)), "Shift + клавиша " + tool.Id);
            }

            foreach (var (id, key) in expected)
                Assert.AreEqual(new ShortcutBinding(new KeyCombination(key)), ShortcutManager.instance.GetShortcutBinding(id), id);

            Assert.AreEqual("2", LiveBuilderToolPalette.HotkeyOf(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>()), "подпись на плитке — из привязки");
        }


        [Test]
        public void SameDigit_ToolInIdle_NumberInDrawing()
        {
            var boxTool = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>();

            Assert.AreEqual(typeof(LiveBuilderIdleShortcutContext), ShortcutContextOf(LiveBuilderShortcutIds.ToolOf(boxTool)), "инструмент — пока ничего не начато");
            Assert.AreEqual(typeof(LiveBuilderDrawingShortcutContext), ShortcutContextOf(LiveBuilderShortcutIds.DigitOf(2)), "цифра — пока рисуешь");
            Assert.AreEqual(typeof(LiveBuilderDrawingShortcutContext), ShortcutContextOf(LiveBuilderShortcutIds.Times), "X — только пока рисуешь");
            Assert.AreEqual(typeof(SceneView), ShortcutContextOf(LiveBuilderShortcutIds.ToggleLiveBuilder), "B — и при выключенном");
        }


        [Test]
        public void ToolShortcut_SelectsTool()
        {
            LiveBuilderToolPalette.Current = LiveBuilderToolPalette.Get<LiveBuilderSelectTool>();
            var wallTool = LiveBuilderToolPalette.Get<LiveBuilderWallTool>();

            InvokeShortcut(LiveBuilderShortcutIds.ToolOf(wallTool));

            Assert.AreSame(wallTool, LiveBuilderToolPalette.Current);
        }


        // ── Инструменты и режимы ─────────────────────────────────────────

        [Test]
        public void ToolLineup_AtMostEight_InSpecifiedOrder()
        {
            var ids = LiveBuilderToolPalette.Tools.Select(tool => tool.Id).ToArray();
            CollectionAssert.AreEqual(new[] { "Select", "Box", "Cut", "Wall", "Building", "Stairs", "Module", "Ruler" }, ids);
        }


        // Клавиша выбирает инструмент в запомненном режиме; ещё раз — следующий режим, Shift — предыдущий; Shift у другого инструмента — только выбрать.
        [Test]
        public void ToolKeyAgain_NextMode_ShiftKey_PreviousMode()
        {
            var boxTool = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>();
            var cutTool = LiveBuilderToolPalette.Get<LiveBuilderCutTool>();
            LiveBuilderToolPalette.Current = LiveBuilderToolPalette.Get<LiveBuilderSelectTool>();

            InvokeShortcut(LiveBuilderShortcutIds.ToolOf(boxTool));
            Assert.AreSame(boxTool, LiveBuilderToolPalette.Current);
            Assert.AreEqual(LiveBuilderBoxMode.Solid, LiveBuilderBoxModeSettings.Mode, "выбор — без смены режима");

            InvokeShortcut(LiveBuilderShortcutIds.ToolOf(boxTool));
            Assert.AreEqual(LiveBuilderBoxMode.Room, LiveBuilderBoxModeSettings.Mode, "«2» ещё раз — Room");

            InvokeShortcut(LiveBuilderShortcutIds.PreviousModeOf(boxTool));
            Assert.AreEqual(LiveBuilderBoxMode.Solid, LiveBuilderBoxModeSettings.Mode, "Shift+2 — назад к Solid");

            InvokeShortcut(LiveBuilderShortcutIds.PreviousModeOf(boxTool));
            Assert.AreEqual(LiveBuilderBoxMode.Column, LiveBuilderBoxModeSettings.Mode, "по кругу — последний");

            LiveBuilderCutterSettings.Shape = LiveBuilderCutterShape.Arch;
            InvokeShortcut(LiveBuilderShortcutIds.PreviousModeOf(cutTool));
            Assert.AreSame(cutTool, LiveBuilderToolPalette.Current, "Shift+3 у другого инструмента — выбрать «Cut»");
            Assert.AreEqual(LiveBuilderCutterShape.Arch, LiveBuilderCutterSettings.Shape, "режим запомнен и не тронут");

            InvokeShortcut(LiveBuilderShortcutIds.ToolOf(cutTool));
            Assert.AreEqual(LiveBuilderCutterShape.Circle, LiveBuilderCutterSettings.Shape, "«3» ещё раз — следующая форма");

            var wallTool = LiveBuilderToolPalette.Get<LiveBuilderWallTool>();
            InvokeShortcut(LiveBuilderShortcutIds.ToolOf(wallTool));
            InvokeShortcut(LiveBuilderShortcutIds.ToolOf(wallTool));
            Assert.AreSame(wallTool, LiveBuilderToolPalette.Current, "без режимов клавиша ещё раз ничего не меняет");
        }


        // Запомненные «Комната» и «Проём» прежних версий — «Коробка» в режиме Room и «Cut».
        [Test]
        public void StoredRoomTool_BecomesBoxInRoomMode()
        {
            Assert.AreEqual("Box", LiveBuilderToolPalette.MigrateStoredId("Room"));
            Assert.AreEqual(LiveBuilderBoxMode.Room, LiveBuilderBoxModeSettings.Mode);
            Assert.AreEqual("Cut", LiveBuilderToolPalette.MigrateStoredId("Opening"));
            Assert.AreEqual("Wall", LiveBuilderToolPalette.MigrateStoredId("Wall"), "прочие — как есть");
        }


        // ── Контексты ────────────────────────────────────────────────────

        [Test]
        public void Contexts_FollowSceneState()
        {
            AssertActive(State(), "ничего не начато", LiveBuilderShortcutContextKind.Scene, LiveBuilderShortcutContextKind.Idle);

            AssertActive(State(isToolActive: false), "Live Builder выключен — клавиши Unity");
            AssertActive(State(isSceneViewFocused: false), "в фокусе другое окно");
            AssertActive(State(isEditingTextField: true), "печатают в поле");
            AssertActive(State(isOtherHandleDragged: true), "тянут чужую ручку");
            AssertActive(State(isCameraFlying: true), "камера летит — WASD и S её");
            AssertActive(State(isAnythingStarted: true, isToolBusy: true), "рисую", LiveBuilderShortcutContextKind.Scene, LiveBuilderShortcutContextKind.Drawing);

            AssertActive(State(isContourShown: true), "видны точки",
                LiveBuilderShortcutContextKind.Scene, LiveBuilderShortcutContextKind.Idle, LiveBuilderShortcutContextKind.Contour);
            AssertActive(State(isContourShown: true, isContourPointSelected: true), "выбраны точки", LiveBuilderShortcutContextKind.Scene,
                LiveBuilderShortcutContextKind.Idle, LiveBuilderShortcutContextKind.Contour, LiveBuilderShortcutContextKind.ContourPoints);
            AssertActive(State(isContourShown: true, isContourPointSelected: true, isAnythingStarted: true), "тянут точку", LiveBuilderShortcutContextKind.Scene);
        }

        private static LiveBuilderSceneKeyState State(bool isToolActive = true, bool isSceneViewFocused = true, bool isEditingTextField = false,
                                                      bool isOtherHandleDragged = false, bool isCameraFlying = false, bool isAnythingStarted = false, bool isToolBusy = false,
                                                      bool isContourShown = false, bool isContourPointSelected = false)
        {
            return new LiveBuilderSceneKeyState
            {
                IsToolActive = isToolActive, IsSceneViewFocused = isSceneViewFocused, IsEditingTextField = isEditingTextField,
                IsOtherHandleDragged = isOtherHandleDragged, IsCameraFlying = isCameraFlying, IsAnythingStarted = isAnythingStarted, IsToolBusy = isToolBusy,
                IsContourShown = isContourShown, IsContourPointSelected = isContourPointSelected
            };
        }

        private static void AssertActive(LiveBuilderSceneKeyState state, string message, params LiveBuilderShortcutContextKind[] activeKinds)
        {
            foreach (LiveBuilderShortcutContextKind kind in System.Enum.GetValues(typeof(LiveBuilderShortcutContextKind)))
                Assert.AreEqual(activeKinds.Contains(kind), LiveBuilderShortcutContexts.IsActive(kind, state), message + ": " + kind);
        }


        // ── Esc, Enter, набор ────────────────────────────────────────────

        [Test]
        public void Cancel_Chain()
        {
            var boxTool = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>();
            LiveBuilderToolPalette.Current = boxTool;
            var driver = new LiveBuilderToolDriver(boxTool);

            driver.Drag(FloorRay(0f, 0f), FloorRay(4f, 3f));
            InvokeShortcut(LiveBuilderShortcutIds.DigitOf(5), new LiveBuilderDrawingShortcutContext(keyboard));
            Assert.IsTrue(keyboard.Cancel());
            Assert.IsTrue(boxTool.IsBusy, "1. Esc стёр набранное число — коробка ещё рисуется");

            Assert.IsTrue(keyboard.Cancel());
            Assert.IsFalse(boxTool.IsBusy, "2. начатое брошено");
            Assert.AreEqual(0, InTestScene<BoxModelPart>().Length, "брошенная коробка не встала");

            var wall = testScene.CreateWall(Vector3.zero, new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 4f));
            Selection.activeGameObject = wall.gameObject;
            LiveBuilderContourSelection.SetOwner(wall);
            LiveBuilderContourSelection.Add(0, 1);
            LiveBuilderBoxTool.IsNewModelRequested = true;

            Assert.IsTrue(keyboard.Cancel());
            Assert.AreEqual(0, LiveBuilderContourSelection.Knots.Count, "3. сняты выбранные точки");
            Assert.IsTrue(LiveBuilderBoxTool.IsNewModelRequested, "«Новый» — следующим");

            Assert.IsTrue(keyboard.Cancel());
            Assert.IsFalse(LiveBuilderBoxTool.IsNewModelRequested, "4. снят «Новый»");
            Assert.AreSame(boxTool, LiveBuilderToolPalette.Current, "инструмент тот же");

            Assert.IsTrue(keyboard.Cancel());
            Assert.IsInstanceOf<LiveBuilderSelectTool>(LiveBuilderToolPalette.Current, "5. на «Выделение»");
            Assert.AreSame(wall.gameObject, Selection.activeGameObject, "выделение пока на месте");

            Assert.IsTrue(keyboard.Cancel());
            Assert.IsNull(Selection.activeGameObject, "6. выделение снято");

            Assert.IsFalse(keyboard.Cancel(), "дальше отменять нечего — Esc не наш");
        }


        [Test]
        public void DigitWhileDrawing_GoesToNumber_NotToTool()
        {
            var boxTool = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>();
            LiveBuilderToolPalette.Current = boxTool;
            new LiveBuilderToolDriver(boxTool).Drag(FloorRay(0f, 0f), FloorRay(4f, 3f));

            var state = keyboard.KeyState;
            state.IsToolActive = true;
            state.IsSceneViewFocused = true;
            state.IsEditingTextField = false;
            Assert.IsTrue(LiveBuilderShortcutContexts.IsActive(LiveBuilderShortcutContextKind.Drawing, state), "рисую — цифры набирают число");
            Assert.IsFalse(LiveBuilderShortcutContexts.IsActive(LiveBuilderShortcutContextKind.Idle, state), "и не выбирают инструмент");

            InvokeShortcut(LiveBuilderShortcutIds.DigitOf(3), new LiveBuilderDrawingShortcutContext(keyboard));
            Assert.IsTrue(keyboard.HandleUnboundKey(KeyCode.Return), "Enter — готово");
            LiveBuilderDraftBuilds.FinishAll();

            Assert.AreSame(boxTool, LiveBuilderToolPalette.Current, "«3» не сменила инструмент");
            Assert.AreEqual(3f, InTestScene<BoxModelPart>().Single().Size.y, 0.001f, "«3» — высота коробки");
        }


        [Test]
        public void TimesWhileDragging_TypesBothSides()
        {
            var boxTool = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>();
            LiveBuilderToolPalette.Current = boxTool;
            var driver = new LiveBuilderToolDriver(boxTool);
            var drawing = new LiveBuilderDrawingShortcutContext(keyboard);

            driver.Hold(FloorRay(0f, 0f), FloorRay(1f, 1f));
            InvokeShortcut(LiveBuilderShortcutIds.DigitOf(4), drawing);
            InvokeShortcut(LiveBuilderShortcutIds.Times, drawing);
            InvokeShortcut(LiveBuilderShortcutIds.DigitOf(3), drawing);
            driver.Release();
            InvokeShortcut(LiveBuilderShortcutIds.DigitOf(2), drawing);
            Assert.IsTrue(keyboard.Confirm());
            LiveBuilderDraftBuilds.FinishAll();

            var size = InTestScene<BoxModelPart>().Single().Size;
            Assert.AreEqual(4f, size.x, 0.001f, "«4 X 3» — по X");
            Assert.AreEqual(3f, size.z, 0.001f, "и по Z");
            Assert.AreEqual(2f, size.y, 0.001f, "высота — потом");
        }


        [Test]
        public void Confirm_FinishesBoxDepth()
        {
            var boxTool = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>();
            LiveBuilderToolPalette.Current = boxTool;
            var drawing = new LiveBuilderDrawingShortcutContext(keyboard);

            new LiveBuilderToolDriver(boxTool).Drag(FloorRay(0f, 0f), FloorRay(4f, 3f));
            InvokeShortcut(LiveBuilderShortcutIds.DigitOf(1), drawing);
            Assert.IsTrue(keyboard.HandleUnboundKey(KeyCode.Comma), "запятая — без ярлыка, окно сцены отдаёт её само");
            InvokeShortcut(LiveBuilderShortcutIds.DigitOf(5), drawing);
            Assert.IsTrue(keyboard.HandleUnboundKey(KeyCode.KeypadEnter), "Enter цифрового блока — тоже «готово»");
            LiveBuilderDraftBuilds.FinishAll();

            Assert.IsFalse(boxTool.IsBusy, "готово — инструмент свободен");
            Assert.AreEqual(1.5f, InTestScene<BoxModelPart>().Single().Size.y, 0.001f);
            Assert.IsFalse(keyboard.Confirm(), "ничего не рисуешь — Enter не наш");
        }


        // ── Подписи клавиш ───────────────────────────────────────────────

        // Подписи — из привязок: Box на Q и Rotate на R во временном профиле — значок и шпаргалка следом.
        [Test]
        public void KeyHints_ShowCurrentBinding()
        {
            var manager = ShortcutManager.instance;
            var activeProfile = manager.activeProfileId;
            var temporaryProfile = "FeatherKit Live Builder Test " + System.Guid.NewGuid().ToString("N");
            var boxTool = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>();
            var stairsTool = LiveBuilderToolPalette.Get<LiveBuilderStairsTool>();
            manager.CreateProfile(temporaryProfile);

            try
            {
                manager.activeProfileId = temporaryProfile;
                manager.RebindShortcut(LiveBuilderShortcutIds.ToolOf(boxTool), new ShortcutBinding(new KeyCombination(KeyCode.Q)));
                manager.RebindShortcut(LiveBuilderShortcutIds.Rotate, new ShortcutBinding(new KeyCombination(KeyCode.R)));

                Assert.AreEqual("Q", LiveBuilderToolPalette.HotkeyOf(boxTool), "подпись значка");
                var stairsKeys = KeysOf(stairsTool.KeyHints);
                CollectionAssert.Contains(stairsKeys, "R", "шпаргалка лестницы");
                CollectionAssert.DoesNotContain(stairsKeys, "Tab");
            }
            finally
            {
                manager.activeProfileId = activeProfile;
                manager.DeleteProfile(temporaryProfile);
            }

            Assert.AreEqual(activeProfile, manager.activeProfileId, "профиль человека вернулся");
        }


        // Строка подсказки режется на текст и плашки; Enter и Esc — постоянные плашки.
        [Test]
        public void Hint_SplitsIntoTextAndKeys()
        {
            var parts = LiveBuilderHintKeys.PartsOf("Drag — box " + LiveBuilderHintKeys.Enter + " done " + LiveBuilderHintKeys.Esc + " cancel");

            Assert.AreEqual(new[] { ("Drag — box", false), ("Enter", true), ("done", false), ("Esc", true), ("cancel", false) }, parts.ToArray());
            Assert.AreEqual("Drag — box Enter done Esc cancel", LiveBuilderHintKeys.PlainTextOf("Drag — box " + LiveBuilderHintKeys.Enter + " done " +
                                                                                                LiveBuilderHintKeys.Esc + " cancel"));
        }


        // ── Совпадения с Unity ───────────────────────────────────────────

        [Test]
        public void SameKeysAsUnity_InDefaultProfile_AreTwoAndSAndArrows()
        {
            if (ShortcutManager.instance.activeProfileId != ShortcutManager.defaultProfileId)
                Assert.Ignore("активен не профиль по умолчанию");

            var pairs = LiveBuilderShortcutConflicts.Find().Select(conflict => (conflict.UnityShortcutId, conflict.LiveBuilderShortcutId)).ToList();
            var boxId = LiveBuilderShortcutIds.ToolOf(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());

            Assert.Contains(("Scene View/Toggle 2D Mode", boxId), pairs);
            Assert.Contains(("Scene View/Toggle 2D Mode", LiveBuilderShortcutIds.DigitOf(2)), pairs);
            Assert.Contains(("Overlays/Show Tool Settings", LiveBuilderShortcutIds.TogglePointSnap), pairs);
            Assert.Contains(("3D Viewport/Fly Mode Left (Alt)", LiveBuilderShortcutIds.ContourNudgeLeft), pairs);
            Assert.IsFalse(pairs.Any(pair => pair.LiveBuilderShortcutId == LiveBuilderShortcutIds.Times), "X Unity глобальный — свой контекст сильнее, не спорит");
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Подписи плашек в строках шпаргалки.
        private static List<string> KeysOf(IEnumerable<string> lines)
        {
            return lines.SelectMany(LiveBuilderHintKeys.PartsOf).Where(part => part.IsKey).Select(part => part.Text).ToList();
        }


        // Зовёт метод ярлыка так же, как Shortcut Manager: с контекстом в аргументах.
        private static void InvokeShortcut(string id, IShortcutContext context = null)
        {
            var method = ShortcutMethodOf(id, out _);
            var arguments = method.GetParameters().Length == 1 ? new object[] { new ShortcutArguments { context = context } } : null;
            method.Invoke(null, arguments);
        }

        private static System.Type ShortcutContextOf(string id)
        {
            ShortcutMethodOf(id, out var contextType);
            return contextType;
        }

        private static MethodInfo ShortcutMethodOf(string id, out System.Type contextType)
        {
            const BindingFlags anyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var type in typeof(LiveBuilderSceneTool).Assembly.GetTypes())
            foreach (var method in type.GetMethods(anyStatic))
            foreach (var attribute in method.GetCustomAttributesData())
            {
                if (attribute.AttributeType != typeof(ShortcutAttribute) || (string)attribute.ConstructorArguments[0].Value != id)
                    continue;

                contextType = attribute.ConstructorArguments.Count > 1 ? attribute.ConstructorArguments[1].Value as System.Type : null;
                return method;
            }

            Assert.Fail("нет метода с ярлыком " + id);
            contextType = null;
            return null;
        }

        // Луч сверху на пол текущего этажа, в метрах от начала тестовой сцены.
        private static Ray FloorRay(float x, float z)
        {
            var floor = LiveBuilderStoreys.CurrentFloorHeight;
            return new Ray(new Vector3(Origin.x + x, floor + 10f, Origin.z + z), Vector3.down);
        }

        private static T[] InTestScene<T>() where T : Component
        {
            var scene = SceneManager.GetActiveScene();
            return Object.FindObjectsByType<T>(FindObjectsInactive.Exclude).Where(component => component.gameObject.scene == scene).ToArray();
        }
    }
}
