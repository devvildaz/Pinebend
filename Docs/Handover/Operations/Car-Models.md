# Add a car visual

1. Import the body as a mesh-only prefab. Use metres, +Z for forward, +Y for up, and position the pivot relative to the existing hatchback root. Keep colliders, rigidbodies, audio listeners, cameras and driving scripts on the existing car rig.
2. For animated wheels, use a separate wheel prefab centered on its axle, with the axle along X. Leave wheels out of the body mesh. The wheel prefab uses each of the four existing wheel sockets; leaving it empty keeps the current wheel visuals.
3. In the Project window choose **Create → Pinebend → Car visual model**. Assign the body and optional wheel prefab. Set the body offset, rotation, and scale and the wheel scale on this asset.
4. Open Assets/Pinebend/Pinebend.unity. Select a racer and drag the asset into **Test Car → Model**. Apply to every desired grid slot, then save the scene and rebuild. Leaving Model empty uses the hatchback. Regenerating the entire scene with CircuitBuilder.Create recreates the grid and its scene assignments.
5. For a multiplayer test distribute the same build to everyone. Visuals belong to grid slots in this release. Selecting or downloading a new model in the lobby is a separate feature.

This adapter changes the visible model only. Keep the wheelbase and body dimensions close to the prototype rig (2.3 m between axles, 1.72 m between wheel centers). A different wheelbase or chassis size needs a corresponding physics rig and spawn footprint.
