using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Xenko.Core;
using Xenko.Core.Annotations;
using Xenko.Rendering.Materials;
using Xenko.Rendering.Materials.ComputeColors;
using Xenko.Shaders;

namespace XenkoByteSized.ProceduralMesh {

    class OurMaterialKeys {

    }

    [DataContract("MaterialVertexFeature")]
    [Display("Vertex Shader")]
    public class MaterialVertexFeature : MaterialFeature, IMaterialDisplacementFeature {

        public const string OurVertexStream = "OurPosition";

        /// <summary>
        /// Initializes a new instance of the <see cref="MaterialVertexFeature"/> class.
        /// </summary>
        public MaterialVertexFeature() : this(new ComputeColor())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MaterialVertexFeature"/> class.
        /// </summary>
        /// <param name="displacementMap">The displacement map.</param>
        public MaterialVertexFeature(IComputeColor vertexShader) {
            VertexShader = vertexShader;
            Stage = DisplacementMapStage.Vertex;
        }

        /// <summary>
        /// Gets or sets the displacement map.
        /// </summary>
        /// <value>The displacement map.</value>
        /// <userdoc>
        /// The map containing the displacement offsets to apply onto the model vertex positions.
        /// </userdoc>
        [DataMember(10)]
        [Display("Vertex Shader")]
        [NotNull]
        public IComputeColor VertexShader { get; set; }

        /// <summary>
        /// Gets or sets a value indicating in which stage the displacement should occur.
        /// </summary>
        /// <userdoc>
        /// The value indicating in which stage the displacement will occur.
        /// </userdoc>
        [DataMember(40)]
        [DefaultValue(true)]
        [Display("Shader Stage")]
        public DisplacementMapStage Stage { get; set; }

        public override void GenerateShader(MaterialGeneratorContext context) {

            if (VertexShader == null) { return; }

            var materialStage = (MaterialShaderStage)Stage;

            // reset the displacement streams at the beginning of the stage
            context.AddStreamInitializer(materialStage, "OurVertexStream");

            // use... (FIXME: is this necessary? why?)
            context.UseStream(materialStage, OurVertexStream);

            // build the vertex shader
            var vertexShader = VertexShader;

            // Workaround to inform compute colors that sampling is occurring from a vertex shader
            context.IsNotPixelStage = materialStage != MaterialShaderStage.Pixel;
            context.SetStream(materialStage, OurVertexStream, vertexShader, MaterialKeys.DisplacementMap, MaterialKeys.DisplacementValue);
            context.IsNotPixelStage = false;

            // var scaleNormal = materialStage != MaterialShaderStage.Vertex;
            var positionMember = materialStage == MaterialShaderStage.Vertex ? "Position" : "PositionWS";
            var normalMember = materialStage == MaterialShaderStage.Vertex ? "meshNormal" : "normalWS";
            context.SetStreamFinalModifier<MaterialVertexFeature>(materialStage, new ShaderClassSource("MaterialVertexDisplacement", positionMember, normalMember));

        }

    }
}
