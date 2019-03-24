using Xenko.Core.Mathematics;
using Xenko.Core.Collections;
using Xenko.Engine;
using Xenko.Rendering;
using Xenko.Graphics;
using Xenko.Graphics.GeometricPrimitives;
using Xenko.Extensions;
using System.Runtime.InteropServices;

namespace XenkoByteSized.ProceduralMesh {

    class PoorMansMultiMesh : SyncScript {

        [StructLayout(LayoutKind.Sequential, Pack=0)]
        internal struct TransformData {

            Matrix mat;

            public TransformData(Matrix localMatrix) {
                mat = localMatrix;
            }

        };

        const int INITIAL_INSTANCE_COUNT = 16;

        private TrackingDictionary<System.Guid, TransformComponent> transforms = new TrackingDictionary<System.Guid, TransformComponent>();
        private FastList<TransformData> matrices = new FastList<TransformData>(INITIAL_INSTANCE_COUNT);

        /* gpu pipeline stuff */
        private PipelineState pipelineState;

        /* gpu side data, our ModelComponent we use to render, no index buffer data so there is some... waste yes */
        private ModelComponent modelComponent;
        private VertexBufferBinding streamOutBufferBinding;
        private Buffer<TransformData> transformBuffer;
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
            streamShader = shader;

            var pipeline = new PipelineStateDescription() {
                // BlendState = BlendStates.Default,
                // RasterizerState = RasterizerStateDescription.Default,
                // DepthStencilState = DepthStencilStates.None,
                // Output = new RenderOutputDescription(GraphicsDevice.Presenter.BackBuffer.Format),
                PrimitiveType = PrimitiveType.TriangleList,
                InputElements = VertexPositionNormalTexture.Layout.CreateInputElements(),
                EffectBytecode = shader.Effect.Bytecode,
                RootSignature = shader.RootSignature,
            };

            var newPipelineState = PipelineState.New(GraphicsDevice, ref pipeline);
            pipelineState = newPipelineState;

            var streamBuffer = Buffer.New<VertexPositionNormalTexture>(GraphicsDevice, 0, BufferFlags.StreamOutput);
            streamOutBufferBinding = new VertexBufferBinding(streamBuffer, VertexPositionNormalTexture.Layout, streamBuffer.ElementCount);

            var newTransformBuffer = Buffer.New<TransformData>(GraphicsDevice, INITIAL_INSTANCE_COUNT, BufferFlags.StructuredBuffer);
            transformBuffer = newTransformBuffer;

        }

        private void CheckBuffers() {

            var device = GraphicsDevice;
            var commandList = Game.GraphicsContext.CommandList;

            var totalIndices = renderedMesh.Draw.IndexBuffer.Count;

            uint neededStreamBufferSize = (uint)(transforms.Count * totalIndices);
            if (neededStreamBufferSize > streamOutBufferBinding.Count) {
                streamOutBufferBinding.Buffer.Dispose(); // dispose the old buffer first
                var streamBuffer = Buffer.New<VertexPositionNormalTexture>(device, (int)(neededStreamBufferSize), BufferFlags.StreamOutput);
                streamOutBufferBinding = new VertexBufferBinding(streamBuffer, VertexPositionNormalTexture.Layout, streamBuffer.ElementCount);
            }

            uint neededTransformBufferSize = (uint)(transforms.Count);
            if (neededTransformBufferSize > transformBuffer.ElementCount) {
                transformBuffer.RecreateWith(matrices.Items);
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
            commandList.SetVertexBuffer(0, vertexBuffer, 0, VertexPositionNormalTexture.Layout.VertexStride);
            commandList.SetIndexBuffer(indexBuffer, 0, is32bits: true);
            commandList.SetStreamTargets(streamOutBufferBinding.Buffer);
            
            streamShader.Parameters.Set(MultiMeshShaderKeys.modelTransforms, streamOutBufferBinding.Buffer);
            streamShader.UpdateEffect(GraphicsDevice);

            /* finally write to our streamout buffer */
            commandList.DrawIndexedInstanced(vertexBuffer.ElementCount, transforms.Count);

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

        public override void Start() {

            CreateDeviceObjects();

            streamBufferedMesh = CreateMesh(streamOutBufferBinding);

            modelComponent = new ModelComponent() {
                Model = new Model() {
                    streamBufferedMesh
                }
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
                matrices.Add(new TransformData(transform.Value.LocalMatrix));
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

        public override void Update() {
            var worldPos = Entity.Transform.WorldMatrix.TranslationVector;
            DebugText.Print($"{Id} - Position - x: {worldPos.X}, y: {worldPos.Y}", new Int2(16, 16 * Id));
        }

    }

    public class CombinedMeshes : SyncScript {

        /* GPU side data */
        private PoorMansMultiMesh multiMesh;

        /* rotation in radians per second */
        public float rotationSpeed = MathUtil.PiOverFour;

        public override void Start() {

            var spherePrimitive = GeometricPrimitive.Sphere.New(GraphicsDevice, 0.5f);
            var sphereMeshDraw = spherePrimitive.ToMeshDraw();

            var newMultiMesh = new PoorMansMultiMesh() {
                Mesh = new Mesh() {
                    Draw = sphereMeshDraw
                }
            };

            var numInstances = 5;
            for (int i = 0; i < numInstances; ++i) {
                var newObjectInSpace = new SomeObjectInSpace() { Id = i };
                newMultiMesh.AddInstance(Entity.Transform);
                Entity.Add(newObjectInSpace);
            }

            Entity.Add(newMultiMesh);
            multiMesh = newMultiMesh;

        }

        public override void Update() {

            var deltaTime = (float)Game.UpdateTime.Elapsed.TotalSeconds;
            var rotation = Quaternion.RotationY(rotationSpeed * deltaTime);
            Entity.Transform.Rotation *= rotation;

        }

    }
}
