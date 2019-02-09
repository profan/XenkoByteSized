using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Rendering;
using Xenko.Graphics;
using Xenko.Physics;
using Xenko.Core;
using Xenko.Physics.Shapes;
using Xenko.Core.Annotations;

using System.Collections.Generic;
using System.Linq;
using Xenko.Input;

namespace XenkoByteSized.ProceduralMesh {
    class SubdividedPlaneMesh : SyncScript {

        class TerrainModifier {

            public enum ModificationMode {
                Raise = 0,
                Lower = 1,
                Smoothen = 2,
                Flatten = 3
            }

            public TerrainModifier(UnmanagedArray<float> data) {
                mode = ModificationMode.Raise;
                multiplier = 1.0f;
            }

            public ModificationMode mode;
            public float multiplier;

            void Raise(ref Vector2 pos, float radius) {

            }

            void Lower(ref Vector2 pos, float radius) {

            }

            void Smoothen(ref Vector2 pos, float radius) {

            }

            void Flatten(ref Vector2 pos, float radius) {

            }

            public void Modify(ref Vector2 pos, float radius) {

                switch (mode) {
                    case ModificationMode.Raise:
                        Raise(ref pos, radius);
                        break;
                    case ModificationMode.Lower:
                        Lower(ref pos, radius);
                        break;
                    case ModificationMode.Smoothen:
                        Smoothen(ref pos, radius);
                        break;
                    case ModificationMode.Flatten:
                        Flatten(ref pos, radius);
                        break;
                }

            }

        }

        const int DEFAULT_WIDTH = 16;
        const int DEFAULT_HEIGHT = 16;
        const int DEFAULT_SUBDIVISIONS = 2;

        /* plane mesh data */
        private VertexPositionNormalTexture[] vertices;
        private ModelComponent modelComponent;
        private Mesh mesh;

        /* plane collision and heightmap data */
        private StaticColliderComponent colliderComponent;
        private UnmanagedArray<float> heightmap;

        /* terrain modification thing */
        private TerrainModifier modifier;

        /* current camera */
        private CameraComponent currentCamera;

        static private VertexPositionNormalTexture[] GenerateSubdividedPlaneMesh(int width, int height, int subdivisions) {

            void CreateQuad(float x1, float z1, float x2, float z2, VertexPositionNormalTexture[] vs, ref int offset) {

                vs[offset++].Position = new Vector3(x2, 0.0f, z1);
                vs[offset++].Position = new Vector3(x1, 0.0f, z2);
                vs[offset++].Position = new Vector3(x1, 0.0f, z1);

                vs[offset++].Position = new Vector3(x1, 0.0f, z2);
                vs[offset++].Position = new Vector3(x2, 0.0f, z1);
                vs[offset++].Position = new Vector3(x2, 0.0f, z2);

            }

            int vertsPerRow = 6 + (6 * subdivisions * subdivisions);
            int vertCount = (width * height * 6 * (subdivisions * subdivisions + 1));
            var verts = new VertexPositionNormalTexture[vertCount];

            int curX = 0, curZ = 0, curOffset = 0;
            for (int i = 0; i < verts.Length; i += vertsPerRow) {

                /* no index buffers for now to keep it.. uh, simple, but wasteful */

                if (subdivisions == 0) {

                    CreateQuad(curX, curZ, curX + 1, curZ + 1, verts, ref curOffset);

                } else {

                    for (int sX = 0; sX < subdivisions; ++sX) {
                        for (int sZ = 0; sZ < subdivisions; ++sZ) {
                            float x1 = curX + ((float)sX / subdivisions);
                            float z1 = curZ + ((float)sZ / subdivisions);
                            float x2 = x1 + (1.0f / subdivisions) * sX + 1;
                            float z2 = z1 + (1.0f / subdivisions) * sZ + 1;
                            CreateQuad(x1, z1, x2, z2, verts, ref curOffset);
                        }
                    }

                }

                if (curX == (width - 1)) {
                    curX = 0;
                    curZ += 1;
                } else {
                    curX += 1;
                }

            }

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
                GraphicsResourceUsage.Dynamic /* usage hint to the GPU for it to allocate it appropriately */
            );

            var newMesh = new Mesh() {
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

        /* from the xenko docs: https://doc.xenko.com/latest/en/manual/physics/raycasting.html */
        public static HitResult ScreenPositionToWorldPositionRaycast(Vector2 screenPos, CameraComponent camera, Simulation simulation) {

            Matrix invViewProj = Matrix.Invert(camera.ViewProjectionMatrix);

            // Reconstruct the projection-space position in the (-1, +1) range.
            //    Don't forget that Y is down in screen coordinates, but up in projection space
            Vector3 sPos;
            sPos.X = screenPos.X * 2f - 1f;
            sPos.Y = 1f - screenPos.Y * 2f;

            // Compute the near (start) point for the raycast
            // It's assumed to have the same projection space (x,y) coordinates and z = 0 (lying on the near plane)
            // We need to unproject it to world space
            sPos.Z = 0f;
            var vectorNear = Vector3.Transform(sPos, invViewProj);
            vectorNear /= vectorNear.W;

            // Compute the far (end) point for the raycast
            // It's assumed to have the same projection space (x,y) coordinates and z = 1 (lying on the far plane)
            // We need to unproject it to world space
            sPos.Z = 1f;
            var vectorFar = Vector3.Transform(sPos, invViewProj);
            vectorFar /= vectorFar.W;

            // Raycast from the point on the near plane to the point on the far plane and get the collision result
            var result = simulation.Raycast(vectorNear.XYZ(), vectorFar.XYZ());
            return result;

        }

        static private BoundingBox FromPoints(VertexPositionNormalTexture[] verts) {

            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            for (int i = 0; i < verts.Length; ++i) {
                Vector3.Min(ref min, ref verts[i].Position, out min);
                Vector3.Max(ref max, ref verts[i].Position, out max);
            }

            return new BoundingBox(min, max);

        }

        float debugTime = 0.0f;
        HitResult lastHitResult;
        Vector3 lastHitPos;

        public override void Update() {

            var screenPos = Input.MousePosition;
            float dt = (float)Game.TargetElapsedTime.TotalSeconds;
            debugTime -= dt;
            
            if (debugTime >= 0.0f) {
                DebugText.Print(
                    $"lastHitPos, result: {lastHitResult.Succeeded}, x: {lastHitPos.X}, y: {lastHitPos.Y}, z: {lastHitPos.Z}",
                    new Int2(64, 64)
                );
            }

            if (Input.IsMouseButtonPressed(MouseButton.Left)) {

                var worldPosHit = ScreenPositionToWorldPositionRaycast(screenPos, currentCamera, this.GetSimulation());
                var hitPos = worldPosHit.Point;

                lastHitResult = worldPosHit;
                lastHitPos = lastHitResult.Point;

                debugTime = 1.0f;

            } else if (Input.IsMouseButtonReleased(MouseButton.Right)) {

                var worldPosHit = ScreenPositionToWorldPositionRaycast(screenPos, currentCamera, this.GetSimulation());
                var hitPos = worldPosHit.Point;

                lastHitResult = worldPosHit;
                lastHitPos = lastHitResult.Point;

                debugTime = 1.0f;

            }

        }

        public override void Start() {

            /* get our current camera, we know it doesnt change so this is fine */
            currentCamera = SceneSystem.SceneInstance.RootScene.Entities.First(e => e.Name == "Camera").Get<CameraComponent>();

            /* set up our heightmap and plane */
            heightmap = new UnmanagedArray<float>(DEFAULT_WIDTH * DEFAULT_HEIGHT);
            for (int i = 0; i < heightmap.Length; ++i) {
                heightmap[i] = 0.0f;
            }

            /* create our collision component, must be created first */
            colliderComponent = new StaticColliderComponent();

            var heightfield = new HeightfieldColliderShape(
                DEFAULT_WIDTH, DEFAULT_HEIGHT, heightmap,
                heightScale: 1.0f,
                minHeight: -64.0f,
                maxHeight: 64.0f,
                flipQuadEdges: false
            );

            /* HACK: necessary because of.. reasons? (without this it will complain of having no collider shape) */
            colliderComponent.ColliderShapes.Add(new BoxColliderShapeDesc());

            /* this is here because the collider component is what ends up loading the bullet physics dll,
             * if we create the collider shape before the component, bullet will not have loaded yet. */
            colliderComponent.ColliderShape = heightfield;

            Entity.Add(colliderComponent);

            /* set up our terrain modifier */
            modifier = new TerrainModifier(heightmap);

            /* set up our mesh */
            vertices = GenerateSubdividedPlaneMesh(
                DEFAULT_WIDTH, DEFAULT_HEIGHT,
                DEFAULT_SUBDIVISIONS
            );
            CalculateNormals(vertices);
            mesh = CreateMesh(vertices);

            /* push the created mesh and its data */
            UpdateMeshData();

            /* create our ModelComponent and add the mesh to it */
            var boundingBox = FromPoints(vertices);
            var boundingSphere = BoundingSphere.FromBox(boundingBox);

            modelComponent = new ModelComponent() {
                Model = new Model() {
                    BoundingBox = FromPoints(vertices),
                    BoundingSphere = BoundingSphere.FromBox(boundingBox)
                }
            };

            modelComponent.Model.Add(mesh);
            Entity.Add(modelComponent);

        }

    }
}
