using Xenko.Core.Mathematics;
using Xenko.Core.Collections;
using Xenko.Engine;
using Xenko.Rendering;
using Xenko.Graphics;
using Xenko.Graphics.GeometricPrimitives;
using Xenko.Extensions;
using System.Runtime.InteropServices;
using System;
using Xenko.Rendering.Materials;
using Xenko.Rendering.Materials.ComputeColors;
using Xenko.Core;
using Xenko.Rendering.Images;

namespace XenkoByteSized.ProceduralMesh {

    class PoorMansMultiMesh : SyncScript {

        const int INITIAL_INSTANCE_COUNT = 16;

        private TrackingDictionary<System.Guid, TransformComponent> transforms = new TrackingDictionary<System.Guid, TransformComponent>();
        private FastList<Matrix> matrices = new FastList<Matrix>(INITIAL_INSTANCE_COUNT);

        /* gpu pipeline stuff */
        private PipelineState pipelineState;

        /* gpu side data, our ModelComponent we use to render, no index buffer data so there is some... waste yes */
        private ModelComponent modelComponent;
        private VertexBufferBinding streamOutBufferBinding;
        private Buffer<Matrix> transformBuffer;
        private EffectInstance streamShader;
        private Mesh streamBufferedMesh;

        private Mesh renderedMesh;
        public Mesh Mesh {
            get {
                return renderedMesh;
            }
            set {
                renderedMesh = value;
            }
        }

        public Material Material { get; set; }

        public System.Guid AddInstance(TransformComponent transform) {
            transforms[transform.Id] = transform;
            return transform.Id;
        }

        public bool RemoveInstance(TransformComponent transform) {
            return transforms.Remove(transform.Id);
        }

        private void CreateDeviceObjects() {

            var commandList = Game.GraphicsContext.CommandList;

            var shader = new EffectInstance(EffectSystem.LoadEffect("MultiMeshShader").WaitForResult());
            shader.UpdateEffect(GraphicsDevice);
            streamShader = shader;

            var outputDesc = new RenderOutputDescription(GraphicsDevice.Presenter.BackBuffer.Format);
            outputDesc.CaptureState(commandList);

            var pipeline = new PipelineStateDescription() {

                /* TODO: do we need all these? */
                BlendState = BlendStates.Default,
                RasterizerState = RasterizerStateDescription.Default,
                DepthStencilState = DepthStencilStates.None,
                Output = outputDesc,

                PrimitiveType = PrimitiveType.TriangleList,
                InputElements = VertexPositionNormalColor.Layout.CreateInputElements(),
                EffectBytecode = shader.Effect.Bytecode,
                RootSignature = shader.RootSignature,

            };

            var newPipelineState = PipelineState.New(GraphicsDevice, ref pipeline);
            pipelineState = newPipelineState;

            var streamBuffer = Xenko.Graphics.Buffer.New<VertexPositionNormalColor>(
                GraphicsDevice,
                INITIAL_INSTANCE_COUNT,
                BufferFlags.VertexBuffer | BufferFlags.StreamOutput,
                GraphicsResourceUsage.Default
            );
            streamOutBufferBinding = new VertexBufferBinding(streamBuffer, VertexPositionNormalColor.Layout, streamBuffer.ElementCount);

            var newTransformBuffer = Xenko.Graphics.Buffer.Structured.New<Matrix>(
                GraphicsDevice,
                INITIAL_INSTANCE_COUNT,
                isUnorderedAccess: true
            );
            transformBuffer = newTransformBuffer;

        }

        private void CheckBuffers() {

            var device = GraphicsDevice;
            var commandList = Game.GraphicsContext.CommandList;
            var totalIndices = renderedMesh.Draw.IndexBuffer.Count;

            int neededStreamBufferSize = transforms.Count * totalIndices;
            if (neededStreamBufferSize > streamOutBufferBinding.Count) {
                streamOutBufferBinding.Buffer.Dispose(); // dispose the old buffer first
                var streamBuffer = Xenko.Graphics.Buffer.New<VertexPositionNormalColor>(
                    device,
                    neededStreamBufferSize,
                    BufferFlags.VertexBuffer | BufferFlags.StreamOutput,
                    GraphicsResourceUsage.Default
                );
                streamOutBufferBinding = new VertexBufferBinding(streamBuffer, VertexPositionNormalColor.Layout, streamBuffer.ElementCount);
            }

            int neededTransformBufferSize = transforms.Count;
            if (neededTransformBufferSize > transformBuffer.ElementCount) {
                transformBuffer.Dispose(); // dispose old buffer first
                var newTransformBuffer = Xenko.Graphics.Buffer.Structured.New<Matrix>(
                    device,
                    matrices.Items,
                    isUnorderedAccess: true
                );
                transformBuffer = newTransformBuffer;
            } else {
                transformBuffer.SetData(commandList, matrices.Items);
            }

        }

        private void PerformStreamOut() {

            var commandList = Game.GraphicsContext.CommandList;

            /* TODO: this currently assumes a single vertex buffer, is this always the case? */
            var vertexBuffer = renderedMesh.Draw.VertexBuffers[0].Buffer;
            var indexBuffer = renderedMesh.Draw.IndexBuffer.Buffer;

            commandList.SetPipelineState(pipelineState);

            /* TODO: this currently assumes a single vertex buffer, is this always the case? */
            commandList.SetVertexBuffer(0, vertexBuffer, renderedMesh.Draw.StartLocation, VertexPositionNormalTexture.Layout.VertexStride);
            commandList.SetIndexBuffer(indexBuffer, 0, renderedMesh.Draw.IndexBuffer.Is32Bit);
            commandList.SetStreamTargets(streamOutBufferBinding.Buffer);

            streamShader.Parameters.Set(MultiMeshShaderKeys.ModelTransforms, transformBuffer);
            streamShader.Apply(Game.GraphicsContext);

            /* finally write to our streamout buffer */
            commandList.DrawIndexedInstanced(indexBuffer.ElementCount, transforms.Count);
            commandList.SetStreamTargets(null);

        }

        static private Mesh CreateMesh(VertexBufferBinding bufferBinding) {

            var newMesh = new Mesh() {
                Draw = new MeshDraw() {
                    PrimitiveType = PrimitiveType.TriangleList,
                    VertexBuffers = new[] {
                        bufferBinding,
                    },
                    DrawCount = bufferBinding.Count
                }
            };

            return newMesh;

        }

        /*
        static private Material CreateVertexColourMaterial(GraphicsDevice device) {

            Material newVertexColorMaterial = Material.New(device, new MaterialDescriptor {
                Attributes = new MaterialAttributes {
                    Diffuse = new MaterialDiffuseMapFeature(new ComputeVertexStreamColor()),
                    DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                    CullMode = CullMode.None,
                },
            });

            return newVertexColorMaterial;

        }
        */

        public override void Start() {

            CreateDeviceObjects();

            streamBufferedMesh = CreateMesh(streamOutBufferBinding);
            streamBufferedMesh.MaterialIndex = 0;

            var newModel = new Model();
            newModel.Meshes.Add(streamBufferedMesh);
            newModel.Materials.Add(Material); // FIXME: can be null at this point if not set

            modelComponent = new ModelComponent() {
                Model = newModel
            };

            Entity.Add(modelComponent);

        }

        private void UpdateBuffers() {

            CheckBuffers();

            /* update our MeshDraw to reflect current state */
            var vertexCount = streamOutBufferBinding.Count;
            streamBufferedMesh.Draw.VertexBuffers[0] = streamOutBufferBinding;
            streamBufferedMesh.Draw.DrawCount = vertexCount;

        }

        private void UpdateMatrices() {

            /* FIXME: this is all somewhat stupid.. fix this once it at least runs */

            matrices.Clear();
            foreach (var transform in transforms) {
                matrices.Add(transform.Value.LocalMatrix);
            }

        }

        public override void Update() {

            UpdateMatrices();
            UpdateBuffers();
            PerformStreamOut();

        }

        public override void Cancel() {
            streamOutBufferBinding.Buffer.Dispose();
            transformBuffer.Dispose();
            streamShader.Dispose();
        }

    }

    class SomeObjectInSpace : SyncScript {

        public int Id;
        public Vector3 Velocity;
        public Vector3 RotVelocity;

        public override void Update() {

            var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;

            if (Entity.Transform.Position.X > 64.0f || Entity.Transform.Position.X < -64.0f) {
                Velocity.X = -Velocity.X;
            }

            if (Entity.Transform.Position.Y > 64.0f || Entity.Transform.Position.Y < -64.0f) {
                Velocity.Y = -Velocity.Y;
            }

            if (Entity.Transform.Position.Z > 64.0f || Entity.Transform.Position.Z < -64.0f) {
                Velocity.Z = -Velocity.Z;
            }

            Entity.Transform.Rotation *=
                Quaternion.RotationX(RotVelocity.X * dt) *
                Quaternion.RotationY(RotVelocity.Y * dt) *
                Quaternion.RotationZ(RotVelocity.Z * dt);

            Entity.Transform.Position += Velocity * dt;

        }

    }

    public class CombinedMeshes : SyncScript {

        /* GPU side data */
        private PoorMansMultiMesh multiMesh;

        [Display("Material")]
        public Material meshMaterial;

        public override void Start() {

            var ourPrimitive = GeometricPrimitive.Cube.New(GraphicsDevice, 1.0f);
            var primitiveMeshDraw = ourPrimitive.ToMeshDraw();

            var newMultiMesh = new PoorMansMultiMesh() {
                Mesh = new Mesh() {
                    Draw = primitiveMeshDraw
                },
                Material = meshMaterial
            };

            var numInstances = 4096;
            var random = new Random();
            for (int i = 0; i < numInstances; ++i) {

                var randX = random.Next(-64, 64);
                var randY = random.Next(-64, 64);
                var randZ = random.Next(-64, 64);

                var velX = random.NextDouble() * 4.0;
                var velY = random.NextDouble() * 4.0;
                var velZ = random.NextDouble() * 4.0;
                var ballVel = new Vector3((float)velX, (float)velY, (float)velZ);

                var rotVelX = random.NextDouble();
                var rotVelY = random.NextDouble();
                var rotVelZ = random.NextDouble();
                var ballRotVel = new Vector3((float)rotVelX, (float)rotVelY, (float)rotVelZ);

                var newEntity = new Entity(new Vector3(randX, randY, randZ));
                var newObjectInSpace = new SomeObjectInSpace() { Id = i, Velocity = ballVel, RotVelocity = ballRotVel };

                newEntity.Add(newObjectInSpace);
                newMultiMesh.AddInstance(newEntity.Transform);
                Entity.AddChild(newEntity);

            }

            Entity.Add(newMultiMesh);
            multiMesh = newMultiMesh;

        }

        public override void Update() {

        }

    }
}
