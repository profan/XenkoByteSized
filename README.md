XenkoByteSized
---------------
Intended as a sort of dumping ground for various small samples of using Xenko to do various things, like procedurally generate meshes.

Project is currently on **Xenko 3.1.0.1-beta01-0441**.

## [XenkoByteSized.ProceduralMesh.TetrahedronMesh](XenkoByteSized/ProceduralMesh/TetrahedronMesh.cs)
A simple example of creating a mesh procedurally by supplying vertices, also calculates normals automatically. Can be observed in the scene that loads when you open the project aside from the source itself.

Uses only a vertex buffer to be as simple as possible.

![tetrahedra](bytesized.png "sphere and tetrahedra")

## [XenkoByteSized.ProceduralMesh.SubdividedPlaneMesh](XenkoByteSized/ProceduralMesh/SubdividedPlaneMesh.cs)

A somewhat less simple example of expanding upon the above, generates a subdivided plane with a configurable width, height and number of subdivisions in each quadrant.

**Does not yet have proper collision.** (Heightfield collider pending)

Still does not use any index buffer, probably should.

Has some basic operations possible like:
 * Raising/Lowering terrain (Left/Middle Mouse)
 * Smoothing terrain (Shift + Left Mouse)
 * Leveling terrain (Ctrl + Left Mouse)

Can also be observed in the same scene

![terrain](terrainy.png "some sculpted terrain thing")

## **XenkoByteSized.SplitScreen**
This one is slightly harder to simply link to some code to illustrate, you'll want to explore the following to see whats going on: 
* **Scenes/SplitScreen/SplitScreenScene**
* **Scenes/SplitScreen/SplitScreenCompositor**
* **Scenes/SplitScreen/LeftTexture**
* **Scenes/SplitScreen/RightTexture**

The scene itself:
![splitscreen](splitscreen.png "the scene as can be seen in the sample")

A relevant piece of the compositor setup, where the default **CameraRenderer** at the root has been replaced by a **SceneRenderCollection**, as can be seen in [this page on the Xenko Docs about render targets](https://doc.xenko.com/latest/en/manual/graphics/graphics-compositor/render-textures.html).

![compositor](compositor_setup.png "a relevant piece of the compositor")

### Misc Considerations
Of the most important bits to consider here are:
* The main renderer only renders the sprites for each of the render target textures, (Group 31), while the render targets render everything **except** Group 31 (can be observed by looking at the **RenderMask** in the GraphicsCompositor for each renderer).
* I created a [special script](XenkoByteSized/SplitScreen/Screen.cs) which just takes the center offset at which to place the render texture on screen, a reference to the render texture, the render group it should be in (to not be rendered by the split screen cameras) and creates the sprite for it.
* I made sure the main camera goes through the normal forward renderer and applies the postfx at the end (so render left, render right, then the main path composits and applies postfx), while the two render target renderers only have a forward renderer, if you used the same render path for both, you'd end up applying post fx twice.

## Misc
The sample also switches out the graphics compositor to the one associated with the scene being switched to currently, currently only relevant for the **SplitScreen** sample.
