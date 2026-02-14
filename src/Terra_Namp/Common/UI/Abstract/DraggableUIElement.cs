// Credit to Scalie for DraggableUIState - https://github.com/ScalarVector1/DragonLens/blob/407a54e45d7a4828f660b46988feaf86092249b3/Content/GUI/DraggableUIState.cs#L10

using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace Terra_Namp.Common.UI.Abstract
{
    public abstract class DraggableUIElement : SmartUIElement
    {
        /// <summary>
        /// The top-left of the main window
        /// </summary>
        private Vector2? basePos;

        private bool dragging;
        private Vector2 dragOff;
        private bool needsRecalcNextDraw;

        /// <summary>
        /// The area where the user can click and drag to move the main window
        /// </summary>
        public abstract Rectangle DragBox { get; }

        /// <summary>
        /// Where the main window will be placed initially
        /// </summary>
        public abstract Vector2 DefaultPosition { get; }

        public virtual void SafeOnInitialize() { }

        public virtual void DraggableUpdate(GameTime gameTime) { }

        public virtual void DraggableDraw(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch) { }

        protected virtual void OnDragEnd(Vector2 position) { }

        /// <summary>
        /// Programmatically set position, bypassing DefaultPosition initialization.
        /// </summary>
        public void SetPositionDirect(float x, float y)
        {
            basePos = new Vector2(x, y);
            needsRecalcNextDraw = true;
        }

        public sealed override void OnInitialize()
        {
            SafeOnInitialize();

            base.OnInitialize();
        }

        private Vector2 ClampedDefaultBasePos(Rectangle size)
        {
            float x = DefaultPosition.X * Main.screenWidth - size.Width / 2f;
            float y = DefaultPosition.Y * Main.screenHeight - size.Height / 2f;
            x = MathHelper.Clamp(x, 0, Main.screenWidth - size.Width);
            y = MathHelper.Clamp(y, 0, Main.screenHeight - size.Height);
            return new Vector2(x, y);
        }

        public sealed override void SafeUpdate(GameTime gameTime)
        {
            Rectangle size = GetDimensions().ToRectangle();

            if (basePos == null)
            {
                basePos = ClampedDefaultBasePos(size);
                needsRecalcNextDraw = true;
            }

            if (!Main.mouseLeft && dragging)
            {
                dragging = false;
                OnDragEnd(basePos.Value);
            }

            if (DragBox.Contains(Main.MouseScreen.ToPoint()) && Main.mouseLeft || dragging)
            {
                dragging = true;

                if (dragOff == Vector2.Zero)
                {
                    dragOff = Main.MouseScreen - basePos.Value;
                }

                // Mark that position needs updating in next Draw() call
                // This ensures smooth dragging at high FPS (160+)
                needsRecalcNextDraw = true;
            }
            else
            {
                dragOff = Vector2.Zero;
            }

            // Block game scroll wheel when mouse is over the entire panel (not just drag box)
            if (size.Contains(Main.MouseScreen.ToPoint()))
            {
                Main.LocalPlayer.mouseInterface = true;
                PlayerInput.LockVanillaMouseScroll("Terra_Namp: MainPanel");
            }

            // If the box was somehow dragged offscreen reset its position.
            // Skip panels intentionally hidden offscreen (x < -1000) via SetPositionDirect.
            bool intentionallyHidden = basePos.HasValue && basePos.Value.X < -1000;
            if (!dragging && !intentionallyHidden && !size.Intersects(new Rectangle(0, 0, Main.screenWidth, Main.screenHeight)))
            {
                basePos = ClampedDefaultBasePos(size);
                AdjustPositions(basePos.Value);
                Recalculate();
            }

            DraggableUpdate(gameTime);
        }

        public sealed override void Draw(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
        {
            if (needsRecalcNextDraw || dragging)
            {
                needsRecalcNextDraw = false;

                Rectangle size = GetDimensions().ToRectangle();

                if (dragging)
                {
                    Vector2 newPos = Main.MouseScreen - dragOff;
                    newPos.X = MathHelper.Clamp(newPos.X, 0, Main.screenWidth - size.Width);
                    newPos.Y = MathHelper.Clamp(newPos.Y, 0, Main.screenHeight - size.Height);
                    basePos = newPos;
                }

                // Clamp all visible panels to screen bounds before applying position
                if (basePos.HasValue && basePos.Value.X > -1000)
                {
                    basePos = new Vector2(
                        MathHelper.Clamp(basePos.Value.X, 0, Main.screenWidth - size.Width),
                        MathHelper.Clamp(basePos.Value.Y, 0, Main.screenHeight - size.Height));
                }

                AdjustPositions(basePos.Value);
                Recalculate();
            }

            DraggableDraw(spriteBatch);
            base.Draw(spriteBatch);
        }

        /// <summary>
        /// You should adjust the position of all child elements of your UIState here so they move when the window is being dragged.
        /// </summary>
        /// <param name="newPos">The new position of the base window</param>
        public virtual void AdjustPositions(Vector2 newPos)
        {
            Left.Set(newPos.X, 0);
            Top.Set(newPos.Y, 0);
        }
    }
}
