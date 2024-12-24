# Simple Atlas Generator for Unity

A **simple and easy** atlas generator script for Unity that helps you combine **diffuse** (albedo) textures and **normal** maps into a single atlas, drastically reducing draw calls. It automatically updates your scene objects' meshes, UVs, and materials to reference the new atlases.

> **Version:** 0.1  
> **License:** Free to use for private and commercial projects.

---

## Features
- **Diffuse and Normal Map Atlasing:** Combine multiple diffuse textures (and optional normal maps) into a single texture atlas.
- **Automatic Mesh & Material Updating:** Replaces materials on all Mesh Renderers / Mesh Filters found in the selected GameObjects (including children) with the new atlas material.
- **Per-Texture Coloring:** If a material uses the same texture but a different color tint, the color can be baked into that texture’s area in the atlas.
- **Preview Before Generation:** Generate a quick, in-Editor **preview** of the atlas (diffuse + normal) before fully applying changes to the scene.
- **Regenerate Normals & Lightmap UVs:** Optionally recalculate normals and lightmap UVs for the newly created meshes.
- **Update Mesh Colliders:** Automatically swap out old mesh references in any attached Mesh Collider with the new atlas-optimized mesh.
- **Adjustable Padding & Atlas Size:** Configure padding between textures and choose from common atlas sizes (256, 512, 1024, 2048, 4096).
- **Shader Selection:** Choose which shader your new atlas material should use (defaults to Standard).
- **UV Remapping:** Automatically remaps UVs to fit the new atlas layout and clamps any out-of-range UVs.
- **Logically Handles 2, 3, 4 … up to 16 (or more) Textures:** See [Atlas Packing Examples](#atlas-packing-examples) below to understand how many rows and columns get generated for different texture counts.
- **Supports LOD Groups:** Shows how many LOD levels are in each selected GameObject at a glance.  

> *Future updates may add additional map support (e.g., occlusion, metallic, etc.).*

---

## Installation
1. **Download/Clone** the script file (`AtlasGenerator.cs`) from this repository.
2. Create an **Editor** folder anywhere under your `Assets/` directory (e.g. `Assets/Editor/`).
3. **Drag and drop** the `AtlasGenerator.cs` script into the `Editor` folder.
4. Once imported, you can open the Atlas Generator by going to **Tools** > **Roundy** > **Simple Atlas Generator** in the Unity Editor menu.

---

## Usage
1. **Open the Window:** Go to **Tools** > **Roundy** > **Simple Atlas Generator**.
2. **Add Objects:**  
   - Select the GameObjects in your scene that contain Mesh Renderers or LOD Groups you want atlased.  
   - Click **Add Selected Objects** in the window to populate the list.  
3. **Configure Settings:**  
   - Choose your **Max Atlas Size** (e.g., 2048).  
   - Set your desired **Material Name**.  
   - Select the **Shader** for the new atlas material.  
   - (Optional) **Apply Material Color** if you have tinted materials.  
   - (Optional) Enable **Normal Map Atlasing** if you want to atlas normal maps.  
   - Adjust the **Padding** between packed textures (in pixels).  
   - (Optional) **Regenerate Normals** and **Regenerate Lightmap UV** for the newly created meshes.  
   - (Optional) **Update Mesh Colliders** to reference the new atlased mesh.  
4. **Preview & Generate:**  
   - Use **Preview Atlas** to create an in-memory preview of the combined diffuse & normal maps (no changes to your scene).  
   - Click **Generate Atlas** to finalize and apply the new atlases and meshes to your scene.

---

## Atlas Packing Examples
When multiple textures need to be combined, the script tries to find an arrangement (rows × columns) that **minimizes empty space** while respecting your chosen `Max Atlas Size`. Here’s a simplified example showing how the tool might arrange up to 16 textures:

| Number of Textures | Possible Grid (Cols × Rows) | Wasted Cells?    |
|--------------------|-----------------------------|------------------|
| **2**              | 2 × 1 (or 1 × 2)            | 0                |
| **3**              | 2 × 2                       | 1 (empty cell)   |
| **4**              | 2 × 2                       | 0                |
| **5**              | 3 × 2                       | 1 (empty cell)   |
| **6**              | 3 × 2                       | 0                |
| **7**              | 4 × 2                       | 1 (empty cell)   |
| **8**              | 4 × 2                       | 0                |
| **9**              | 3 × 3                       | 0                |
| **10**             | 4 × 3                       | 2 (empty cells)  |
| **16**             | 4 × 4                       | 0                |

> **Note:** These are **general** examples. Exact rows/columns can change based on each texture’s width/height and your `Max Atlas Size`.

---

## How It Works
1. **Gathers** all diffuse and normal textures from your selected objects.  
2. **Ensures** each texture is set to “Readable” so it can be processed.  
3. **Calculates** an optimal grid layout (rows & columns) for your chosen atlas size, minimizing wasted space.  
4. **Copies** each texture into the combined atlas at the correct position (applying any per-texture color tint).  
5. **Creates** a new material using your selected shader and assigns the combined atlas as the main texture (and normal map if enabled).  
6. **Remaps** UVs for every mesh to match the new atlas layout.  
7. **(Optional)** **Regenerates normals, regenerates lightmap UVs,** and **updates Mesh Colliders** with the newly created mesh.  
8. **Saves** new meshes, textures, and materials in the generated **AtlasGeneratorFolder** under `Assets/`.

---

## Limitations
- **Single-Material Meshes Only:** This tool works only if each mesh uses **1 material maximum**. Meshes with submeshes or multiple materials are not yet supported.  
- **Out-of-Range UVs:** While the tool clamps UVs to the 0–1 range, heavily tiled textures (UVs far outside 0–1) will not atlas cleanly and may look incorrect.

---

## Notes
- If any meshes have **UVs out of 0–1 range**, the tool clamps them to 0–1 to avoid undesired tiling in the atlas.
- The **folder structure** is automatically created under `Assets/AtlasGeneratorFolder/`. This includes:
  - `Meshes/`
  - `Textures/`
  - `Materials/`
- The script currently only handles **Diffuse** (`_MainTex`) and **Normal** (`_BumpMap` / `_NormalMap`) textures.  
- Other material properties (like **Metallic**, **Roughness**, **Emission**) are **not** yet included.
- You can **re-run** the tool any time you want to add more objects or create a new atlas.

---

## Contributing
Contributions, issues, and feature requests are welcome.  
Feel free to **fork** the repo and open **pull requests**.

---

## License
**Free to use** for both private and commercial projects. No attribution required, but always appreciated!

Enjoy and happy atlasing!
