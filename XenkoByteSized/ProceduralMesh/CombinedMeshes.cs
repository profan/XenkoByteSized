using Xenko.Core.Mathematics;
using Xenko.Core.Collections;
using Xenko.Engine;
using Xenko.Rendering;
using Xenko.Graphics;

namespace XenkoByteSized.ProceduralMesh {

    class PoorMansMultiMesh : SyncScript {

        private TrackingDictionary<System.Guid, TransformComponent> transforms = new TrackingDictionary<System.Guid, TransformComponent>();
        
        /* gpu side data */
        private ModelComponent modelComponent;
        private VertexBufferBinding vertexBinding;
        private IndexBufferBinding indexBinding;

        public Material[] Materials;
        public Mesh Mesh;

        System.Guid AddInstance(TransformComponent transform) {
            transforms[transform.Id] = transform;
            return transform.Id;
        }

        bool RemoveInstance(TransformComponent transform) {
            return transforms.Remove(transform.Id);
        }

        void CheckBuffers() {

            var device = GraphicsDevice;
            var layout = Mesh.Draw.VertexBuffers[0].Declaration;

            var vbo = Mesh.Draw.VertexBuffers[0].Buffer;
            var vboInBytes = vbo.SizeInBytes;

            var ibo = Mesh.Draw.IndexBuffer.Buffer;
            var iboInBytes = ibo.SizeInBytes;

            uint neededVboSize = (uint)(transforms.Count * vboInBytes);
            uint neededIboSize = (uint)(iboInBytes);

            if (neededVboSize > vertexBinding.Buffer.SizeInBytes) {
                var vertexBuffer = Buffer.Vertex.New(device, (int)(neededVboSize * 1.5f));
                vertexBinding = new VertexBufferBinding(vertexBuffer, layout, 0);
            }

            if (neededIboSize > indexBinding.Buffer.SizeInBytes) {
                var is32Bits = false;
                var indexBuffer = Buffer.Index.New(device, (int)(neededIboSize * 1.5f));
                indexBinding = new IndexBufferBinding(indexBuffer, is32Bits, 0);
            }

        }

        void UpdateBuffers() {

            for (int i = 0; i < transforms.Count; ++i) {

            }

        }

        public override void Start() {

        }

        public override void Update() {

        }

        public override void Cancel() {

        }

    }

    public class CombinedMeshes : SyncScript {

        /* GPU side data */
        private ModelComponent modelComponent;
        private Mesh mesh;

        /* rotation in radians per second */
        public float rotationSpeed = MathUtil.PiOverFour;

        static private Mesh CreateMesh(GraphicsDevice device, VertexPositionNormalTexture[] verts, uint[] indices) {

            /* now set up the GPU side stuff */
            var vbo = Xenko.Graphics.Buffer.Vertex.New(
                device,
                verts, /* allocated size of buffer inferred from the stored datatype and the length of the array */
                GraphicsResourceUsage.Default /* usage hint to the GPU for it to allocate it appropriately (explicit default in our case) */
            );

            /* NOTE: if resource usage here  is set to immutable (the default) you will encounter an error if you try to update it after creating it */
            var ibo = Xenko.Graphics.Buffer.Index.New(device, indices, GraphicsResourceUsage.Default);

            var newMesh = new Mesh() {
                Draw = new MeshDraw() {
                    PrimitiveType = PrimitiveType.TriangleList,
                    VertexBuffers = new[] {
                        new VertexBufferBinding(vbo, VertexPositionNormalTexture.Layout, verts.Length),
                    },
                    IndexBuffer = new IndexBufferBinding(ibo, is32Bit: true, count: indices.Length),
                    DrawCount = indices.Length
                }
            };

            return newMesh;

        }

        public override void Start() {

            var vertices = new VertexPositionNormalTexture[0];
            var indices = new uint[0];

            mesh = CreateMesh(GraphicsDevice, vertices, indices);

            /* create our ModelComponent and add the mesh to it */
            modelComponent = new ModelComponent() {
                Model = new Model() {
                    mesh,
                }
            };

            Entity.Add(modelComponent);

        }

        public override void Update() {

            var deltaTime = (float)Game.UpdateTime.Elapsed.TotalSeconds;
            var rotation = Quaternion.RotationY(rotationSpeed * deltaTime);
            Entity.Transform.Rotation *= rotation;

        }

        // FIXME: can this be done differently? its not a hack but..
        public override void Cancel() {
            mesh.Draw.VertexBuffers[0].Buffer.Dispose();
            mesh.Draw.IndexBuffer.Buffer.Dispose();
        }

    }
}
