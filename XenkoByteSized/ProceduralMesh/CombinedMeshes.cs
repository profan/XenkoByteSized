using System;

using Xenko.Core;
using Xenko.Core.Mathematics;
using Xenko.Core.Collections;
using Xenko.Engine;
using Xenko.Graphics;
using Xenko.Graphics.GeometricPrimitives;
using Xenko.Rendering;

using Xenko.Extensions;
using System.Runtime.InteropServices;
using VertexType = XenkoByteSized.ProceduralMesh.VertexPositionNormalTextureColor;

namespace XenkoByteSized.ProceduralMesh
{

    class PoorMansMultiMesh : SyncScript
    {

        const int INITIAL_INSTANCE_COUNT = 16;

        private TrackingDictionary<Guid, SomeObjectInSpace> objects = new TrackingDictionary<Guid, SomeObjectInSpace>();
        private FastList<Matrix> matrices = new FastList<Matrix>(INITIAL_INSTANCE_COUNT);
        private FastList<Color4> colors = new FastList<Color4>(INITIAL_INSTANCE_COUNT);

        /* gpu pipeline stuff */
        private PipelineState pipelineState;

        /* gpu side data, our ModelComponent we use to render, no index buffer data so there is some... waste yes */
        private ModelComponent modelComponent;
        private VertexBufferBinding streamOutBufferBinding;
        private Buffer<Matrix> transformBuffer;
        private Buffer<Color4> colorBuffer;
        private EffectInstance streamShader;
        private Mesh streamBufferedMesh;

        private Mesh renderedMesh;
        public Mesh Mesh
        {
            get
            {
                return renderedMesh;
            }
            set
            {
                renderedMesh = value;
            }
        }

        public Material Material { get; set; }

        public System.Guid AddInstance(SomeObjectInSpace someObject)
        {
            objects[someObject.Id] = someObject;
            return someObject.Id;
        }

        public bool RemoveInstance(SomeObjectInSpace someObject)
        {
            return objects.Remove(someObject.Id);
        }

        private void CreateDeviceObjects()
        {

            var commandList = Game.GraphicsContext.CommandList;

            var shader = new EffectInstance(EffectSystem.LoadEffect("MultiMeshShader").WaitForResult());
            shader.UpdateEffect(GraphicsDevice);
            streamShader = shader;

            var outputDesc = new RenderOutputDescription(GraphicsDevice.Presenter.BackBuffer.Format);
            outputDesc.CaptureState(commandList);

            var pipeline = new PipelineStateDescription()
            {

                /* TODO: do we need all these? */
                BlendState = BlendStates.Default,
                RasterizerState = RasterizerStateDescription.Default,
                DepthStencilState = DepthStencilStates.None,
                Output = outputDesc,

                PrimitiveType = PrimitiveType.TriangleList,
                InputElements = VertexType.Layout.CreateInputElements(),
                EffectBytecode = shader.Effect.Bytecode,
                RootSignature = shader.RootSignature,

            };

            var newPipelineState = PipelineState.New(GraphicsDevice, ref pipeline);
            pipelineState = newPipelineState;

            var streamBuffer = Xenko.Graphics.Buffer.New<VertexType>(
                GraphicsDevice,
                INITIAL_INSTANCE_COUNT,
                BufferFlags.VertexBuffer | BufferFlags.StreamOutput
            );
            streamOutBufferBinding = new VertexBufferBinding(streamBuffer, VertexType.Layout, streamBuffer.ElementCount);

            transformBuffer = Xenko.Graphics.Buffer.Structured.New<Matrix>(
                GraphicsDevice,
                INITIAL_INSTANCE_COUNT,
                isUnorderedAccess: true
            );

            colorBuffer = Xenko.Graphics.Buffer.Structured.New<Color4>( 
                GraphicsDevice,
                INITIAL_INSTANCE_COUNT,
                isUnorderedAccess: true
            );

        }

        private void CheckBuffers()
        {

            var device = GraphicsDevice;
            var commandList = Game.GraphicsContext.CommandList;
            var totalIndices = renderedMesh.Draw.IndexBuffer.Count;

            int neededStreamBufferSize = objects.Count * totalIndices;
            if (neededStreamBufferSize > streamOutBufferBinding.Count)
            {
                streamOutBufferBinding.Buffer.Dispose(); // dispose the old buffer first
                var streamBuffer = Xenko.Graphics.Buffer.New<VertexType>(
                    device,
                    neededStreamBufferSize,
                    BufferFlags.VertexBuffer | BufferFlags.StreamOutput
                );
                streamOutBufferBinding = new VertexBufferBinding(streamBuffer, VertexType.Layout, streamBuffer.ElementCount);
            }

            int neededTransformBufferSize = objects.Count;
            if (neededTransformBufferSize > transformBuffer.ElementCount)
            {
                transformBuffer.Dispose(); // dispose old buffer first
                transformBuffer = Xenko.Graphics.Buffer.Structured.New(
                    device,
                    matrices.Items,
                    isUnorderedAccess: true
                );

                colorBuffer.Dispose(); // dispose old buffer first
                colorBuffer = Xenko.Graphics.Buffer.Structured.New(
                    device,
                    colors.Items,
                    isUnorderedAccess: true
                );
            }
            else
            {
                transformBuffer.SetData(commandList, matrices.Items);
                colorBuffer.SetData(commandList, colors.Items);
            }
        }

        private void PerformStreamOut()
        {

            var commandList = Game.GraphicsContext.CommandList;
            commandList.SetPipelineState(pipelineState);

            var meshDraw = renderedMesh.Draw;

            /* vertex buffer(s) */
            for (int i = 0; i < meshDraw.VertexBuffers.Length; i++)
            {
                var vertexBufferBinding = meshDraw.VertexBuffers[i];
                commandList.SetVertexBuffer(i, vertexBufferBinding.Buffer, meshDraw.StartLocation, vertexBufferBinding.Declaration.VertexStride);
            }

            var indexBuffer = meshDraw.IndexBuffer.Buffer;
            commandList.SetIndexBuffer(indexBuffer, 0, meshDraw.IndexBuffer.Is32Bit);

            commandList.SetStreamTargets(streamOutBufferBinding.Buffer);

            streamShader.Parameters.Set(MultiMeshShaderKeys.ModelTransforms, transformBuffer);
            streamShader.Parameters.Set(MultiMeshShaderKeys.ModelColors, colorBuffer);
            streamShader.Apply(Game.GraphicsContext);

            /* finally write to our streamout buffer */
            commandList.DrawIndexedInstanced(indexBuffer.ElementCount, objects.Count);
            commandList.SetStreamTargets(null);

        }

        static private Mesh CreateMesh(VertexBufferBinding bufferBinding)
        {

            var newMesh = new Mesh()
            {
                Draw = new MeshDraw()
                {
                    PrimitiveType = PrimitiveType.TriangleList,
                    VertexBuffers = new[] {
                        bufferBinding,
                    },
                    DrawCount = bufferBinding.Count
                }
            };

            return newMesh;

        }

        public override void Start()
        {

            CreateDeviceObjects();

            streamBufferedMesh = CreateMesh(streamOutBufferBinding);
            streamBufferedMesh.MaterialIndex = 0;

            var newModel = new Model();
            newModel.Meshes.Add(streamBufferedMesh);
            newModel.Materials.Add(Material); // FIXME: can be null at this point if not set

            modelComponent = new ModelComponent()
            {
                Model = newModel
            };

            Entity.Add(modelComponent);
        }

        private void UpdateBuffers()
        {

            CheckBuffers();

            /* update our MeshDraw to reflect current state */
            var vertexCount = streamOutBufferBinding.Count;
            streamBufferedMesh.Draw.VertexBuffers[0] = streamOutBufferBinding;
            streamBufferedMesh.Draw.DrawCount = vertexCount;

        }

        private void UpdateInstanceData()
        {

            /* FIXME: this is all somewhat stupid.. fix this once it at least runs */

            matrices.Clear();
            colors.Clear();
            foreach (var kv in objects)
            {
                matrices.Add(kv.Value.Entity.Transform.WorldMatrix);
                colors.Add(kv.Value.Color);
            }

        }

        public override void Update()
        {

            UpdateInstanceData();
            UpdateBuffers();
            PerformStreamOut();

        }

        public override void Cancel()
        {
            streamOutBufferBinding.Buffer.Dispose();
            transformBuffer.Dispose();
            colorBuffer.Dispose();
            streamShader.Dispose();
        }

    }

    class SomeObjectInSpace : SyncScript
    {

        public Vector3 Velocity;
        public Vector3 RotVelocity;
        public Color4 Color;

        public override void Update()
        {

            var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;

            if (Entity.Transform.Position.X > 64.0f || Entity.Transform.Position.X < -64.0f)
            {
                Velocity.X = -Velocity.X;
            }

            if (Entity.Transform.Position.Y > 64.0f || Entity.Transform.Position.Y < -64.0f)
            {
                Velocity.Y = -Velocity.Y;
            }

            if (Entity.Transform.Position.Z > 64.0f || Entity.Transform.Position.Z < -64.0f)
            {
                Velocity.Z = -Velocity.Z;
            }

            Entity.Transform.Rotation *=
                Quaternion.RotationX(RotVelocity.X * dt) *
                Quaternion.RotationY(RotVelocity.Y * dt) *
                Quaternion.RotationZ(RotVelocity.Z * dt);

            Entity.Transform.Position += Velocity * dt;

        }

    }

    public class CombinedMeshes : SyncScript
    {

        /* GPU side data */
        private PoorMansMultiMesh multiMesh;

        [Display("Material")]
        public Material meshMaterial;

        public override void Start()
        {

            var ourPrimitive = GeometricPrimitive.Cube.New(GraphicsDevice, 1.0f);
            var primitiveMeshDraw = ourPrimitive.ToMeshDraw();

            var newMultiMesh = new PoorMansMultiMesh()
            {
                Mesh = new Mesh()
                {
                    Draw = primitiveMeshDraw
                },
                Material = meshMaterial
            };

            var numInstances = 2048;
            var random = new Random();
            for (int i = 0; i < numInstances; ++i)
            {

                var randX = random.Next(-64, 64);
                var randY = random.Next(-64, 64);
                var randZ = random.Next(-64, 64);

                var velX = random.NextDouble() * 4.0;
                var velY = random.NextDouble() * 4.0;
                var velZ = random.NextDouble() * 4.0;
                var vel = new Vector3((float)velX, (float)velY, (float)velZ);

                var rotVelX = random.NextDouble();
                var rotVelY = random.NextDouble();
                var rotVelZ = random.NextDouble();
                var rotVel = new Vector3((float)rotVelX, (float)rotVelY, (float)rotVelZ);

                var r = random.NextDouble();
                var g = random.NextDouble();
                var b = random.NextDouble();
                var col = new Color4((float)r, (float)g, (float)b);

                var newEntity = new Entity(new Vector3(randX, randY, randZ));
                var newObjectInSpace = new SomeObjectInSpace() { Velocity = vel, RotVelocity = rotVel, Color = col };

                newEntity.Add(newObjectInSpace);
                newMultiMesh.AddInstance(newObjectInSpace);
                Entity.AddChild(newEntity);

            }

            Entity.Add(newMultiMesh);
            multiMesh = newMultiMesh;

        }

        public override void Update()
        {

        }

    }

    /// <summary>
    /// Describes a custom vertex format structure that contains position and color information. 
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct VertexPositionNormalTextureColor : IEquatable<VertexPositionNormalTextureColor>, IVertex
    {
        /// <summary>
        /// Initializes a new <see cref="VertexPositionNormalTextureColor"/> instance.
        /// </summary>
        /// <param name="position">The position of this vertex.</param>
        /// <param name="color">The color of this vertex.</param>
        /// <param name="textureCoordinate">UV texture coordinates.</param>
        public VertexPositionNormalTextureColor(Vector3 position, Vector3 normal, Vector2 textureCoordinate, Color4 color)
            : this()
        {
            Position = position;
            Normal = normal;
            TextureCoordinate = textureCoordinate;
            Color = color;
        }

        /// <summary>
        /// XYZ position.
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// XYZ position.
        /// </summary>
        public Vector3 Normal;

        /// <summary>
        /// UV texture coordinates.
        /// </summary>
        public Vector2 TextureCoordinate;

        /// <summary>
        /// The vertex color.
        /// </summary>
        public Color4 Color;


        /// <summary>
        /// Defines structure byte size.
        /// </summary>
        public static readonly int Size = 48;

        /// <summary>
        /// The vertex layout of this struct.
        /// </summary>
        public static readonly VertexDeclaration Layout = new VertexDeclaration(
            VertexElement.Position<Vector3>(),
            VertexElement.Normal<Vector3>(),
            VertexElement.TextureCoordinate<Vector2>(),
            VertexElement.Color<Color4>());
        

        public bool Equals(VertexPositionNormalTextureColor other)
        {
            return Position.Equals(other.Position) && Normal.Equals(other.Normal) && Color.Equals(other.Color) && TextureCoordinate.Equals(other.TextureCoordinate);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is VertexPositionNormalTextureColor && Equals((VertexPositionNormalTextureColor)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Position.GetHashCode();
                hashCode = (hashCode * 397) ^ Normal.GetHashCode();
                hashCode = (hashCode * 397) ^ TextureCoordinate.GetHashCode();
                hashCode = (hashCode * 397) ^ Color.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(VertexPositionNormalTextureColor left, VertexPositionNormalTextureColor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VertexPositionNormalTextureColor left, VertexPositionNormalTextureColor right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return string.Format("Position: {0}, Normal {1} Color: {2}, Texcoord: {3}", Position, Normal, TextureCoordinate, Color);
        }

        public VertexDeclaration GetLayout()
        {
            return Layout;
        }

        public void FlipWinding()
        {
            TextureCoordinate.X = (1.0f - TextureCoordinate.X);
        }
    }
}
