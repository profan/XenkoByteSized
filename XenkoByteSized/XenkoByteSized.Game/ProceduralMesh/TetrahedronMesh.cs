using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Rendering;
using Xenko.Graphics;

namespace XenkoByteSized.ProceduralMesh {
    public class TetrahedronMesh : SyncScript {

        const int TETRAHEDRON_VERTS = 3 * 4;
        static Vector3 VERT_TOP = new Vector3(1, 1, 1);
        static Vector3 VERT_LEFT = new Vector3(-1, -1, 1);
        static Vector3 VERT_RIGHT = new Vector3(-1, 1, -1);
        static Vector3 VERT_FRONT = new Vector3(1, -1, -1);

        /* our vertex array, vertex buffer will be built from this data we keep around */
        private VertexPositionNormalTexture[] vertices;

        /* GPU side data */
        private ModelComponent modelComponent;
        private Mesh mesh;

        /* rotation in radians per second */
        public float rotationSpeed = MathUtil.PiOverFour;

        static private VertexPositionNormalTexture[] GenerateTetrahedra() {

            /* create our vertex data first */
            var verts = new VertexPositionNormalTexture[TETRAHEDRON_VERTS];

            /* bottom face */
            verts[0].Position = VERT_LEFT;
            verts[1].Position = VERT_FRONT;
            verts[2].Position = VERT_RIGHT;

            /* left face */
            verts[3].Position = VERT_TOP;
            verts[4].Position = VERT_FRONT;
            verts[5].Position = VERT_LEFT;

            /* right face */
            verts[6].Position = VERT_RIGHT;
            verts[7].Position = VERT_FRONT;
            verts[8].Position = VERT_TOP;

            /* back face */
            verts[9].Position = VERT_LEFT;
            verts[10].Position = VERT_RIGHT;
            verts[11].Position = VERT_TOP;

            return verts;

        }

        static private void CalculateNormals(VertexPositionNormalTexture[] verts) {

            for (int i = 0; i < verts.Length; i += 3) {
                var u = verts[i + 2].Position - verts[i].Position;
                var v = verts[i + 1].Position - verts[i].Position;
                var normal = Vector3.Cross(u, v);
                verts[i + 0].Normal = normal;
                verts[i + 1].Normal = normal;
                verts[i + 2].Normal = normal;
            }

        }

        private Mesh CreateMesh(VertexPositionNormalTexture[] verts) {

            /* now set up the GPU side stuff */
            var vbo = Xenko.Graphics.Buffer.New<VertexPositionNormalTexture>(
                GraphicsDevice,
                verts.Length, /* how many vertices to allocate space for */
                BufferFlags.VertexBuffer, /* what kind of buffer... */
                GraphicsResourceUsage.Default /* usage hint to the GPU for it to allocate it appropriately (explicit default in our case) */
            );

            Mesh newMesh = new Mesh() {
                Draw = new MeshDraw() {
                    PrimitiveType = PrimitiveType.TriangleList,
                    VertexBuffers = new[] {
                        new VertexBufferBinding(vbo, VertexPositionNormalTexture.Layout, verts.Length)
                    },
                    DrawCount = verts.Length
                }
            };

            return newMesh;

        }

        private void UpdateMeshData() {

            var context = Services.GetService<GraphicsContext>();

            /* currently assumes the size of the data does not change, only the contents */
            mesh.Draw.VertexBuffers[0].Buffer.SetData(context.CommandList, vertices);

        }

        public override void Start() {

            /* set up our mesh */
            vertices = GenerateTetrahedra();
            CalculateNormals(vertices);
            mesh = CreateMesh(vertices);

            /* push the created mesh and its data */
            UpdateMeshData();

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
            var rotation = Quaternion.RotationY(rotationSpeed * deltaTime) * Quaternion.RotationZ(rotationSpeed * deltaTime);
            Entity.Transform.Rotation *= rotation;

        }

    }
}
