using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 入力をまとめた窓口。新Input System / 旧Input Manager のどちらが有効でも動く。
    /// （Project Settings > Player > Active Input Handling の設定に自動で合わせる）
    /// </summary>
    public static class GameInput
    {
        /// <summary>移動入力。x=画面右、y=画面上。</summary>
        public static Vector2 Move()
        {
            Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
            }
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) v.x -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) v.x += 1;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v.y += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v.y -= 1;
#endif
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        /// <summary>塩を撃つ：左クリック / Z / Space</summary>
        public static bool Fire()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current; var k = Keyboard.current;
            return (m != null && m.leftButton.isPressed) || (k != null && (k.zKey.isPressed || k.spaceKey.isPressed));
#else
            return Input.GetMouseButton(0) || Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.Space);
#endif
        }

        /// <summary>クモの糸でガード：右クリック / X / 左Shift</summary>
        public static bool Guard()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current; var k = Keyboard.current;
            return (m != null && m.rightButton.isPressed) || (k != null && (k.xKey.isPressed || k.leftShiftKey.isPressed));
#else
            return Input.GetMouseButton(1) || Input.GetKey(KeyCode.X) || Input.GetKey(KeyCode.LeftShift);
#endif
        }

        /// <summary>一時停止：P / Esc（押した瞬間だけ true）</summary>
        public static bool PausePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && (k.pKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        /// <summary>マウス位置（スクリーン座標、左下原点）</summary>
        public static Vector2 MousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null ? m.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }
    }
}
