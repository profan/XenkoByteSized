using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Rendering;
using Xenko.Graphics;
using Xenko.Physics;
using Xenko.Core;
using Xenko.Physics.Shapes;
using Xenko.Core.Annotations;
using Xenko.Core.Diagnostics;
using Xenko.Extensions;
using Xenko.Profiling;
using Xenko.Input;

using System.Collections.Generic;
using System;

namespace XenkoByteSized.ProceduralMesh {
    class SubdividedPlaneMesh : SyncScript {

        static class StaticDebug {

            public static DebugTextSystem debug;

            const int MSG_HEIGHT = 16;
            const int MAX_MESSAGES = 32;
            static string[] messages = new string[MAX_MESSAGES];
            static Int2 initialOffset = new Int2(48, 48);
            static int curPos = 0;

            public static void Log(string msg) {
                curPos = (curPos + 1) % MAX_MESSAGES;
                messages[curPos] = msg;
            }

            public static void Draw() {

                if (debug == null) {
                    return;
                }

                var curOffset = initialOffset;
                foreach (var msg in messages) {
                    if (msg != null) {
                        debug.Print(msg, curOffset);
                        curOffset.Y += MSG_HEIGHT;
                    }
                }

            }

        }

        class TerrainModifier {

            public delegate void OnModify();
            public event OnModify OnModifyEvent;

            public enum ModificationMode {
                Raise = 0,
                Lower = 1,
                Smoothen = 2,
                Flatten = 3
            }

            public TerrainModifier(VertexPositionNormalTexture[] verts) {
                vertices = verts;
                Mode = ModificationMode.Raise;
                multiplier = 1.0f;
            }

            private VertexPositionNormalTexture[] vertices;

            public ModificationMode Mode { get; set; }
            public float multiplier = 1.0f;
            public float radius = 4.0f;

            private void AdjustHeight(ref Vector2 pos, float radius, float delta) {

                /* FIXME: more efficient later */
                var verts = VerticesNearPosition(vertices, pos, radius);

                for (int i = 0; i < verts.Length; ++i) {
                    ref var v = ref verts[i].Position;
                    var dist = (v.XZ() - pos).Length();
                    // ShittyDebug.Log($"dist: {dist}, v.XZ: {v.XZ()}, pos: {pos}");
                    float factor = (multiplier * (radius - dist));
                    if (factor >= 0.0f) {
                        v.Y += factor * delta;
                    }
                }

            }

            void Raise(ref Vector2 pos, float radius, float delta) {
                AdjustHeight(ref pos, radius, delta);
            }

            void Lower(ref Vector2 pos, float radius, float delta) {
                AdjustHeight(ref pos, radius, -delta);
            }

            void Smoothen(ref Vector2 pos, float radius, float delta) {

                /* FIXME: more efficient later */
                var verts = VerticesNearPosition(vertices, pos, radius);

                float totalHeights = 0.0f;
                for (int i = 0; i < verts.Length; ++i) {
                    ref var v = ref verts[i].Position;
                    var dist = (v.XZ() - pos).Length();
                    if (dist <= radius) {
                        totalHeights += v.Y;
                    }
                }


                /* now _approach_ the average height value, from the current height value */
                float avgHeight = totalHeights / verts.Length;

                for (int i = 0; i < verts.Length; ++i) {
                    ref var v = ref verts[i].Position;
                    var dist = (v.XZ() - pos).Length();
                    if (dist <= radius) {
                        v.Y += (avgHeight - v.Y) * (radius - dist) * delta;
                    }
                }

            }

            float Flatten(ref Vector2 pos, float radius, float delta) {

                /* FIXME: more efficient later */
                var verts = VerticesNearPosition(vertices, pos, radius);

                float closestVertVal = float.MaxValue;
                float closestVertHeight = 0.0f;

                for (int i = 0; i < verts.Length; ++i) {
                    ref var v = ref verts[i].Position;
                    float dist = (v.XZ() - pos).Length();
                    if (dist <= radius && dist <= closestVertVal) {
                        closestVertVal = dist;
                        closestVertHeight = v.Y;
                    }
                }

                for (int i = 0; i < verts.Length; ++i) {
                    ref var v = ref verts[i].Position;
                    var dist = (v.XZ() - pos).Length();
                    if (dist <= radius) {
                        v.Y = closestVertHeight;
                    }
                }

                return closestVertVal;

            }

            public void Modify(Vector2 pos, float radius, float delta) {

                switch (Mode) {
                    case ModificationMode.Raise:
                        Raise(ref pos, radius, delta);
                        break;
                    case ModificationMode.Lower:
                        Lower(ref pos, radius, delta);
                        break;
                    case ModificationMode.Smoothen:
                        Smoothen(ref pos, radius, delta);
                        break;
                    case ModificationMode.Flatten:
                        Flatten(ref pos, radius, delta);
                        break;
                }

                OnModifyEvent();

            }

        }

        const int DEFAULT_WIDTH = 64;
        const int DEFAULT_HEIGHT = 64;
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

        public CameraComponent CurrentCamera { get; set; }

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
                            float step = (1.0f / subdivisions);
                            float x1 = curX + ((float)sX / subdivisions) + step;
                            float z1 = curZ + ((float)sZ / subdivisions) + step;
                            float x2 = x1 + step;
                            float z2 = z1 + step;
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

            StaticDebug.Log($"curOffset: {curOffset}");
            StaticDebug.Log($"vertCount: {verts.Length}");

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

            var context = Game.GraphicsContext;

            /* currently assumes the size of the data does not change, only the contents */
            mesh.Draw.VertexBuffers[0].Buffer.SetData(context.CommandList, vertices);

        }

        static private Span<VertexPositionNormalTexture> VerticesNearPosition(VertexPositionNormalTexture[] vs, Vector2 pos, float radius) {

            /* for now, we do this with brute force, but we know our data layout, so it can be a *lot* more efficient */
            Span<VertexPositionNormalTexture> verts = vs;
            return verts;

        }

        public override void Update() {

            const float UNITS_PER_SECOND = 2.0f;

            var screenPos = Input.MousePosition;
            float dt = (float)Game.TargetElapsedTime.TotalSeconds;

            if (Input.IsMouseButtonDown(MouseButton.Left) && Input.IsKeyDown(Keys.LeftCtrl)) {

                var worldPosHit = Utils.ScreenPositionToWorldPositionRaycast(screenPos, CurrentCamera, this.GetSimulation());
                var hitPos = worldPosHit.Point;

                if (worldPosHit.Succeeded) {
                    var localHitPos = Entity.Transform.WorldToLocal(hitPos);
                    modifier.Mode = TerrainModifier.ModificationMode.Flatten;
                    modifier.Modify(localHitPos.XZ(), modifier.radius, UNITS_PER_SECOND * dt);
                }

            } else if (Input.IsMouseButtonDown(MouseButton.Left) && Input.IsKeyDown(Keys.LeftShift)) {

                var worldPosHit = Utils.ScreenPositionToWorldPositionRaycast(screenPos, CurrentCamera, this.GetSimulation());
                var hitPos = worldPosHit.Point;

                if (worldPosHit.Succeeded) {
                    var localHitPos = Entity.Transform.WorldToLocal(hitPos);
                    modifier.Mode = TerrainModifier.ModificationMode.Smoothen;
                    modifier.Modify(localHitPos.XZ(), modifier.radius, UNITS_PER_SECOND * dt);
                }

            } else if (Input.IsMouseButtonDown(MouseButton.Left)) {

                var worldPosHit = Utils.ScreenPositionToWorldPositionRaycast(screenPos, CurrentCamera, this.GetSimulation());
                var hitPos = worldPosHit.Point;

                if (worldPosHit.Succeeded) {
                    var localHitPos = Entity.Transform.WorldToLocal(hitPos);
                    modifier.Mode = TerrainModifier.ModificationMode.Raise;
                    modifier.Modify(localHitPos.XZ(), modifier.radius, UNITS_PER_SECOND * dt);
                }

                // ShittyDebug.Log($"hitPos, result: {worldPosHit.Succeeded}, x: {hitPos.X}, y: {hitPos.Y}, z: {hitPos.Z}");

            } else if (Input.IsMouseButtonDown(MouseButton.Middle)) {

                var worldPosHit = Utils.ScreenPositionToWorldPositionRaycast(screenPos, CurrentCamera, this.GetSimulation());
                var hitPos = worldPosHit.Point;

                if (worldPosHit.Succeeded) {
                    var localHitPos = Entity.Transform.WorldToLocal(hitPos);
                    modifier.Mode = TerrainModifier.ModificationMode.Lower;
                    modifier.Modify(localHitPos.XZ(), modifier.radius, UNITS_PER_SECOND * dt);
                }

                // ShittyDebug.Log($"hitPos, result: {worldPosHit.Succeeded}, x: {hitPos.X}, y: {hitPos.Y}, z: {hitPos.Z}");

            }

            StaticDebug.Draw();

        }

        public override void Start() {

            /* init our bad debug helper */
            StaticDebug.debug = DebugText;

            /* create our collision component, must be created first */
            colliderComponent = new StaticColliderComponent();

            /* create plane collider shape, meant to catch our raycasts so we can manipulate terrain (for now) */
            StaticPlaneColliderShape planeShape = new StaticPlaneColliderShape(new Vector3(0.0f, 1.0f, 0.0f), 0.0f);
            colliderComponent.ColliderShapes.Add(new StaticPlaneColliderShapeDesc() { Normal = new Vector3(0.0f, 1.0f, 0.0f) });
            colliderComponent.ColliderShape = planeShape;
            Entity.Add(colliderComponent);

            /* set up our heightmap and plane */

            /* temporarily uncommented
            heightmap = new UnmanagedArray<float>(DEFAULT_WIDTH * DEFAULT_HEIGHT);
            for (int i = 0; i < heightmap.Length; ++i) {
                heightmap[i] = 0.0f;
            }

            var heightfield = new HeightfieldColliderShape(
                DEFAULT_WIDTH*2, DEFAULT_HEIGHT*2, heightmap,
                heightScale: 1.0f,
                minHeight: -64.0f,
                maxHeight: 64.0f,
                flipQuadEdges: false
            );
            */

            /* HACK: necessary because of.. reasons? (without this it will complain of having no collider shape) */
            // colliderComponent.ColliderShapes.Add(new BoxColliderShapeDesc());

            /* this is here because the collider component is what ends up loading the bullet physics dll,
             * if we create the collider shape before the component, bullet will not have loaded yet. */
            // colliderComponent.ColliderShape = heightfield;

            // Entity.Add(colliderComponent);

            /* set up our mesh */
            vertices = GenerateSubdividedPlaneMesh(
                DEFAULT_WIDTH, DEFAULT_HEIGHT,
                DEFAULT_SUBDIVISIONS
            );
            CalculateNormals(vertices);
            mesh = CreateMesh(vertices);

            /* set up our terrain modifier */
            modifier = new TerrainModifier(vertices);
            modifier.OnModifyEvent += () => UpdateMeshData();
            modifier.OnModifyEvent += () => CalculateNormals(vertices);

            /* push the created mesh and its data */
            UpdateMeshData();

            /* create our ModelComponent and add the mesh to it */
            var boundingBox = Utils.FromPoints(vertices);
            var boundingSphere = BoundingSphere.FromBox(boundingBox);

            modelComponent = new ModelComponent() {
                Model = new Model() {
                    BoundingBox = Utils.FromPoints(vertices),
                    BoundingSphere = BoundingSphere.FromBox(boundingBox)
                }
            };

            modelComponent.Model.Add(mesh);
            Entity.Add(modelComponent);

        }

    }
}
