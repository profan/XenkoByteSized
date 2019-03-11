using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Rendering;
using Xenko.Graphics;

namespace XenkoByteSized.ProceduralMesh {
    public class CubeMesh : SyncScript {

        const int CUBE_VERTS = 24;
        const int CUBE_INDICES = 36;

        /* our vertex array, vertex buffer will be built from this data we keep around */
        private VertexPositionNormalTexture[] vertices;
        private uint[] indices;

        /* GPU side data */
        private ModelComponent modelComponent;
        private Mesh mesh;

        /* rotation in radians per second */
        public float rotationSpeed = MathUtil.PiOverFour;

        static private (VertexPositionNormalTexture[], uint[]) GenerateCube() {

            void MakeFace(uint[] inds, ref uint offset, uint face) {

                inds[offset++] = 0 + (4 * face);
                inds[offset++] = 1 + (4 * face);
                inds[offset++] = 2 + (4 * face);

                inds[offset++] = 2 + (4 * face);
                inds[offset++] = 3 + (4 * face);
                inds[offset++] = 0 + (4 * face);

            }

            /* create our vertex data first */
            var verts = new VertexPositionNormalTexture[CUBE_VERTS];

            /* top */
            verts[0].Position = new Vector3(1.0f, 1.0f, 1.0f);
            verts[1].Position = new Vector3(0.0f, 1.0f, 1.0f);
            verts[2].Position = new Vector3(0.0f, 1.0f, 0.0f);
            verts[3].Position = new Vector3(1.0f, 1.0f, 0.0f);

            /* bottom */
            verts[4].Position = new Vector3(0.0f, 0.0f, 0.0f);
            verts[5].Position = new Vector3(0.0f, 0.0f, 1.0f);
            verts[6].Position = new Vector3(1.0f, 0.0f, 1.0f);
            verts[7].Position = new Vector3(1.0f, 0.0f, 0.0f);

            /* left */
            verts[8].Position = new Vector3(0.0f, 1.0f, 1.0f);
            verts[9].Position = new Vector3(0.0f, 0.0f, 1.0f);
            verts[10].Position = new Vector3(0.0f, 0.0f, 0.0f);
            verts[11].Position = new Vector3(0.0f, 1.0f, 0.0f);

            /* right */
            verts[12].Position = new Vector3(1.0f, 0.0f, 0.0f);
            verts[13].Position = new Vector3(1.0f, 0.0f, 1.0f);
            verts[14].Position = new Vector3(1.0f, 1.0f, 1.0f);
            verts[15].Position = new Vector3(1.0f, 1.0f, 0.0f);

            /* front */
            verts[16].Position = new Vector3(1.0f, 0.0f, 1.0f);
            verts[17].Position = new Vector3(0.0f, 0.0f, 1.0f);
            verts[18].Position = new Vector3(0.0f, 1.0f, 1.0f);
            verts[19].Position = new Vector3(1.0f, 1.0f, 1.0f);

            /* back */
            verts[20].Position = new Vector3(0.0f, 0.0f, 0.0f);
            verts[21].Position = new Vector3(1.0f, 0.0f, 0.0f);
            verts[22].Position = new Vector3(1.0f, 1.0f, 0.0f);
            verts[23].Position = new Vector3(0.0f, 1.0f, 0.0f);

            var indices = new uint[CUBE_INDICES];
            uint curIndex = 0, curFace = 0;
            
            /* top */
            MakeFace(indices, ref curIndex, curFace++);

            /* bottom */
            MakeFace(indices, ref curIndex, curFace++);
            
            /* left */
            MakeFace(indices, ref curIndex, curFace++);

            /* right */
            MakeFace(indices, ref curIndex, curFace++);

            /* front */
            MakeFace(indices, ref curIndex, curFace++);

            /* back */
            MakeFace(indices, ref curIndex, curFace++);

            return (verts, indices);

        }

        static private void CalculateNormals(VertexPositionNormalTexture[] verts, uint[] indices) {

            for (int i = 0; i < indices.Length; i += 3) {
                var v1 = indices[i];
                var v2 = indices[i + 1];
                var v3 = indices[i + 2];
                var u = verts[v3].Position - verts[v1].Position;
                var v = verts[v2].Position - verts[v1].Position;
                var normal = Vector3.Cross(u, v);
                verts[v1].Normal = normal;
                verts[v2].Normal = normal;
                verts[v3].Normal = normal;
            }

        }

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

        private void UpdateMeshData() {
            
            var context = Game.GraphicsContext;

            /* currently assumes the size of the data does not change, only the contents */
            mesh.Draw.VertexBuffers[0].Buffer.SetData(context.CommandList, vertices);
            mesh.Draw.IndexBuffer.Buffer.SetData(context.CommandList, indices);

        }

        public override void Start() {

            /* set up our mesh */
            (vertices, indices) = GenerateCube();
            CalculateNormals(vertices, indices);
            mesh = CreateMesh(GraphicsDevice, vertices, indices);

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
