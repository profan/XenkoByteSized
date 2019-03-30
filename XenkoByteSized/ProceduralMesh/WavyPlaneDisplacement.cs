using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xenko.Core.Mathematics;
using Xenko.Input;
using Xenko.Engine;
using Xenko.Rendering;
using Xenko.Rendering.Materials;
using Xenko.Graphics;
using Xenko.Rendering.Materials.ComputeColors;
using XenkoByteSized.ProceduralMesh;

namespace XenkoByteSized {
    public class WavyPlaneDisplacement : StartupScript {

        private ModelComponent modelComponent;
        private EffectInstance wavyShader;

        public override void Start() {

            var newWavyShader = new EffectInstance(EffectSystem.LoadEffect("WavyPlaneVertices").WaitForResult());
            newWavyShader.UpdateEffect(GraphicsDevice);
            wavyShader = newWavyShader;

            Material newVertexColorMaterial = Material.New(GraphicsDevice, new MaterialDescriptor {
                Attributes = new MaterialAttributes {
                    Displacement = new MaterialVertexFeature(),
                    Diffuse = new MaterialDiffuseMapFeature(new ComputeVertexStreamColor()),
                    DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                    CullMode = CullMode.None,
                },
            });

            modelComponent = Entity.Get<ModelComponent>();
            modelComponent.Model.Add(newVertexColorMaterial);

        }

        public override void Cancel() {
            wavyShader.Dispose();
        }

    }
}
