XenkoByteSized
---------------
Intended as a sort of dumping ground for various small samples of using Xenko to do various things, like procedurally generate meshes.

Project is currently on **Xenko 3.1.0.1-beta02**.

## [XenkoByteSized.ProceduralMesh.TetrahedronMesh](XenkoByteSized/XenkoByteSized.Game/ProceduralMesh/TetrahedronMesh.cs)
A simple example of creating a mesh procedurally by supplying vertices, also calculates normals automatically. Can be observed in the scene that loads when you open the project aside from the source itself.

Uses only a vertex buffer to be as simple as possible.

![tetrahedra](bytesized.png "sphere and tetrahedra")

## [XenkoByteSized.ProceduralMesh.SubdividedPlaneMesh](XenkoByteSized/XenkoByteSized.Game/ProceduralMesh/SubdividedPlaneMesh.cs)

A somewhat less simple example of expanding upon the above, generates a subdivided plane with a configurable width, height and number of subdivisions in each quadrant.

**Does not yet have proper collision.** (Heightfield collider pending)

Still does not use any index buffer, probably should.

Has some basic operations possible like:
 * Raising/Lowering terrain (Left/Middle Mouse)
 * Smoothing terrain (Shift + Left Mouse)
 * Leveling terrain (Ctrl + Left Mouse)

Can also be observed in the same scene

![terrain](terrainy.png "some sculpted terrain thing")
