using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;
using System.IO;

// Define a class to hold texture and color information
public class TextureData
{
    public Texture2D texture;
    public bool applyColor;
    public Color color;

    public TextureData(Texture2D tex, bool colorFlag, Color col)
    {
        texture = tex;
        applyColor = colorFlag;
        color = col;
    }
}

// Define a class to hold combined diffuse and normal texture information
public class CombinedTextureData
{
    public Texture2D diffuseTexture;
    public Texture2D normalTexture;
    public bool applyColor;
    public Color color;

    public CombinedTextureData(Texture2D diffuseTex, Texture2D normalTex, bool colorFlag, Color col)
    {
        diffuseTexture = diffuseTex;
        normalTexture = normalTex;
        applyColor = colorFlag;
        color = col;
    }
}

public class SimpleAtlasGenerator : EditorWindow
{
    // List to store selected GameObjects
    private ReorderableList reorderableList;
    private List<GameObject> selectedObjects = new List<GameObject>();

    // Settings
    private int selectedAtlasSizeIndex = 3; // Default to 2048
    private readonly int[] atlasSizes = new int[] { 256, 512, 1024, 2048, 4096 };
    private string atlasMaterialName = "AtlasOptimized_Material";
    private Shader selectedShader;

    private bool applyMaterialColor = true; // Toggle to apply material colors
    private bool enableNormalMapAtlasing = false; // Toggle for normal map packing
    private string normalAtlasMaterialName = "AtlasOptimized_Normal"; // Name for normal atlas material

    private int padding = 1;

    // Folder Structure
    private string rootFolderName = "SimpleAtlasGeneratorFolder";
    private string meshesFolderName = "Meshes";
    private string texturesFolderName = "Textures";
    private string materialsFolderName = "Materials";

    // Progress Indicator
    private bool isProcessing = false;

    // Warnings
    private List<string> uvWarnings = new List<string>();

    // Shader List
    private List<Shader> availableShaders = new List<Shader>();
    private string[] shaderNames;
    private int selectedShaderIndex = -1;

    // Scroll position for the ReorderableList
    private Vector2 listScrollPos;

    // ========= NEW FIELDS FOR FEATURES =========
    private bool regenerateNormals = false;
    private bool regenerateLightmapUV = false;
    private bool updateMeshColliders = false;

    // Preview-related
    private bool showPreview = false;                // If true, display preview in the EditorWindow
    private Texture2D previewDiffuseAtlas = null;    // Holds a generated diffuse atlas for preview
    private Texture2D previewNormalAtlas = null;     // Holds a generated normal atlas for preview

    // ========= Scroll for the entire window =========
    private Vector2 mainScrollPos;

    [MenuItem("Tools/Roundy/Simple Atlas Generator")]
    public static void ShowWindow()
    {
        GetWindow<SimpleAtlasGenerator>("Simple Atlas Generator v0.1");
    }

    private void OnEnable()
    {
        // Initialize the ReorderableList
        reorderableList = new ReorderableList(selectedObjects, typeof(GameObject), true, true, true, true);

        reorderableList.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, "Selected Objects");
        };

        reorderableList.drawElementCallback = DrawReorderableListElement;

        // Define the element height callback
        reorderableList.elementHeightCallback = (int index) =>
        {
            return EditorGUIUtility.singleLineHeight + 4; // Single row height
        };

        // Custom onAddCallback to add a null slot
        reorderableList.onAddCallback = (ReorderableList list) =>
        {
            selectedObjects.Add(null);
            Debug.Log("Simple Atlas Generator: Added a new slot. Drag a GameObject into the slot.");
        };

        reorderableList.onRemoveCallback = (ReorderableList list) =>
        {
            if (EditorUtility.DisplayDialog("Confirm Removal", "Are you sure you want to remove the selected object?", "Yes", "No"))
            {
                selectedObjects.RemoveAt(list.index);
                Debug.Log("Simple Atlas Generator: Removed selected object from the list.");
                Repaint();
            }
        };

        // Populate availableShaders
        availableShaders = Resources.FindObjectsOfTypeAll<Shader>().ToList();
        shaderNames = availableShaders.Select(s => s.name).ToArray();

        // Set default shader index
        if (selectedShader != null)
        {
            selectedShaderIndex = availableShaders.IndexOf(selectedShader);
        }
        else
        {
            // Default to Standard shader
            selectedShaderIndex = availableShaders.FindIndex(s => s.name == "Standard");
            if (selectedShaderIndex == -1 && availableShaders.Count > 0)
                selectedShaderIndex = 0;
        }
    }

    private void DrawReorderableListElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        if (index < selectedObjects.Count)
        {
            GameObject obj = selectedObjects[index];
            rect.y += 2;

            // Define column widths
            float objectColumnWidth = rect.width * 0.5f;
            float statsColumnWidth = rect.width * 0.45f; // Adjust as needed
            float spacing = rect.width * 0.05f;

            // Define positions
            Rect objectFieldRect = new Rect(rect.x, rect.y, objectColumnWidth, EditorGUIUtility.singleLineHeight);
            Rect pingButtonRect = new Rect(rect.x + objectColumnWidth + spacing, rect.y, 18, EditorGUIUtility.singleLineHeight);
            Rect statsRect = new Rect(rect.x + objectColumnWidth + spacing + 20, rect.y, statsColumnWidth - 20, EditorGUIUtility.singleLineHeight);

            // Display the GameObject field
            selectedObjects[index] = (GameObject)EditorGUI.ObjectField(
                objectFieldRect,
                obj,
                typeof(GameObject),
                true
            );

            // Add a button to ping the object in the hierarchy
            if (GUI.Button(pingButtonRect, "P"))
            {
                if (obj != null)
                {
                    Selection.activeGameObject = obj;
                    EditorGUIUtility.PingObject(obj);
                }
            }

            // Display stats beside the GameObject field
            if (obj != null)
            {
                LODGroup lodGroup = obj.GetComponent<LODGroup>();
                string lodInfo = lodGroup != null
                    ? $"LOD Levels: {lodGroup.GetLODs().Length}"
                    : "No LOD Group";

                string vertexInfo = "";
                MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    int vertexCount = meshFilter.sharedMesh.vertexCount;
                    vertexInfo = $"Vertices: {vertexCount}";
                }

                // Combine stats into a single line
                string combinedStats = lodInfo;
                if (!string.IsNullOrEmpty(vertexInfo))
                {
                    combinedStats += " | " + vertexInfo;
                }

                EditorGUI.LabelField(
                    statsRect,
                    combinedStats
                );
            }
        }
    }

    private void OnGUI()
    {
        // Begin a scroll view that wraps the entire window's content
        mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos);

        GUILayout.Label("Simple Atlas Generator Settings", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Selected Objects"))
        {
            AddSelectedObjects();
        }
        if (GUILayout.Button("Clear List"))
        {
            selectedObjects.Clear();
            Debug.Log("Simple Atlas Generator: Cleared selected objects list.");
            Repaint();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Limit the ReorderableList display to 10 items and add a scrollbar
        int maxVisibleItems = 10;
        float elementHeight = reorderableList.elementHeight;
        float headerHeight = reorderableList.headerHeight;
        float listHeight = headerHeight + elementHeight * maxVisibleItems;

        listScrollPos = EditorGUILayout.BeginScrollView(listScrollPos, GUILayout.Height(listHeight));
        reorderableList.DoLayoutList();
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);

        // Atlas Size Dropdown
        GUILayout.BeginHorizontal();
        GUILayout.Label("Max Atlas Size", GUILayout.Width(100));
        selectedAtlasSizeIndex = EditorGUILayout.Popup(
            selectedAtlasSizeIndex,
            atlasSizes.Select(size => size.ToString()).ToArray(),
            GUILayout.Width(100)
        );
        GUILayout.EndHorizontal();

        // Material Name Input
        GUILayout.BeginHorizontal();
        GUILayout.Label("Material Name", GUILayout.Width(100));
        atlasMaterialName = EditorGUILayout.TextField(atlasMaterialName);
        GUILayout.EndHorizontal();

        // Shader Selection Dropdown
        GUILayout.BeginHorizontal();
        GUILayout.Label("Target Shader", GUILayout.Width(100));
        if (availableShaders.Count > 0)
        {
            selectedShaderIndex = EditorGUILayout.Popup(selectedShaderIndex, shaderNames, GUILayout.Width(200));
            selectedShader = availableShaders[selectedShaderIndex];
        }
        else
        {
            EditorGUILayout.LabelField("No shaders found.", GUILayout.Width(200));
        }
        GUILayout.EndHorizontal();

        // Refresh Shaders Button
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Shaders"))
        {
            availableShaders = Resources.FindObjectsOfTypeAll<Shader>().ToList();
            shaderNames = availableShaders.Select(s => s.name).ToArray();
            selectedShaderIndex = availableShaders.FindIndex(s => s.name == "Standard");
            if (selectedShaderIndex == -1 && availableShaders.Count > 0)
                selectedShaderIndex = 0;
            Repaint();
        }
        GUILayout.EndHorizontal();

        // Apply Material Color Checkbox
        GUILayout.BeginHorizontal();
        applyMaterialColor = EditorGUILayout.Toggle("Apply Material Color", applyMaterialColor, GUILayout.Width(200));
        GUILayout.EndHorizontal();

        // Padding Input
        GUILayout.BeginHorizontal();
        GUILayout.Label("Padding (px)", GUILayout.Width(100));
        padding = EditorGUILayout.IntField(padding, GUILayout.Width(50));
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Normal Map Packing Toggle
        GUILayout.BeginHorizontal();
        enableNormalMapAtlasing = EditorGUILayout.Toggle("Normal Map Atlasing", enableNormalMapAtlasing, GUILayout.Width(200));
        GUILayout.EndHorizontal();

        if (enableNormalMapAtlasing)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Normal Material Name", GUILayout.Width(150));
            normalAtlasMaterialName = EditorGUILayout.TextField(normalAtlasMaterialName);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        // ========= NEW TOGGLES =========
        regenerateNormals = EditorGUILayout.Toggle("Regenerate Normals", regenerateNormals);
        regenerateLightmapUV = EditorGUILayout.Toggle("Regenerate Lightmap UV", regenerateLightmapUV);
        updateMeshColliders = EditorGUILayout.Toggle("Update Mesh Colliders", updateMeshColliders);

        GUILayout.Space(10);

        // Buttons: Preview and Generate
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Preview Atlas") && !isProcessing)
        {
            // Clear old warnings and preview atlases
            uvWarnings.Clear();
            previewDiffuseAtlas = null;
            previewNormalAtlas = null;
            showPreview = false;

            isProcessing = true;
            PreviewAtlas(); // Only generate atlases for preview
            isProcessing = false;
        }

        if (GUILayout.Button("Generate Atlas") && !isProcessing)
        {
            // Clear old warnings, old previews
            uvWarnings.Clear();
            previewDiffuseAtlas = null;
            previewNormalAtlas = null;
            showPreview = false;

            if (selectedObjects.Count == 0 || selectedObjects.All(obj => obj == null))
            {
                EditorUtility.DisplayDialog("Simple Atlas Generator", "No valid objects selected.", "OK");
                Debug.LogWarning("Simple Atlas Generator: No valid GameObjects selected.");
                return;
            }

            isProcessing = true;
            GenerateAtlas();
            isProcessing = false;
        }
        GUILayout.EndHorizontal();

        // Show the preview if available
        if (showPreview && previewDiffuseAtlas != null)
        {
            GUILayout.Space(10);
            GUILayout.Label("Diffuse Atlas Preview:", EditorStyles.boldLabel);

            // Force the preview to a fixed 256x256 size
            Rect previewRect = GUILayoutUtility.GetRect(256, 256);
            EditorGUI.DrawPreviewTexture(previewRect, previewDiffuseAtlas, null, ScaleMode.ScaleToFit);

            if (previewNormalAtlas != null)
            {
                GUILayout.Label("Normal Atlas Preview:", EditorStyles.boldLabel);
                Rect previewRectNormal = GUILayoutUtility.GetRect(256, 256);
                EditorGUI.DrawPreviewTexture(previewRectNormal, previewNormalAtlas, null, ScaleMode.ScaleToFit);
            }
        }

        GUILayout.Space(10);

        // Display Warnings (including the note if UV is out of range)
        if (uvWarnings.Count > 0)
        {
            EditorGUILayout.HelpBox(
                "Some objects have UVs outside the 0-1 range.\n" +
                "The resulting atlas might look correct in preview, but final mapping could be problematic.",
                MessageType.Warning
            );
            foreach (var warning in uvWarnings)
            {
                EditorGUILayout.LabelField(warning);
            }
        }

        if (isProcessing)
        {
            GUILayout.Label("Processing...", EditorStyles.boldLabel);
        }

        // End the main scroll view
        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// Function to *only* generate the atlas textures (diffuse and normal) for a visual preview, 
    /// without modifying the scene meshes or materials.
    /// </summary>
    private void PreviewAtlas()
    {
        Debug.Log("Simple Atlas Generator: PreviewAtlas method invoked.");

        try
        {
            // Filter out null GameObjects
            List<GameObject> validObjects = selectedObjects.Where(obj => obj != null).ToList();
            Debug.Log($"Simple Atlas Generator: {validObjects.Count} valid GameObjects to process for preview.");

            if (validObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("Simple Atlas Generator", "No valid objects selected.", "OK");
                Debug.LogWarning("Simple Atlas Generator: No valid GameObjects for preview.");
                return;
            }

            // Collect all MeshRenderers
            List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
            foreach (var obj in validObjects)
            {
                var childRenderers = obj.GetComponentsInChildren<MeshRenderer>(true);
                meshRenderers.AddRange(childRenderers);
            }

            if (meshRenderers.Count == 0)
            {
                EditorUtility.DisplayDialog("Simple Atlas Generator", "No MeshRenderers found in selected objects.", "OK");
                return;
            }

            // Dictionary to store unique textures
            Dictionary<(Texture2D, Color), CombinedTextureData> uniqueTexturesAndColors
                = new Dictionary<(Texture2D, Color), CombinedTextureData>();
            List<CombinedTextureData> combinedTextureDataList = new List<CombinedTextureData>();

            // Gather textures
            foreach (var renderer in meshRenderers)
            {
                if (renderer == null) continue;

                Material mat = renderer.sharedMaterial;
                if (mat != null)
                {
                    Texture2D diffuseTex = mat.mainTexture as Texture2D;
                    if (diffuseTex == null)
                    {
                        diffuseTex = Texture2D.whiteTexture;
                    }

                    // Color
                    Color color = Color.white;
                    bool applyColor = false;
                    if (applyMaterialColor && mat.HasProperty("_Color"))
                    {
                        color = mat.color;
                        applyColor = (color != Color.white);
                    }

                    // Normal map
                    Texture2D normalTex = null;
                    if (enableNormalMapAtlasing)
                    {
                        if (mat.HasProperty("_BumpMap"))
                        {
                            normalTex = mat.GetTexture("_BumpMap") as Texture2D;
                        }
                        else if (mat.HasProperty("_NormalMap"))
                        {
                            normalTex = mat.GetTexture("_NormalMap") as Texture2D;
                        }

                        if (normalTex == null)
                            normalTex = GenerateFlatNormalMap(texSize: 128);
                    }

                    EnsureTextureIsReadable(diffuseTex);
                    if (normalTex != null) EnsureTextureIsReadable(normalTex);

                    var key = (diffuseTex, color);
                    if (!uniqueTexturesAndColors.ContainsKey(key))
                    {
                        var ctd = new CombinedTextureData(diffuseTex, normalTex, applyColor, color);
                        uniqueTexturesAndColors.Add(key, ctd);
                        combinedTextureDataList.Add(ctd);
                    }
                }
            }

            // Check UV out of bounds
            foreach (var renderer in meshRenderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                Vector2[] uvs = filter.sharedMesh.uv;
                foreach (var uv in uvs)
                {
                    if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                    {
                        uvWarnings.Add($"Object '{renderer.gameObject.name}' has UVs outside the 0-1 range.");
                        break;
                    }
                }
            }

            if (combinedTextureDataList.Count == 0)
            {
                EditorUtility.DisplayDialog("Simple Atlas Generator", "No textures found among the selected objects.", "OK");
                return;
            }

            // Calculate grid
            (int rows, int columns, int texSize) = CalculateOptimalGrid(
                combinedTextureDataList.Count,
                atlasSizes[selectedAtlasSizeIndex],
                combinedTextureDataList.Select(
                    ctd => new TextureData(ctd.diffuseTexture, ctd.applyColor, ctd.color)
                ).ToList()
            );
            if (rows == 0 || columns == 0 || texSize == 0)
            {
                EditorUtility.DisplayDialog("Simple Atlas Generator", "Failed to calculate optimal grid for atlas preview.", "OK");
                return;
            }

            // Create Diffuse Atlas
            previewDiffuseAtlas = CreateDiffuseAtlas(
                combinedTextureDataList,
                atlasSizes[selectedAtlasSizeIndex],
                "Assets", // Store in memory; no permanent saving needed for preview
                atlasMaterialName + "_Preview",
                padding,
                rows,
                columns,
                texSize,
                saveAtlasToDisk: false // <--- pass a flag to avoid saving
            );

            // Create Normal Atlas if enabled
            if (enableNormalMapAtlasing)
            {
                previewNormalAtlas = CreateNormalAtlas(
                    combinedTextureDataList,
                    atlasSizes[selectedAtlasSizeIndex],
                    "Assets",
                    normalAtlasMaterialName + "_Preview",
                    padding,
                    rows,
                    columns,
                    texSize,
                    saveAtlasToDisk: false
                );
            }

            showPreview = true;
            Debug.Log("Simple Atlas Generator: Preview atlases generated successfully.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Simple Atlas Generator: An error occurred during preview: {ex.Message}");
            EditorUtility.DisplayDialog("Simple Atlas Generator", "An error occurred during preview. See the console for details.", "OK");
        }
    }

    private void AddSelectedObjects()
    {
        foreach (var obj in Selection.gameObjects)
        {
            if (!selectedObjects.Contains(obj))
            {
                selectedObjects.Add(obj);
                Debug.Log($"Simple Atlas Generator: Added '{obj.name}' to the list.");
            }
        }
        Repaint();
    }

    /// <summary>
    /// Generates a unique asset path by appending a number if the asset already exists.
    /// </summary>
    private string GetUniqueAssetPath(string basePath, string baseName, string extension)
    {
        string assetPath = Path.Combine(basePath, baseName + extension);
        int counter = 1;
        while (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
        {
            assetPath = Path.Combine(basePath, $"{baseName}_{counter}{extension}");
            counter++;
        }
        return assetPath;
    }

    private void GenerateAtlas()
    {
        Debug.Log("Simple Atlas Generator: GenerateAtlas method invoked.");

        try
        {
            // Filter out null GameObjects
            List<GameObject> validObjects = selectedObjects.Where(obj => obj != null).ToList();
            Debug.Log($"Simple Atlas Generator: {validObjects.Count} valid GameObjects to process.");

            if (validObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("Simple Atlas Generator", "No valid objects selected.", "OK");
                Debug.LogWarning("Simple Atlas Generator: No valid GameObjects to process.");
                return;
            }

            // Step 1: Setup folders
            string rootPath = Path.Combine("Assets", rootFolderName);
            string meshesPath = Path.Combine(rootPath, meshesFolderName);
            string texturesPath = Path.Combine(rootPath, texturesFolderName);
            string materialsPath = Path.Combine(rootPath, materialsFolderName);
            string normalTexturesPath = Path.Combine(texturesPath, "Normals"); // for normal maps

            CreateFolderIfNotExists(rootPath);
            CreateFolderIfNotExists(meshesPath);
            CreateFolderIfNotExists(texturesPath);
            CreateFolderIfNotExists(materialsPath);
            if (enableNormalMapAtlasing)
            {
                CreateFolderIfNotExists(normalTexturesPath);
            }

            Debug.Log($"Simple Atlas Generator: Folder structure ensured at '{rootPath}'.");

            // Step 2: Collect *all* MeshRenderers from the selected objects (including children)
            List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
            foreach (var obj in validObjects)
            {
                var childRenderers = obj.GetComponentsInChildren<MeshRenderer>(true);
                meshRenderers.AddRange(childRenderers);
            }

            // Dictionary to store unique diffuse textures and colors
            Dictionary<(Texture2D, Color), CombinedTextureData> uniqueTexturesAndColors
                = new Dictionary<(Texture2D, Color), CombinedTextureData>();
            List<CombinedTextureData> combinedTextureDataList = new List<CombinedTextureData>();
            Dictionary<MeshRenderer, CombinedTextureData> rendererToTextureData = new Dictionary<MeshRenderer, CombinedTextureData>();

            // Step 3: For each MeshRenderer, gather material textures (diffuse + normal)
            foreach (var renderer in meshRenderers)
            {
                if (renderer == null) continue;

                Material mat = renderer.sharedMaterial;
                if (mat != null)
                {
                    // Diffuse texture
                    Texture2D diffuseTex = mat.mainTexture as Texture2D;
                    if (diffuseTex == null)
                    {
                        diffuseTex = Texture2D.whiteTexture;
                        Debug.LogWarning($"Simple Atlas Generator: No _MainTex found for '{renderer.gameObject.name}'. Using white texture.");
                    }

                    // Color
                    Color color = Color.white;
                    bool applyColor = false;
                    if (applyMaterialColor && mat.HasProperty("_Color"))
                    {
                        color = mat.color;
                        applyColor = (color != Color.white);
                    }

                    // Normal map
                    Texture2D normalTex = null;
                    if (enableNormalMapAtlasing)
                    {
                        if (mat.HasProperty("_BumpMap"))
                        {
                            normalTex = mat.GetTexture("_BumpMap") as Texture2D;
                        }
                        else if (mat.HasProperty("_NormalMap"))
                        {
                            normalTex = mat.GetTexture("_NormalMap") as Texture2D;
                        }

                        if (normalTex == null)
                        {
                            normalTex = GenerateFlatNormalMap(texSize: 128);
                            Debug.Log($"Simple Atlas Generator: Generated flat normal map for '{renderer.gameObject.name}'.");
                        }
                    }

                    // Ensure textures are readable
                    EnsureTextureIsReadable(diffuseTex);
                    if (normalTex != null) EnsureTextureIsReadable(normalTex);

                    // Use both texture and color as the key
                    var key = (diffuseTex, color);
                    if (!uniqueTexturesAndColors.TryGetValue(key, out CombinedTextureData ctd))
                    {
                        ctd = new CombinedTextureData(diffuseTex, normalTex, applyColor, color);
                        uniqueTexturesAndColors.Add(key, ctd);
                        combinedTextureDataList.Add(ctd);

                        if (applyColor)
                        {
                            Debug.Log($"Simple Atlas Generator: Added new texture-color combination for '{renderer.gameObject.name}'. Color: {color}");
                        }
                    }
                    else
                    {
                        // If we already have an entry, but there's a different normal map, keep the first encountered
                        if (normalTex != null && ctd.normalTexture != normalTex)
                        {
                            Debug.LogWarning(
                                $"Simple Atlas Generator: Different normal map found for the same diffuse texture & color in '{renderer.gameObject.name}'. " +
                                "Using the first encountered normal map."
                            );
                        }
                    }

                    rendererToTextureData[renderer] = uniqueTexturesAndColors[key];
                }
            }

            if (meshRenderers.Count == 0)
            {
                EditorUtility.DisplayDialog("Simple Atlas Generator", "No MeshRenderers found in selected objects.", "OK");
                Debug.LogWarning("Simple Atlas Generator: No MeshRenderers found in selected objects.");
                return;
            }

            Debug.Log($"Simple Atlas Generator: Collected {meshRenderers.Count} MeshRenderers and {combinedTextureDataList.Count} unique texture-color combos.");

            // Step 4: Check for UV out-of-bounds and collect warnings
            foreach (var renderer in meshRenderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                Vector2[] uvs = filter.sharedMesh.uv;
                foreach (var uv in uvs)
                {
                    if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                    {
                        uvWarnings.Add($"Object '{renderer.gameObject.name}' has UVs outside the 0-1 range.");
                        break; // Only add one warning per object
                    }
                }
            }

            // Step 5: Calculate optimal grid
            Debug.Log("Simple Atlas Generator: Calculating optimal grid for texture atlases.");
            (int rows, int columns, int singleTexSize) = CalculateOptimalGrid(
                combinedTextureDataList.Count,
                atlasSizes[selectedAtlasSizeIndex],
                combinedTextureDataList
                    .Select(ctd => new TextureData(ctd.diffuseTexture, ctd.applyColor, ctd.color))
                    .ToList()
            );
            if (rows == 0 || columns == 0 || singleTexSize == 0)
            {
                EditorUtility.DisplayDialog("Simple Atlas Generator", "Failed to calculate optimal grid for atlas packing.", "OK");
                Debug.LogError("Simple Atlas Generator: Failed to calculate optimal grid for atlas packing.");
                return;
            }

            // Step 6: Create diffuse atlas
            Debug.Log("Simple Atlas Generator: Creating diffuse atlas texture.");
            Texture2D diffuseAtlas = CreateDiffuseAtlas(
                combinedTextureDataList,
                atlasSizes[selectedAtlasSizeIndex],
                texturesPath,
                atlasMaterialName,
                padding,
                rows,
                columns,
                singleTexSize
            );
            if (diffuseAtlas == null)
            {
                EditorUtility.DisplayDialog("Simple Atlas Generator", "Failed to create diffuse atlas texture.", "OK");
                Debug.LogError("Simple Atlas Generator: Failed to create diffuse atlas texture.");
                return;
            }

            // Step 7: Create normal atlas if needed
            Texture2D normalAtlas = null;
            if (enableNormalMapAtlasing && combinedTextureDataList.Count > 0)
            {
                Debug.Log("Simple Atlas Generator: Creating normal atlas texture using the same grid.");
                normalAtlas = CreateNormalAtlas(
                    combinedTextureDataList,
                    atlasSizes[selectedAtlasSizeIndex],
                    Path.Combine(texturesPath, "Normals"),
                    normalAtlasMaterialName,
                    padding,
                    rows,
                    columns,
                    singleTexSize
                );

                if (normalAtlas == null)
                {
                    EditorUtility.DisplayDialog("Simple Atlas Generator", "Failed to create normal atlas texture.", "OK");
                    Debug.LogError("Simple Atlas Generator: Failed to create normal atlas texture.");
                    return;
                }
            }

            // Step 8: Create diffuse material
            Debug.Log("Simple Atlas Generator: Creating diffuse atlas material.");
            Material atlasMaterial = new Material(selectedShader != null ? selectedShader : Shader.Find("Standard"));
            atlasMaterial.name = atlasMaterialName;
            atlasMaterial.mainTexture = diffuseAtlas;
            if (atlasMaterial.HasProperty("_Glossiness"))
            {
                atlasMaterial.SetFloat("_Glossiness", 0f);
            }

            string materialPath = GetUniqueAssetPath(materialsPath, atlasMaterial.name, ".mat");
            AssetDatabase.CreateAsset(atlasMaterial, materialPath);
            Debug.Log($"Simple Atlas Generator: Created material asset at '{materialPath}'.");

            // Step 9: Assign atlas materials, remap UVs, and (optionally) regenerate normals/lightmap UV, update mesh colliders
            Debug.Log("Simple Atlas Generator: Assigning atlas materials and remapping UVs.");
            Undo.RegisterCompleteObjectUndo(meshRenderers.Select(r => r.gameObject).ToArray(), "Simple Atlas Generator");

            // Compute the final atlas dimensions (including padding). We'll use these to map sub-UVs correctly.
            int atlasWidth = columns * (singleTexSize + padding * 2);
            int atlasHeight = rows * (singleTexSize + padding * 2);

            foreach (var renderer in meshRenderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    Debug.LogWarning($"Simple Atlas Generator: MeshFilter or Mesh is missing on '{renderer.gameObject.name}'. Skipping.");
                    continue;
                }

                // Duplicate mesh
                Mesh originalMesh = filter.sharedMesh;
                Mesh newMesh = Instantiate(originalMesh);
                newMesh.name = originalMesh.name + "_AtlasOptimized";

                // Optionally regenerate normals
                if (regenerateNormals)
                {
                    newMesh.RecalculateNormals();
                }
                // Optionally regenerate lightmap UV
                if (regenerateLightmapUV)
                {
                    Unwrapping.GenerateSecondaryUVSet(newMesh);
                }

                // Save the new mesh asset
                string meshAssetName = newMesh.name;
                string meshPath = GetUniqueAssetPath(meshesPath, meshAssetName, ".asset");
                AssetDatabase.CreateAsset(newMesh, meshPath);
                Undo.RegisterCreatedObjectUndo(newMesh, "Simple Atlas Generator Create Mesh");
                Debug.Log($"Simple Atlas Generator: Created mesh asset at '{meshPath}'.");

                // Find the correct texture data for this renderer
                if (!rendererToTextureData.TryGetValue(renderer, out CombinedTextureData ctd))
                {
                    Debug.LogError($"Simple Atlas Generator: No texture data found for renderer on '{renderer.gameObject.name}'. Skipping UV remapping.");
                    continue;
                }

                // Figure out the index in combinedTextureDataList
                int texIndex = combinedTextureDataList.IndexOf(ctd);
                if (texIndex == -1)
                {
                    Debug.LogError($"Simple Atlas Generator: Texture data not found in combined list for '{renderer.gameObject.name}'. Skipping UV remapping.");
                    continue;
                }

                // Compute row/col from texIndex
                int row = texIndex / columns;
                int col = texIndex % columns;

                // =============================
                // NEW UV-Remapping (with padding)
                // =============================
                // - Each sub-texture is 'singleTexSize' wide/high within a cell of (singleTexSize + 2*padding).
                // - So the fraction of the atlas used by each sub-texture is: singleTexSize / atlasWidth, etc.
                float subUSize = (float)singleTexSize / atlasWidth;
                float subVSize = (float)singleTexSize / atlasHeight;

                float subUStart = (col * (singleTexSize + 2 * padding) + padding) / (float)atlasWidth;
                float subVStart = (row * (singleTexSize + 2 * padding) + padding) / (float)atlasHeight;

                Vector2[] originalUV = originalMesh.uv;
                Vector2[] newUV = new Vector2[originalUV.Length];

                bool hasTiling = false;
                foreach (var uv in originalUV)
                {
                    if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                    {
                        hasTiling = true;
                        break;
                    }
                }

                for (int v = 0; v < originalUV.Length; v++)
                {
                    Vector2 uv = originalUV[v];

                    // If you want to clamp out-of-range UVs
                    if (hasTiling)
                    {
                        uv.x = Mathf.Clamp01(uv.x);
                        uv.y = Mathf.Clamp01(uv.y);
                    }

                    // Map [0..1] => sub-texture in the atlas
                    newUV[v] = new Vector2(
                        subUStart + uv.x * subUSize,
                        subVStart + uv.y * subVSize
                    );
                }

                newMesh.uv = newUV;
                newMesh.RecalculateBounds();

                // Assign new mesh and atlas material
                filter.sharedMesh = newMesh;
                renderer.sharedMaterial = atlasMaterial;
                Debug.Log($"Simple Atlas Generator: Updated '{renderer.gameObject.name}' with new mesh and atlas material. Texture index: {texIndex}");

                // Assign normal atlas to the material, if needed
                if (enableNormalMapAtlasing && normalAtlas != null)
                {
                    renderer.sharedMaterial.SetTexture("_BumpMap", normalAtlas);
                    renderer.sharedMaterial.EnableKeyword("_NORMALMAP");
                    Debug.Log($"Simple Atlas Generator: Assigned normal atlas to '{renderer.gameObject.name}'.");
                }

                // Optionally update mesh colliders
                if (updateMeshColliders)
                {
                    // If there's a MeshCollider referencing the original mesh, update it
                    MeshCollider[] meshColliders = renderer.GetComponents<MeshCollider>();
                    foreach (var mc in meshColliders)
                    {
                        if (mc.sharedMesh == originalMesh)
                        {
                            Undo.RecordObject(mc, "Atlas Generator - update mesh collider");
                            mc.sharedMesh = newMesh;
                            Debug.Log($"Simple Atlas Generator: Updated MeshCollider on '{renderer.gameObject.name}' to the new mesh.");
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Simple Atlas Generator: Optimization process completed successfully.");
            EditorUtility.DisplayDialog("Simple Atlas Generator", "Atlas generation completed successfully.", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Simple Atlas Generator: An error occurred during generation: {ex.Message}");
            EditorUtility.DisplayDialog("Simple Atlas Generator", "An error occurred during generation. See the console for details.", "OK");
        }
    }


    private void CreateFolderIfNotExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path);
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                CreateFolderIfNotExists(parent);
            }
            string newFolderPath = AssetDatabase.CreateFolder(parent, folderName);
            if (!string.IsNullOrEmpty(newFolderPath))
            {
                Debug.Log($"Simple Atlas Generator: Created folder '{newFolderPath}'.");
            }
            else
            {
                Debug.LogWarning($"Simple Atlas Generator: Failed to create folder '{path}'.");
            }
        }
    }

    private void EnsureTextureIsReadable(Texture2D texture)
    {
        if (texture == null) return;

        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            Debug.Log($"Simple Atlas Generator: Made texture '{texture.name}' readable.");
        }
    }

    /// <summary>
    /// Calculates the optimal number of rows and columns for the atlas grid to minimize unused space.
    /// Returns the number of rows, columns, and the texture size.
    /// </summary>
    private (int rows, int columns, int texSize) CalculateOptimalGrid(
        int texCount,
        int maxAtlasSize,
        List<TextureData> textureDataList
    )
    {
        int optimalRows = 0;
        int optimalColumns = 0;
        int minimalWaste = int.MaxValue;
        int optimalTexSize = 0;

        // Determine the maximum texture size among the collected textures
        int maxTexWidth = textureDataList.Max(td => td.texture.width);
        int maxTexHeight = textureDataList.Max(td => td.texture.height);
        int currentTexSize = Mathf.NextPowerOfTwo(Mathf.Max(maxTexWidth, maxTexHeight));

        // Start with the largest possible texture size and reduce if necessary
        while (currentTexSize >= 16)
        {
            for (int columns = 1; columns <= texCount; columns++)
            {
                int rows = Mathf.CeilToInt((float)texCount / columns);

                int atlasWidth = columns * (currentTexSize + padding * 2);
                int atlasHeight = rows * (currentTexSize + padding * 2);

                if (atlasWidth > maxAtlasSize || atlasHeight > maxAtlasSize)
                    continue;

                int waste = (columns * rows) - texCount;
                if (waste < minimalWaste)
                {
                    minimalWaste = waste;
                    optimalRows = rows;
                    optimalColumns = columns;
                    optimalTexSize = currentTexSize;

                    if (waste == 0)
                        break; // Perfect fit
                }
            }

            if (optimalRows > 0 && optimalColumns > 0)
                break; // Found a suitable grid

            currentTexSize /= 2;
        }

        if (optimalRows == 0 || optimalColumns == 0 || optimalTexSize == 0)
        {
            Debug.LogError("Simple Atlas Generator: Unable to fit textures into atlas within the maximum atlas size.");
            return (0, 0, 0);
        }

        Debug.Log($"Simple Atlas Generator: Optimal grid calculated - Rows: {optimalRows}, Columns: {optimalColumns}, Texture Size: {optimalTexSize}");
        return (optimalRows, optimalColumns, optimalTexSize);
    }

    private Texture2D CreateDiffuseAtlas(
        List<CombinedTextureData> combinedTextureDataList,
        int maxSize,
        string texturesPath,
        string atlasName,
        int padding,
        int rows,
        int columns,
        int texSize,
        bool saveAtlasToDisk = true // <--- extra flag for preview
    )
    {
        if (combinedTextureDataList.Count == 0)
        {
            Debug.LogError("Simple Atlas Generator: No diffuse textures to atlas.");
            return null;
        }

        int atlasWidth = columns * (texSize + padding * 2);
        int atlasHeight = rows * (texSize + padding * 2);

        Debug.Log($"Simple Atlas Generator: Creating diffuse atlas with {columns} columns and {rows} rows. Atlas size: {atlasWidth}x{atlasHeight}.");

        Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
        atlas.name = atlasName + "_DiffuseAtlas";

        // Fill with white
        Color[] clearColors = Enumerable.Repeat(Color.white, atlasWidth * atlasHeight).ToArray();
        atlas.SetPixels(clearColors);

        for (int i = 0; i < combinedTextureDataList.Count; i++)
        {
            CombinedTextureData ctd = combinedTextureDataList[i];
            Texture2D diffuseTex = ctd.diffuseTexture;

            EnsureTextureIsReadable(diffuseTex);
            Texture2D resizedTex = ResizeTexture(diffuseTex, texSize, texSize, false);

            int row = i / columns;
            int col = i % columns;

            int x = col * (texSize + padding * 2) + padding;
            int y = row * (texSize + padding * 2) + padding;

            Color[] diffusePixels = resizedTex.GetPixels();

            // Apply color if needed
            if (ctd.applyColor)
            {
                for (int p = 0; p < diffusePixels.Length; p++)
                {
                    diffusePixels[p] = diffusePixels[p] * ctd.color;
                }
            }

            atlas.SetPixels(x, y, texSize, texSize, diffusePixels);
        }

        atlas.Apply();

        if (saveAtlasToDisk)
        {
            // Save atlas as PNG
            string atlasPath = GetUniqueAssetPath(texturesPath, atlas.name, ".png");
            byte[] atlasBytes = atlas.EncodeToPNG();
            File.WriteAllBytes(atlasPath, atlasBytes);
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
            Texture2D importedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

            // Configure texture importer for diffuse
            TextureImporter atlasImporter = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
            if (atlasImporter != null)
            {
                atlasImporter.isReadable = true;
                atlasImporter.textureCompression = TextureImporterCompression.Uncompressed;
                atlasImporter.sRGBTexture = true; // Use sRGB for diffuse textures
                atlasImporter.mipmapEnabled = true;
                atlasImporter.filterMode = FilterMode.Bilinear;
                atlasImporter.SaveAndReimport();

                return importedAtlas;
            }
            return importedAtlas; // fallback if importer is null
        }
        else
        {
            // Preview mode: just return the in-memory atlas
            return atlas;
        }
    }

    private Texture2D CreateNormalAtlas(
        List<CombinedTextureData> combinedTextureDataList,
        int maxSize,
        string normalTexturesPath,
        string atlasName,
        int padding,
        int rows,
        int columns,
        int texSize,
        bool saveAtlasToDisk = true
    )
    {
        if (combinedTextureDataList.Count == 0)
        {
            Debug.LogError("Simple Atlas Generator: No normal textures to atlas.");
            return null;
        }

        int atlasWidth = columns * (texSize + padding * 2);
        int atlasHeight = rows * (texSize + padding * 2);

        Debug.Log($"Simple Atlas Generator: Creating normal atlas with {columns} columns and {rows} rows. Atlas size: {atlasWidth}x{atlasHeight}.");

        Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
        atlas.name = atlasName + "_NormalAtlas";

        // Fill with flat normal color
        Color flatNormalColor = new Color(0.5f, 0.5f, 1f, 1f);
        Color[] clearColors = Enumerable.Repeat(flatNormalColor, atlasWidth * atlasHeight).ToArray();
        atlas.SetPixels(clearColors);

        for (int i = 0; i < combinedTextureDataList.Count; i++)
        {
            CombinedTextureData ctd = combinedTextureDataList[i];
            Texture2D normalTex = ctd.normalTexture;

            if (normalTex == null) continue; // skip if no normal

            EnsureTextureIsReadable(normalTex);
            Texture2D resizedTex = ResizeTexture(normalTex, texSize, texSize, true);

            int row = i / columns;
            int col = i % columns;

            int x = col * (texSize + padding * 2) + padding;
            int y = row * (texSize + padding * 2) + padding;

            Color[] normalPixels = resizedTex.GetPixels();
            atlas.SetPixels(x, y, texSize, texSize, normalPixels);
        }

        atlas.Apply();

        if (saveAtlasToDisk)
        {
            // Save atlas as PNG
            string atlasPath = GetUniqueAssetPath(normalTexturesPath, atlas.name, ".png");
            byte[] atlasBytes = atlas.EncodeToPNG();
            File.WriteAllBytes(atlasPath, atlasBytes);
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
            Texture2D importedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

            // Configure importer for normal map
            TextureImporter atlasImporter = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
            if (atlasImporter != null)
            {
                atlasImporter.isReadable = true;
                atlasImporter.textureCompression = TextureImporterCompression.Uncompressed;
                atlasImporter.textureType = TextureImporterType.NormalMap;
                atlasImporter.wrapMode = TextureWrapMode.Clamp;
                atlasImporter.filterMode = FilterMode.Bilinear;
                atlasImporter.mipmapEnabled = false;
                atlasImporter.sRGBTexture = false; // Use linear for normal maps
                atlasImporter.SaveAndReimport();

                return importedAtlas;
            }
            return importedAtlas; // fallback if importer is null
        }
        else
        {
            return atlas;
        }
    }

    private Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight, bool isNormalMap)
    {
        // Create a temporary RenderTexture of the desired size
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(newWidth, newHeight, RenderTextureFormat.ARGB32, 0)
        {
            sRGB = !isNormalMap // For normal maps, use linear
        };
        RenderTexture rt = RenderTexture.GetTemporary(descriptor);
        rt.filterMode = FilterMode.Bilinear;

        // Blit the source texture onto the RenderTexture
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);

        // Read back into a new Texture2D
        Texture2D resized = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false, !isNormalMap);
        resized.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        resized.Apply(false);

        // Release temp
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return resized;
    }

    /// <summary>
    /// Generates a flat normal map (points upwards).
    /// </summary>
    private Texture2D GenerateFlatNormalMap(int texSize)
    {
        Texture2D flatNormal = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        Color32 flatColor = new Color32(128, 128, 255, 255); // Represents a flat normal

        Color32[] pixels = new Color32[texSize * texSize];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = flatColor;
        }

        flatNormal.SetPixels32(pixels);
        flatNormal.Apply();
        flatNormal.name = "FlatNormal";

        // Save the flat normal texture
        string folderPath = Path.Combine("Assets", rootFolderName, texturesFolderName, "Normals");
        CreateFolderIfNotExists(folderPath);
        string flatNormalPath = Path.Combine(folderPath, "FlatNormal.png");

        byte[] bytes = flatNormal.EncodeToPNG();
        File.WriteAllBytes(flatNormalPath, bytes);
        AssetDatabase.ImportAsset(flatNormalPath, ImportAssetOptions.ForceUpdate);
        Texture2D importedFlatNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(flatNormalPath);

        // Ensure it's marked as a normal map
        TextureImporter importer = AssetImporter.GetAtPath(flatNormalPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
            Debug.Log($"Simple Atlas Generator: Saved flat normal map at '{flatNormalPath}'.");
        }

        return importedFlatNormal;
    }
}
