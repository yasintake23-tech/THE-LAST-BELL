# Chapter I interaction pose review

The contact sheet uses the existing `Avatar_Mesh.fbx` skinned mesh and hierarchy. The door grip, breaker grip bar and Feştek are the original project FBXs. The flashlight uses the cylinder dimensions serialized in the Unity scene. Lighting and camera angles are Blender workbench review settings.

The runtime uses thirteen authored pose profiles from `Assets/Resources/Chapter1HandPoses.json`. Values are per-joint flexion in the wrist's calibrated anatomical frame. The thumb's bind-side orientation defines the palm side, and actual joint-to-tip vectors define the distal axes. The runtime applies these poses relative to cached joint references after Unity's body and arm IK evaluation.

The eight frames cover relaxed, open, flashlight, door handle, breaker, floor pickup, carry and insertion. This review covers the static mesh and grip silhouettes. Unity retargeting and animated contact still need the Play Mode and Android checks.

In Unity, run **The Last Bell → Tests → Mobile interaction Play Mode** for the authored verification scenario. It clicks the existing mobile action and inventory buttons, drives the rotation dial through its pointer handlers, and checks the entrance, classroom, breaker, physical release, two pickup postures, duplicate presses, interruption, bounded rotation, withdrawal and storage. The regular Android build still runs the existing verification suite.
