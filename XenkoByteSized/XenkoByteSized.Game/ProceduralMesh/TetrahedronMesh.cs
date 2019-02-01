using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xenko.Core.Mathematics;
using Xenko.Input;
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

        private ModelComponent modelComponent;

        /* our vertex array, vertex buffer will be built from this data we keep around */
        private VertexPositionNormalTexture[] vertices;

        /* GPU side data */
        private Mesh mesh;

        /* rotation in radians per second */
        public float rotationSpeed = MathUtil.PiOverFour;

        private void GenerateTetrahedra() {

            /* create our vertex data first */
            vertices = new VertexPositionNormalTexture[TETRAHEDRON_VERTS];

            /* bottom face */
            vertices[0].Position = VERT_LEFT;
            vertices[1].Position = VERT_FRONT;
            vertices[2].Position = VERT_RIGHT;

            /* left face */
            vertices[3].Position = VERT_TOP;
            vertices[4].Position = VERT_FRONT;
            vertices[5].Position = VERT_LEFT;

            /* right face */
            vertices[6].Position = VERT_RIGHT;
            vertices[7].Position = VERT_FRONT;
            vertices[8].Position = VERT_TOP;

            /* back face */
            vertices[9].Position = VERT_LEFT;
            vertices[10].Position = VERT_RIGHT;
            vertices[11].Position = VERT_TOP;

        }

        private void CalculateNormals() {

            for (int i = 0; i < vertices.Length; i += 3) {
                var u = vertices[i + 2].Position - vertices[i].Position;
                var v = vertices[i + 1].Position - vertices[i].Position;
                var normal = Vector3.Cross(u, v);
                vertices[i + 0].Normal = normal;
                vertices[i + 1].Normal = normal;
                vertices[i + 2].Normal = normal;
            }

        }

        private void CreateMesh() {

            /* set up our mesh */
            GenerateTetrahedra();
            CalculateNormals();

            /* now set up the GPU side stuff */
            var vbo = Xenko.Graphics.Buffer.New<VertexPositionNormalTexture>(
                GraphicsDevice,
                vertices.Length, /* how many vertices to allocate space for */
                BufferFlags.VertexBuffer, /* what kind of buffer... */
                GraphicsResourceUsage.Dynamic /* usage hint to the GPU for it to allocate it appropriately */
            );

            var vertexBuffer = new VertexBufferBinding(vbo, VertexPositionNormalTexture.Layout, vertices.Length);

            mesh = new Mesh() {
                Draw = new MeshDraw() {
                    PrimitiveType = PrimitiveType.TriangleList,
                    VertexBuffers = new[] { vertexBuffer },
                    DrawCount = vertices.Length
                }
            };

            modelComponent = new ModelComponent() {
                Model = new Model() {
                    mesh,
                }
            };

            UpdateMeshData(); /* finally push the data */
            Entity.Add(modelComponent);

        }

        private void UpdateMeshData() {
            var context = Services.GetService<GraphicsContext>();
            mesh.Draw.VertexBuffers[0].Buffer.SetData(context.CommandList, vertices);
        }

        public override void Start() {
            CreateMesh();
        }

        public override void Update() {

            var deltaTime = (float)Game.UpdateTime.Elapsed.TotalSeconds;
            var rotation = Quaternion.RotationY(rotationSpeed * deltaTime) * Quaternion.RotationZ(rotationSpeed * deltaTime);
            Entity.Transform.Rotation *= rotation;

        }

    }
}
