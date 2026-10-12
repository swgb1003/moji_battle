using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class GripDragTests
    {
        FighterBuildData left, right;
        int reduced;
        CustomizeScreen screen;
        RawImage viewport;
        Camera camera;
        PointerEventData pointer;

        [SetUp]
        public void Setup()
        {
            left = BuildRuntimeStore.Get(0).Clone(); right = BuildRuntimeStore.Get(1).Clone();
            reduced = PlayerPrefs.GetInt("MenuReducedMotion", 0);
            PlayerPrefs.SetInt("MenuReducedMotion", 0);
            TimeController.ResetAll();
            BuildRuntimeStore.Set(0, FighterBuildData.Default("一"));
        }
        [TearDown]
        public void Cleanup()
        {
            if (EventSystem.current != null)
                foreach (var module in EventSystem.current.GetComponents<BaseInputModule>()) module.enabled = true;
            TimeController.ResetAll(); PlayerPrefs.SetInt("MenuReducedMotion", reduced);
            GameFlow.ToTitle(); BuildRuntimeStore.Set(0, left); BuildRuntimeStore.Set(1, right);
            BuildRuntimeStore.Active = false;
        }
        IEnumerator Open()
        {
            GameFlow.ToCustomize(); yield return null; yield return null;
            yield return new WaitForSecondsRealtime(.85f);
            screen = Object.FindAnyObjectByType<CustomizeScreen>();
            viewport = Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Single(v => v.name == "PreviewViewport");
            camera = GameObject.Find("PreviewCamera").GetComponent<Camera>();
            // Drive real UGUI handlers deterministically, without the desktop mouse interfering.
            foreach (var module in EventSystem.current.GetComponents<BaseInputModule>()) module.enabled = false;
            pointer = new PointerEventData(EventSystem.current) { pointerId = -1, button = PointerEventData.InputButton.Left };
        }
        void Send<T>(ExecuteEvents.EventFunction<T> handler, Vector2 position) where T : IEventSystemHandler
        {
            pointer.position = position;
            ExecuteEvents.Execute(viewport.gameObject, pointer, handler);
        }
        Vector2 ScreenPoint(Vector3 world)
        {
            Vector3 uv = camera.WorldToViewportPoint(world);
            var rect = viewport.rectTransform;
            Vector2 local = rect.rect.min + new Vector2(uv.x * rect.rect.width, uv.y * rect.rect.height);
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local));
        }
        GlyphDefinitionRuntime Glyph()
        {
            var build = screen.Customizer.Current;
            Assert.IsTrue(GlyphCatalog.TryGet(build.character, build.fontType, CombatBalance.Default,
                GlyphCalibration.Default, out var glyph, out _));
            return GlyphFlip.Apply(glyph, build.weaponFlip);
        }
        Vector2 VisibleInk(bool far = false)
        {
            var glyph = Glyph(); var bounds = glyph.features.inkBounds; var pixels = glyph.texture.GetPixels32();
            var choices = new List<Vector2>();
            for (int y = bounds.yMin; y < bounds.yMax; y += 2)
            for (int x = bounds.xMin; x < bounds.xMax; x += 2)
            {
                if (pixels[y * glyph.texture.width + x].a < 128) continue;
                Vector2 px = new Vector2(x + .5f, y + .5f);
                Vector3 uv = camera.WorldToViewportPoint(screen.Preview.PixelToWorld(px));
                if (uv.x > .05f && uv.x < .95f && uv.y > .05f && uv.y < .95f) choices.Add(px);
            }
            Assert.IsNotEmpty(choices, "At least one stroke must be visible and draggable.");
            return far ? choices.Last() : choices.First();
        }
        void AssertFixed(Vector2 reference, Vector3 world, Quaternion rotation)
        {
            Assert.Less(Vector3.Distance(world, screen.Preview.PixelToWorld(reference)), .0001f, "Glyph slid under the pointer.");
            Assert.Less(Quaternion.Angle(rotation, screen.Preview.transform.Find("WeaponSprite").rotation), .001f, "Glyph rotated while editing.");
        }

        void AssertReadyPose()
        {
            var preview = screen.Preview;
            var balance = CombatBalance.Default;
            Vector3 grip = (Vector3)preview.feet + (Vector3)balance.shoulderLocal;
            var weapon = preview.transform.Find("WeaponSprite");
            Assert.Less(Vector3.Distance(grip, weapon.position), .0001f, "Confirmed grip must return to the fighter's hand immediately.");
            float ready = float.IsNaN(preview.Mods.readyPsi) ? balance.ReadyPsi(preview.Stats.weightScore) : preview.Mods.readyPsi;
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0, 0, ready - preview.Geometry.alpha0Deg), weapon.rotation), .001f,
                "Confirmed grip must show its new ready pose without waiting for pointer exit.");
            var arm = preview.transform.Find("ArmFront").GetComponent<LineRenderer>();
            Assert.Less(Vector3.Distance(grip, arm.GetPosition(arm.positionCount - 1)), .0001f);
            Assert.Less(Vector3.Distance(grip, preview.PixelToWorld(preview.Geometry.gripPx)), .0001f);
        }

        [UnityTest]
        public IEnumerator ConfirmedGrip_ShowsNewPoseWhilePointerRemainsOverPreview()
        {
            yield return Open();
            screen.Customizer.SetCharacter("鬱"); screen.Customizer.SetSize(WeaponSize.XL);
            screen.Customizer.SetFlip(WeaponFlip.Vertical);
            yield return null; yield return null;
            Vector2 start = ScreenPoint(screen.Preview.PixelToWorld(VisibleInk()));
            Vector2 end = ScreenPoint(screen.Preview.PixelToWorld(VisibleInk(true)));
            Send(ExecuteEvents.pointerEnterHandler, start); Send(ExecuteEvents.pointerDownHandler, start);
            Send(ExecuteEvents.beginDragHandler, end);
            Send(ExecuteEvents.pointerUpHandler, end); Send(ExecuteEvents.endDragHandler, end);
            yield return null; yield return null;
            Assert.IsTrue(screen.Preview.GripEditing); Assert.IsTrue(screen.Preview.Paused);
            AssertReadyPose();
            yield return new WaitForSecondsRealtime(.4f);
            AssertReadyPose();
        }

        [UnityTest]
        public IEnumerator GripButtons_UpdatePoseAndHandsBeforeNextFrame()
        {
            yield return Open();
            Send(ExecuteEvents.pointerEnterHandler, ScreenPoint(screen.Preview.PixelToWorld(VisibleInk())));
            foreach (var grip in new[] { GripType.Horizontal, GripType.TwoHanded, GripType.Reverse, GripType.OneHanded })
            {
                Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "Grip_" + grip).onClick.Invoke();
                AssertReadyPose();
                var preview = screen.Preview;
                Vector3 hand = preview.transform.Find("WeaponSprite").position;
                var arm = preview.transform.Find("ArmBack").GetComponent<LineRenderer>();
                Vector3 expected = hand;
                float length = preview.Geometry.comLocal.magnitude;
                if (grip == GripType.TwoHanded && length > .001f)
                    expected += preview.transform.Find("WeaponSprite").rotation * (Vector3)(preview.Geometry.comLocal / length
                        * Mathf.Min(length, CustomizeBalance.Default.secondHandOffset));
                Assert.Less(Vector3.Distance(expected, arm.GetPosition(arm.positionCount - 1)), .0001f);
            }
        }

        [UnityTest]
        public IEnumerator GripAndStyleChanges_ReuseUnchangedGeometryAndSprite()
        {
            yield return Open();
            screen.Customizer.SetCharacter("鬱"); screen.Customizer.SetSize(WeaponSize.XL);
            screen.Customizer.SetFlip(WeaponFlip.Both); screen.Customizer.SetGripPoint(new Vector2(.8f, .2f));
            var geometry = screen.Preview.Geometry;
            var sprite = screen.Preview.transform.Find("WeaponSprite").GetComponent<SpriteRenderer>().sprite;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 40; i++)
            {
                screen.Customizer.SetGrip((GripType)(i % 4));
                screen.Customizer.SetStyle((BattleStyle)(i % 4));
                Assert.AreSame(geometry, screen.Preview.Geometry, "Grip type and battle style do not change weapon geometry.");
                Assert.AreSame(sprite, screen.Preview.transform.Find("WeaponSprite").GetComponent<SpriteRenderer>().sprite,
                    "Grip type and battle style must not regenerate the weapon sprite.");
            }
            watch.Stop();
            Debug.Log($"Grip refresh: 80 UI changes, {watch.Elapsed.TotalMilliseconds:F2} ms total, {watch.Elapsed.TotalMilliseconds / 80:F3} ms/change.");
        }

        [UnityTest]
        public IEnumerator Drag_KeepsLargeAndFlippedGlyphsFixed_AndCommitsOnceOnDrop()
        {
            yield return Open();
            foreach (var ch in new[] { "一", "鬱" })
            {
                screen.Customizer.SetCharacter(ch); screen.Customizer.SetSize(WeaponSize.XL);
                screen.Customizer.SetFlip(ch == "鬱" ? WeaponFlip.Vertical : WeaponFlip.None);
                screen.Customizer.ResetGrip();
                yield return null; yield return null;
                Vector2 reference = VisibleInk(), endpoint = VisibleInk(true);
                Vector3 fixedWorld = screen.Preview.PixelToWorld(reference);
                Quaternion rotation = screen.Preview.transform.Find("WeaponSprite").rotation;
                var sprite = screen.Preview.transform.Find("WeaponSprite").GetComponent<SpriteRenderer>().sprite;
                Vector3 cameraPosition = camera.transform.position;
                Vector2 start = ScreenPoint(fixedWorld), end = ScreenPoint(screen.Preview.PixelToWorld(endpoint));
                var hits = new List<RaycastResult>();
                pointer.position = start; EventSystem.current.RaycastAll(pointer, hits);
                Assert.AreEqual(viewport.gameObject, hits.First().gameObject, "Preview must receive real pointer input.");
                int changed = 0; System.Action count = () => changed++;
                screen.Customizer.Changed += count;
                Send(ExecuteEvents.pointerEnterHandler, start);
                Send(ExecuteEvents.pointerDownHandler, start);
                Assert.IsTrue(screen.GripDragging);
                Send(ExecuteEvents.beginDragHandler, end);
                for (int i = 1; i <= 8; i++)
                {
                    Send(ExecuteEvents.dragHandler, Vector2.Lerp(start, end, i / 8f));
                    yield return null; yield return null;
                    AssertFixed(reference, fixedWorld, rotation);
                    Assert.IsFalse(screen.Customizer.Current.customGrip);
                    Assert.AreEqual(0, changed);
                    Assert.AreSame(sprite, screen.Preview.transform.Find("WeaponSprite").GetComponent<SpriteRenderer>().sprite);
                }
                screen.Preview.ProjectGrip(screen.Preview.PixelToWorld(endpoint), out var expected);
                if (ch == "鬱") MenuUiTests.Capture("grip_drag_in_progress", 1230, 690);
                Send(ExecuteEvents.pointerUpHandler, end);
                Send(ExecuteEvents.endDragHandler, end);
                AssertReadyPose();
                yield return null; yield return null;
                Assert.IsFalse(screen.GripDragging);
                Assert.IsTrue(screen.Customizer.Current.customGrip); Assert.AreEqual(1, changed);
                Assert.Less(Vector2.Distance(expected, screen.Customizer.Current.gripPoint), .0001f);
                var cb = CustomizeBalance.Default;
                Assert.Less(Vector2.Distance(WeaponGripController.GripFromPoint(Glyph(), expected,
                    cb.gripClampMin * .5f, 1f - (1f - cb.gripClampMax) * .5f), screen.Preview.Geometry.gripPx), .001f);
                AssertReadyPose();
                Assert.AreEqual(cameraPosition, camera.transform.position);
                if (ch == "鬱") MenuUiTests.Capture("grip_drag_dropped", 1230, 690);
                screen.Customizer.Changed -= count;
                Send(ExecuteEvents.pointerExitHandler, end);
                Assert.IsFalse(screen.Preview.GripEditing); Assert.IsFalse(screen.Preview.Paused);
            }
        }

        [UnityTest]
        public IEnumerator ClickAndOutsideDrop_DoNotChangeSavedGrip()
        {
            yield return Open();
            Vector2 start = ScreenPoint(screen.Preview.PixelToWorld(VisibleInk()));
            int changed = 0; screen.Customizer.Changed += () => changed++;
            Send(ExecuteEvents.pointerEnterHandler, start);
            Send(ExecuteEvents.pointerDownHandler, start);
            Send(ExecuteEvents.pointerUpHandler, start);
            Assert.IsFalse(screen.Customizer.Current.customGrip); Assert.AreEqual(0, changed);
            Send(ExecuteEvents.pointerDownHandler, start);
            Send(ExecuteEvents.beginDragHandler, start + Vector2.right * 20);
            Vector2 outside = new Vector2(-100, -100);
            Send(ExecuteEvents.pointerExitHandler, outside);
            yield return null;
            Assert.IsTrue(screen.Preview.GripEditing, "Leaving while captured must not move the glyph.");
            Send(ExecuteEvents.dragHandler, outside);
            Send(ExecuteEvents.pointerUpHandler, outside); Send(ExecuteEvents.endDragHandler, outside);
            Assert.IsFalse(screen.GripDragging); Assert.IsFalse(screen.Preview.GripEditing);
            Assert.IsFalse(screen.Preview.Paused);
            Assert.IsFalse(screen.Customizer.Current.customGrip); Assert.AreEqual(0, changed);
        }

        [UnityTest]
        public IEnumerator SideAndVsChanges_CancelCapturedDragWithoutEditingAnotherPlayer()
        {
            yield return Open();
            Vector2 start = ScreenPoint(screen.Preview.PixelToWorld(VisibleInk()));
            var other = screen.Customizer.Get(1).Clone();
            Send(ExecuteEvents.pointerEnterHandler, start); Send(ExecuteEvents.pointerDownHandler, start);
            Send(ExecuteEvents.beginDragHandler, start + Vector2.right * 20);
            screen.Customizer.SelectSide(1);
            Send(ExecuteEvents.pointerUpHandler, start); Send(ExecuteEvents.endDragHandler, start);
            Assert.IsFalse(screen.GripDragging); Assert.IsFalse(screen.Preview.GripEditing);
            Assert.AreEqual(other.customGrip, screen.Customizer.Current.customGrip);
            Assert.AreEqual(other.gripPoint, screen.Customizer.Current.gripPoint);
            yield return null; yield return null;
            start = ScreenPoint(screen.Preview.PixelToWorld(VisibleInk()));
            Send(ExecuteEvents.pointerEnterHandler, start); Send(ExecuteEvents.pointerDownHandler, start);
            Send(ExecuteEvents.beginDragHandler, start + Vector2.right * 20);
            screen.ShowVs();
            Send(ExecuteEvents.pointerUpHandler, start); Send(ExecuteEvents.endDragHandler, start);
            Assert.IsTrue(screen.VsVisible); Assert.IsFalse(screen.GripDragging); Assert.IsFalse(screen.Preview.GripEditing);
            screen.HideVs(); yield return null;
            Assert.IsFalse(screen.Preview.Paused);
            Assert.AreEqual(other.gripPoint, screen.Customizer.Current.gripPoint);
        }

        [UnityTest]
        public IEnumerator Hover_FreezesMidSwing_AndResetRestoresDefaultGrip()
        {
            yield return Open();
            yield return new WaitForSecondsRealtime(1.5f);
            Vector2 reference = VisibleInk(); Vector3 world = screen.Preview.PixelToWorld(reference);
            Quaternion rotation = screen.Preview.transform.Find("WeaponSprite").rotation;
            Vector2 start = ScreenPoint(world);
            Send(ExecuteEvents.pointerEnterHandler, start);
            yield return new WaitForSecondsRealtime(.4f);
            AssertFixed(reference, world, rotation);
            Send(ExecuteEvents.pointerDownHandler, start);
            Send(ExecuteEvents.beginDragHandler, start);
            Send(ExecuteEvents.pointerUpHandler, start); Send(ExecuteEvents.endDragHandler, start);
            Assert.IsTrue(screen.Customizer.Current.customGrip);
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "GripReset").onClick.Invoke();
            AssertReadyPose();
            yield return null; yield return null;
            Assert.IsFalse(screen.Customizer.Current.customGrip);
            Assert.AreEqual(.5f, screen.Customizer.Current.gripPosition);
            AssertReadyPose();
            Send(ExecuteEvents.pointerExitHandler, start);
            Assert.IsFalse(screen.Preview.Paused);
        }

        [UnityTest]
        public IEnumerator GripPointAndReset_UpdateImmediatelyAcrossFontsSizesAndFlips()
        {
            yield return Open();
            foreach (FontStyleId font in System.Enum.GetValues(typeof(FontStyleId)))
            foreach (WeaponSize size in System.Enum.GetValues(typeof(WeaponSize)))
            foreach (WeaponFlip flip in System.Enum.GetValues(typeof(WeaponFlip)))
            {
                screen.Customizer.SetCharacter("火"); screen.Customizer.SetFont(font);
                screen.Customizer.SetSize(size); screen.Customizer.SetFlip(flip);
                screen.SetPreviewPointerOver(true);
                screen.Customizer.SetGripPoint(new Vector2(.85f, .15f));
                AssertReadyPose();
                Assert.IsTrue(screen.Preview.Paused);
                var cb = CustomizeBalance.Default;
                var expected = FighterFactory.ResolveWeapon(screen.Customizer.Current, Glyph(), screen.Preview.Stats,
                    CombatBalance.Default, cb, out float mass);
                Assert.AreEqual(expected.gripPx, screen.Preview.Geometry.gripPx);
                Assert.AreEqual(expected.alpha0Deg, screen.Preview.Geometry.alpha0Deg);
                Assert.AreEqual(mass, screen.Preview.WeaponMass);
                var sprite = screen.Preview.transform.Find("WeaponSprite").GetComponent<SpriteRenderer>().sprite;
                Assert.AreEqual(screen.Preview.Geometry.gripPx, sprite.pivot);
                Assert.AreEqual(1f / screen.Preview.Geometry.scale, sprite.pixelsPerUnit);
                screen.Customizer.ResetGrip();
                AssertReadyPose();
                Assert.IsFalse(screen.Customizer.Current.customGrip);
                screen.SetPreviewPointerOver(false);
                Assert.IsFalse(screen.Preview.Paused);
                AssertReadyPose();
            }
        }
    }
}
