using System;

using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Core;
using Xenko.Rendering;
using Xenko.Graphics;
using Xenko.Rendering.Sprites;

namespace XenkoByteSized {
    public class Screen : StartupScript {
        
        [Display("Center")]
        public Vector2 center;

        [Display("Render Target")]
        public Texture renderTexture;

        [Display("Render Group")]
        public RenderGroup renderGroup;

        private SpriteComponent surface;

        public override void Start() {

            surface = new SpriteComponent() {
                RenderGroup = renderGroup,
                SpriteProvider = new SpriteFromTexture() {
                    Texture = renderTexture,
                    IsTransparent = false,
                    Center = center
                }
            };

            Entity.Add(surface);

        }

    }
}
