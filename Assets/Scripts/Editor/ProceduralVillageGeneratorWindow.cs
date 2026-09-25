using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Placement layout strategy for village buildings.
    /// </summary>
    public enum VillageLayoutMode
    {
        /// <summary>Places houses in a circular/radial formation facing a central village square.</summary>
        RadialRingAroundCenter,

        /// <summary>Places houses along both sides of a defined waypoint path facing inward.</summary>
        AlongPredefinedPath
    }

    /// <summary>
    /// Procedural Stylized Village Generator for Unity Editor.
    /// Snaps prefabs to terrain using downward Physics.Raycast,
    /// places houses facing the village center while preventing collisions via Physics.OverlapBox,
    /// and populates surrounding terrain with tree prefabs using Poisson Disk Sampling.
    /// </summary>
    public class ProceduralVillageGeneratorWindow : EditorWindow
    {
        #region Menu Item

        [MenuItem("Tools/Procedural Village Generator", false, 10)]
        [MenuItem("CastleOfDice/Procedural Village Generator", false, 15)]
        public static void ShowWindow()
        {
            var window = GetWindow<ProceduralVillageGeneratorWindow>("Village Generator");
            window.minSize = new Vector2(440, 680);
            window.Show();
        }

        #endregion

        #region Serialized Settings & State

        [Header("Prefab Libraries")]
        [SerializeField] private List<GameObject> housePrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> propPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> treePrefabs = new List<GameObject>();
        [SerializeField] private GameObject centerLandmarkPrefab;

        [Header("Ground Snapping (Physics.Raycast)")]
        [SerializeField] private LayerMask groundLayerMask = ~0;
        [SerializeField] private float raycastStartHeight = 250f;
        [SerializeField] private float raycastMaxDistance = 500f;
        [SerializeField] private float groundSurfaceOffset = 0f;
        [SerializeField] private bool alignWithGroundNormal = false;
        [SerializeField] private float maxNormalTiltAngle = 18f;

        [Header("Village Center & Layout")]
        [SerializeField] private Vector3 villageCenter = Vector3.zero;
        [SerializeField] private Transform villageCenterTransform;
        [SerializeField] private VillageLayoutMode layoutMode = VillageLayoutMode.RadialRingAroundCenter;
        [SerializeField] private float villageRadius = 26f;
        [SerializeField] private float radiusJitter = 4f;
        [SerializeField] private int targetHouseCount = 12;
        [SerializeField] private int maxHouseAttempts = 100;
        [SerializeField] private Vector3 houseOverlapBoxHalfExtents = new Vector3(4.5f, 4f, 4.5f);
        [SerializeField] private LayerMask obstacleLayerMask = ~0;

        [Header("Path Settings (For AlongPredefinedPath Mode)")]
        [SerializeField] private List<Vector3> pathWaypoints = new List<Vector3>();
        [SerializeField] private float pathSpacing = 14f;
        [SerializeField] private float pathLateralOffset = 9f;

        [Header("Props & Village Center")]
        [SerializeField] private bool placeCenterLandmark = true;
        [SerializeField] private bool placeHouseProps = true;
        [SerializeField] private int propsPerHouse = 2;
        [SerializeField] private float propScatterRadius = 5.5f;
        [SerializeField] private Vector3 propOverlapBoxHalfExtents = new Vector3(1f, 1.5f, 1f);

        [Header("Tree Population (Poisson Disk Sampling)")]
        [SerializeField] private bool populateTrees = true;
        [SerializeField] private Vector2 treeAreaSize = new Vector2(160f, 160f);
        [SerializeField] private float treeMinDistance = 6.5f;
        [SerializeField] private int treeRejectionSamples = 30;
        [SerializeField] private float villageExclusionRadius = 34f;
        [SerializeField] private float houseExclusionRadius = 8.5f;
        [SerializeField] private float maxTreeSlopeAngle = 42f;
        [SerializeField] private Vector2 treeScaleRange = new Vector2(0.85f, 1.25f);
        [SerializeField] private Vector3 treeOverlapBoxHalfExtents = new Vector3(1.2f, 3.5f, 1.2f);

        [Header("Hierarchy & Output")]
        [SerializeField] private string rootContainerName = "Procedural_Village";
        [SerializeField] private bool showGizmosInSceneView = true;

        #endregion

        #region Private UI State

        private Vector2 scrollPosition;
        private bool showPrefabsFoldout = true;
        private bool showRaycastFoldout = true;
        private bool showHousesFoldout = true;
        private bool showPropsFoldout = true;
        private bool showTreesFoldout = true;
        private bool showGizmosFoldout = false;

        private readonly List<Vector3> placedHousePositions = new List<Vector3>();
        private readonly List<GameObject> placedCollidersToClean = new List<GameObject>();

        #endregion

        #region Unity Lifecycle & Scene GUI

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;

            // Auto-detect center if existing village or player exists
            if (villageCenter == Vector3.zero)
            {
                var villageLayout = GameObject.Find("Village_Layout") ?? GameObject.Find("Village");
                if (villageLayout != null)
                {
                    villageCenter = villageLayout.transform.position;
                }
            }

            // Auto-populate default path waypoints if empty
            if (pathWaypoints == null || pathWaypoints.Count == 0)
            {
                pathWaypoints = new List<Vector3>
                {
                    villageCenter + new Vector3(-35f, 0f, 0f),
                    villageCenter + new Vector3(-15f, 0f, 0f),
                    villageCenter + new Vector3(0f, 0f, 0f),
                    villageCenter + new Vector3(20f, 0f, 5f),
                    villageCenter + new Vector3(40f, 0f, 10f)
                };
            }
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        #endregion

        #region GUI Rendering

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeaderGUI();
            DrawPrefabsSection();
            DrawRaycastSection();
            DrawHouseSection();
            DrawPropsSection();
            DrawTreeSection();
            DrawActionsSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeaderGUI()
        {
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter
                };
                EditorGUILayout.LabelField("Procedural Stylized Village Generator", titleStyle);
                EditorGUILayout.LabelField(
                    "Generates an organic village with ground raycast snapping, " +
                    "collision-safe house placement facing center, and Poisson Disk tree distribution.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.Space(4);
        }

        private void DrawPrefabsSection()
        {
            showPrefabsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showPrefabsFoldout, "1. Prefab Library Configuration");
            if (showPrefabsFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.HelpBox("Assign prefabs for houses, props, trees, and center landmark. You can also use the auto-assign button below to search Assets/PREFABS.", MessageType.None);

                    DrawPrefabList("House Prefabs", housePrefabs);
                    DrawPrefabList("Prop Prefabs", propPrefabs);
                    DrawPrefabList("Tree Prefabs", treePrefabs);

                    centerLandmarkPrefab = (GameObject)EditorGUILayout.ObjectField(
                        new GUIContent("Center Landmark (Well/Altar)", "Prefab placed at the center of the village square."),
                        centerLandmarkPrefab, typeof(GameObject), false);

                    EditorGUILayout.Space(4);
                    if (GUILayout.Button("Auto-Assign Prefabs from Assets/PREFABS", GUILayout.Height(26)))
                    {
                        AutoAssignPrefabsFromProject();
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4);
        }

        private void DrawRaycastSection()
        {
            showRaycastFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showRaycastFoldout, "2. Ground Snapping (Physics.Raycast)");
            if (showRaycastFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    groundLayerMask = DrawLayerMaskField(
                        new GUIContent("Ground Layer Mask", "Layers evaluated by downward raycasting to identify the terrain or ground plane."),
                        groundLayerMask);

                    raycastStartHeight = EditorGUILayout.FloatField(
                        new GUIContent("Raycast Start Height (Y)", "Height from which downward raycasts are cast."),
                        raycastStartHeight);

                    raycastMaxDistance = EditorGUILayout.FloatField(
                        new GUIContent("Raycast Max Distance", "Maximum distance raycast searches downward for ground contact."),
                        raycastMaxDistance);

                    groundSurfaceOffset = EditorGUILayout.FloatField(
                        new GUIContent("Surface Height Offset", "Slight vertical offset above the hit surface (prevents z-fighting)."),
                        groundSurfaceOffset);

                    alignWithGroundNormal = EditorGUILayout.Toggle(
                        new GUIContent("Align With Terrain Normal", "Whether prefabs tilt slightly to match terrain slope."),
                        alignWithGroundNormal);

                    if (alignWithGroundNormal)
                    {
                        maxNormalTiltAngle = EditorGUILayout.Slider(
                            new GUIContent("Max Tilt Angle", "Maximum allowed tilt from vertical up direction."),
                            maxNormalTiltAngle, 0f, 45f);
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4);
        }

        private void DrawHouseSection()
        {
            showHousesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showHousesFoldout, "3. Village Center & House Placement");
            if (showHousesFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    villageCenterTransform = (Transform)EditorGUILayout.ObjectField(
                        new GUIContent("Center Transform (Optional)", "Transform used as the village center. If empty, Vector3 coordinates below are used."),
                        villageCenterTransform, typeof(Transform), true);

                    if (villageCenterTransform != null)
                    {
                        villageCenter = villageCenterTransform.position;
                    }
                    else
                    {
                        villageCenter = EditorGUILayout.Vector3Field("Village Center Coordinates", villageCenter);
                    }

                    layoutMode = (VillageLayoutMode)EditorGUILayout.EnumPopup(
                        new GUIContent("Village Layout Mode", "Algorithm used to position houses around the village."),
                        layoutMode);

                    if (layoutMode == VillageLayoutMode.RadialRingAroundCenter)
                    {
                        villageRadius = EditorGUILayout.FloatField(
                            new GUIContent("Village Ring Radius", "Base distance from center to house ring."),
                            villageRadius);

                        radiusJitter = EditorGUILayout.FloatField(
                            new GUIContent("Radius Jitter (+/-)", "Random variance added to radius for an organic look."),
                            radiusJitter);

                        targetHouseCount = EditorGUILayout.IntSlider(
                            new GUIContent("Target House Count", "Number of houses to attempt placing."),
                            targetHouseCount, 1, 40);
                    }
                    else
                    {
                        pathSpacing = EditorGUILayout.FloatField(
                            new GUIContent("Path House Spacing", "Distance along the path between house candidates."),
                            pathSpacing);

                        pathLateralOffset = EditorGUILayout.FloatField(
                            new GUIContent("Path Lateral Offset", "Distance away from path center line to place houses on left/right sides."),
                            pathLateralOffset);

                        DrawPathWaypointsList();
                    }

                    maxHouseAttempts = EditorGUILayout.IntSlider(
                        new GUIContent("Max Placement Attempts", "Safety limit on candidate tries to avoid infinite loops."),
                        maxHouseAttempts, 20, 300);

                    houseOverlapBoxHalfExtents = EditorGUILayout.Vector3Field(
                        new GUIContent("OverlapBox Half-Extents", "Dimensions tested via Physics.OverlapBox to prevent house overlapping."),
                        houseOverlapBoxHalfExtents);

                    obstacleLayerMask = DrawLayerMaskField(
                        new GUIContent("Obstacle Layer Mask", "Layers checked for building and prop collisions (excluding terrain)."),
                        obstacleLayerMask);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4);
        }

        private void DrawPropsSection()
        {
            showPropsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showPropsFoldout, "4. Props & Central Landmark");
            if (showPropsFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    placeCenterLandmark = EditorGUILayout.Toggle(
                        new GUIContent("Place Center Landmark", "Spawns center landmark prefab (e.g. Well) at village center snapped to ground."),
                        placeCenterLandmark);

                    placeHouseProps = EditorGUILayout.Toggle(
                        new GUIContent("Scatter House Props", "Scatters barrels, crates, and lanterns around placed houses."),
                        placeHouseProps);

                    if (placeHouseProps)
                    {
                        propsPerHouse = EditorGUILayout.IntSlider(
                            new GUIContent("Props Per House", "Number of decorative props to place near each house."),
                            propsPerHouse, 1, 6);

                        propScatterRadius = EditorGUILayout.FloatField(
                            new GUIContent("Prop Scatter Radius", "Distance range from house perimeter to place props."),
                            propScatterRadius);

                        propOverlapBoxHalfExtents = EditorGUILayout.Vector3Field(
                            new GUIContent("Prop OverlapBox Half-Extents", "Volume tested via Physics.OverlapBox for prop placement clearance."),
                            propOverlapBoxHalfExtents);
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4);
        }

        private void DrawTreeSection()
        {
            showTreesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showTreesFoldout, "5. Tree Population (Poisson Disk Sampling)");
            if (showTreesFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    populateTrees = EditorGUILayout.Toggle(
                        new GUIContent("Populate Trees", "Fills empty terrain surrounding the village with trees using Poisson Disk Sampling."),
                        populateTrees);

                    if (populateTrees)
                    {
                        treeAreaSize = EditorGUILayout.Vector2Field(
                            new GUIContent("Tree Boundary Size (X, Z)", "Total rectangular dimensions centered on village for tree generation."),
                            treeAreaSize);

                        treeMinDistance = EditorGUILayout.Slider(
                            new GUIContent("Minimum Tree Distance (r)", "Minimum spacing between adjacent tree trunks."),
                            treeMinDistance, 3f, 25f);

                        treeRejectionSamples = EditorGUILayout.IntSlider(
                            new GUIContent("Sample Limit (k)", "Number of candidate samples before giving up on a point."),
                            treeRejectionSamples, 10, 50);

                        villageExclusionRadius = EditorGUILayout.FloatField(
                            new GUIContent("Village Center Exclusion", "Circular radius around village center where no trees can spawn."),
                            villageExclusionRadius);

                        houseExclusionRadius = EditorGUILayout.FloatField(
                            new GUIContent("House Exclusion Radius", "Clearance radius around every placed house where trees are disallowed."),
                            houseExclusionRadius);

                        maxTreeSlopeAngle = EditorGUILayout.Slider(
                            new GUIContent("Max Terrain Slope Angle", "Maximum slope angle steepness where trees can take root."),
                            maxTreeSlopeAngle, 10f, 60f);

                        treeScaleRange = EditorGUILayout.Vector2Field(
                            new GUIContent("Tree Scale Range (Min, Max)", "Random uniform scaling applied to instantiated trees."),
                            treeScaleRange);

                        treeOverlapBoxHalfExtents = EditorGUILayout.Vector3Field(
                            new GUIContent("Tree OverlapBox Half-Extents", "Volume tested via Physics.OverlapBox before planting tree."),
                            treeOverlapBoxHalfExtents);
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4);
        }

        private void DrawActionsSection()
        {
            showGizmosFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showGizmosFoldout, "6. Scene View Gizmos & Output");
            if (showGizmosFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    rootContainerName = EditorGUILayout.TextField("Root Container Name", rootContainerName);
                    showGizmosInSceneView = EditorGUILayout.Toggle("Show SceneView Gizmos", showGizmosInSceneView);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(12);

            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
            if (GUILayout.Button("★ Generate Procedural Village ★", GUILayout.Height(42)))
            {
                GenerateVillage();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("Clear Generated Village", GUILayout.Height(30)))
            {
                ClearGeneratedVillage();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(12);
        }

        private void DrawPrefabList(string label, List<GameObject> list)
        {
            EditorGUILayout.LabelField($"{label} ({list.Count})", EditorStyles.boldLabel);
            int removeIndex = -1;

            for (int i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    list[i] = (GameObject)EditorGUILayout.ObjectField($"Prefab #{i + 1}", list[i], typeof(GameObject), false);
                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        removeIndex = i;
                    }
                }
            }

            if (removeIndex >= 0)
            {
                list.RemoveAt(removeIndex);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"+ Add {label.TrimEnd('s')}", GUILayout.Height(20)))
                {
                    list.Add(null);
                }
                if (list.Count > 0 && GUILayout.Button("Clear List", GUILayout.Width(80)))
                {
                    list.Clear();
                }
            }
            EditorGUILayout.Space(4);
        }

        private void DrawPathWaypointsList()
        {
            EditorGUILayout.LabelField($"Path Waypoints ({pathWaypoints.Count})", EditorStyles.boldLabel);
            int removeIndex = -1;

            for (int i = 0; i < pathWaypoints.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    pathWaypoints[i] = EditorGUILayout.Vector3Field($"Waypoint {i + 1}", pathWaypoints[i]);
                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        removeIndex = i;
                    }
                }
            }

            if (removeIndex >= 0)
            {
                pathWaypoints.RemoveAt(removeIndex);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add Waypoint", GUILayout.Height(20)))
                {
                    Vector3 nextPos = pathWaypoints.Count > 0 ? pathWaypoints[^1] + new Vector3(15f, 0f, 0f) : villageCenter;
                    pathWaypoints.Add(nextPos);
                }
                if (GUILayout.Button("Reset To Default Line", GUILayout.Width(140)))
                {
                    pathWaypoints = new List<Vector3>
                    {
                        villageCenter + new Vector3(-35f, 0f, 0f),
                        villageCenter + new Vector3(-15f, 0f, 0f),
                        villageCenter + new Vector3(0f, 0f, 0f),
                        villageCenter + new Vector3(20f, 0f, 5f),
                        villageCenter + new Vector3(40f, 0f, 10f)
                    };
                }
            }
            EditorGUILayout.Space(4);
        }

        private LayerMask DrawLayerMaskField(GUIContent label, LayerMask layerMask)
        {
            var layers = new List<string>();
            var layerNumbers = new List<int>();

            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    layers.Add(layerName);
                    layerNumbers.Add(i);
                }
            }

            int maskWithoutEmpty = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if (((1 << layerNumbers[i]) & layerMask.value) > 0)
                {
                    maskWithoutEmpty |= 1 << i;
                }
            }

            maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());

            int mask = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if ((maskWithoutEmpty & (1 << i)) > 0)
                {
                    mask |= 1 << layerNumbers[i];
                }
            }

            layerMask.value = mask;
            return layerMask;
        }

        #endregion

        #region Auto-Detection Utilities

        public void AutoAssignPrefabsFromProject()
        {
            housePrefabs.Clear();
            treePrefabs.Clear();
            propPrefabs.Clear();

            string[] houseNames = { "Baker_house", "Small_house", "Large_house", "Tavern", "Town_Hall", "Witch_house" };
            foreach (string hName in houseNames)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/PREFABS/{hName}.prefab");
                if (prefab != null && !housePrefabs.Contains(prefab))
                {
                    housePrefabs.Add(prefab);
                }
            }

            string[] treeNames = { "Tree_1", "Tree_2", "Tree_4", "Pine_tree", "Pine_tree_2", "Pine_tree_2_1" };
            foreach (string tName in treeNames)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/PREFABS/{tName}.prefab");
                if (prefab != null && !treePrefabs.Contains(prefab))
                {
                    treePrefabs.Add(prefab);
                }
            }

            string[] propNames = { "Barrel", "Box_1", "Box_2", "Chest", "Lamppost", "street_light", "Log", "Rock_1", "Rock_2", "Fence" };
            foreach (string pName in propNames)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/PREFABS/{pName}.prefab");
                if (prefab != null && !propPrefabs.Contains(prefab))
                {
                    propPrefabs.Add(prefab);
                }
            }

            centerLandmarkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Well.prefab")
                                ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Altar.prefab");

            EditorUtility.SetDirty(this);
            Debug.Log($"[VillageGenerator] Auto-assigned {housePrefabs.Count} house prefabs, {propPrefabs.Count} prop prefabs, and {treePrefabs.Count} tree prefabs from Assets/PREFABS.");
        }

        #endregion

        #region Procedural Generation Core

        public void GenerateVillage()
        {
            if (villageCenterTransform != null)
            {
                villageCenter = villageCenterTransform.position;
            }

            if (housePrefabs == null || housePrefabs.Count == 0 || housePrefabs.All(p => p == null))
            {
                EditorUtility.DisplayDialog("Missing Prefabs", "Please assign at least one house prefab before generating.", "OK");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Generating Stylized Village", "Preparing scene hierarchy...", 0.05f);

                // 1. Create or clean root GameObject
                GameObject root = GameObject.Find(rootContainerName);
                if (root != null)
                {
                    Undo.DestroyObjectImmediate(root);
                }

                root = new GameObject(rootContainerName);
                Undo.RegisterCreatedObjectUndo(root, "Generate Procedural Village");

                GameObject housesContainer = new GameObject("Houses");
                housesContainer.transform.SetParent(root.transform, false);

                GameObject propsContainer = new GameObject("Props");
                propsContainer.transform.SetParent(root.transform, false);

                GameObject treesContainer = new GameObject("Trees");
                treesContainer.transform.SetParent(root.transform, false);

                placedHousePositions.Clear();
                placedCollidersToClean.Clear();

                // 2. Center Landmark Placement
                if (placeCenterLandmark && centerLandmarkPrefab != null)
                {
                    EditorUtility.DisplayProgressBar("Generating Stylized Village", "Placing central plaza landmark...", 0.15f);
                    PlaceCenterLandmark(propsContainer.transform);
                }

                // 3. House Placement (Physics.Raycast downward + Physics.OverlapBox overlap prevention)
                EditorUtility.DisplayProgressBar("Generating Stylized Village", "Placing village houses along layout...", 0.35f);
                if (layoutMode == VillageLayoutMode.RadialRingAroundCenter)
                {
                    PlaceHousesRadial(housesContainer.transform);
                }
                else
                {
                    PlaceHousesAlongPath(housesContainer.transform);
                }

                // 4. Props Scatter Around Houses
                if (placeHouseProps && propPrefabs != null && propPrefabs.Count > 0)
                {
                    EditorUtility.DisplayProgressBar("Generating Stylized Village", "Scattering village props...", 0.60f);
                    PlacePropsAroundHouses(propsContainer.transform);
                }

                // 5. Tree Population (Poisson Disk Sampling)
                if (populateTrees && treePrefabs != null && treePrefabs.Count > 0)
                {
                    EditorUtility.DisplayProgressBar("Generating Stylized Village", "Populating trees with Poisson Disk Sampling...", 0.80f);
                    PopulateTreesPoisson(treesContainer.transform);
                }

                // Ensure clean transforms synchronization in PhysX
                Physics.SyncTransforms();

                Debug.Log($"[VillageGenerator] Successfully generated village '{rootContainerName}': " +
                          $"{placedHousePositions.Count} houses placed at center {villageCenter}.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public void ClearGeneratedVillage()
        {
            GameObject root = GameObject.Find(rootContainerName);
            if (root != null)
            {
                Undo.DestroyObjectImmediate(root);
                placedHousePositions.Clear();
                Debug.Log($"[VillageGenerator] Removed procedural village container '{rootContainerName}'.");
            }
            else
            {
                Debug.LogWarning($"[VillageGenerator] No GameObject named '{rootContainerName}' found in scene.");
            }
        }

        #endregion

        #region Step 1: Center Landmark Placement

        private void PlaceCenterLandmark(Transform parent)
        {
            if (centerLandmarkPrefab == null) return;

            if (TrySnapToGround(villageCenter, out Vector3 snappedPos, out Vector3 groundNormal))
            {
                Quaternion rotation = Quaternion.identity;
                if (alignWithGroundNormal)
                {
                    rotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
                }

                GameObject landmarkObj = (GameObject)PrefabUtility.InstantiatePrefab(centerLandmarkPrefab, parent);
                if (landmarkObj != null)
                {
                    Undo.RegisterCreatedObjectUndo(landmarkObj, "Create Center Landmark");
                    landmarkObj.transform.position = snappedPos;
                    landmarkObj.transform.rotation = rotation;
                    EnsureColliderOnObject(landmarkObj, new Vector3(2f, 2f, 2f));
                    Physics.SyncTransforms();
                }
            }
        }

        #endregion

        #region Step 2: House Placement Core (Physics.OverlapBox + Center Facing)

        private void PlaceHousesRadial(Transform parent)
        {
            var validPrefabs = housePrefabs.Where(p => p != null).ToList();
            if (validPrefabs.Count == 0) return;

            int placedCount = 0;
            float angleStep = 360f / Mathf.Max(1, targetHouseCount);

            for (int i = 0; i < maxHouseAttempts && placedCount < targetHouseCount; i++)
            {
                float baseAngle = placedCount * angleStep;
                float angleJitter = UnityEngine.Random.Range(-angleStep * 0.35f, angleStep * 0.35f);
                float angle = (baseAngle + angleJitter + (i * 13.7f)) * Mathf.Deg2Rad;

                float r = villageRadius + UnityEngine.Random.Range(-radiusJitter, radiusJitter);
                Vector3 candidateHorizontalPos = villageCenter + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

                // Downward raycast to snap to terrain surface
                if (!TrySnapToGround(candidateHorizontalPos, out Vector3 snappedPos, out Vector3 groundNormal))
                {
                    continue;
                }

                // Forward vector must point directly toward village center (projected horizontally)
                Vector3 toCenter = (villageCenter - snappedPos);
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude < 0.001f)
                {
                    continue;
                }

                Quaternion houseRotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
                if (alignWithGroundNormal)
                {
                    Quaternion normalTilt = Quaternion.FromToRotation(Vector3.up, groundNormal);
                    if (Quaternion.Angle(Quaternion.identity, normalTilt) <= maxNormalTiltAngle)
                    {
                        houseRotation = normalTilt * houseRotation;
                    }
                }

                // Check clearance with Physics.OverlapBox to prevent overlap
                Vector3 overlapBoxCenter = snappedPos + Vector3.up * houseOverlapBoxHalfExtents.y;
                if (IsPositionBlockedByOverlapBox(overlapBoxCenter, houseOverlapBoxHalfExtents, houseRotation))
                {
                    continue;
                }

                // Instantiate prefab
                var prefabToSpawn = validPrefabs[UnityEngine.Random.Range(0, validPrefabs.Count)];
                GameObject houseObj = (GameObject)PrefabUtility.InstantiatePrefab(prefabToSpawn, parent);
                if (houseObj != null)
                {
                    Undo.RegisterCreatedObjectUndo(houseObj, "Place House");
                    houseObj.transform.position = snappedPos;
                    houseObj.transform.rotation = houseRotation;

                    // Ensure collider exists for subsequent OverlapBox checks
                    EnsureColliderOnObject(houseObj, houseOverlapBoxHalfExtents);
                    Physics.SyncTransforms();

                    placedHousePositions.Add(snappedPos);
                    placedCount++;
                }
            }
        }

        private void PlaceHousesAlongPath(Transform parent)
        {
            var validPrefabs = housePrefabs.Where(p => p != null).ToList();
            if (validPrefabs.Count == 0 || pathWaypoints.Count < 2) return;

            int placedCount = 0;

            for (int seg = 0; seg < pathWaypoints.Count - 1 && placedCount < targetHouseCount; seg++)
            {
                Vector3 pA = pathWaypoints[seg];
                Vector3 pB = pathWaypoints[seg + 1];
                Vector3 segmentDir = (pB - pA);
                segmentDir.y = 0f;
                float segLength = segmentDir.magnitude;
                if (segLength < 0.5f) continue;

                Vector3 forward = segmentDir.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                int steps = Mathf.Max(1, Mathf.FloorToInt(segLength / pathSpacing));
                for (int s = 0; s < steps && placedCount < targetHouseCount; s++)
                {
                    float t = (s + 0.5f) / steps;
                    Vector3 pathPoint = Vector3.Lerp(pA, pB, t);

                    // Try left side (-right) and right side (+right)
                    float[] sideMultipliers = { -1f, 1f };
                    foreach (float side in sideMultipliers)
                    {
                        if (placedCount >= targetHouseCount) break;

                        float lateral = (pathLateralOffset + UnityEngine.Random.Range(-1.5f, 1.5f)) * side;
                        Vector3 candidatePos = pathPoint + right * lateral;

                        if (!TrySnapToGround(candidatePos, out Vector3 snappedPos, out Vector3 groundNormal))
                        {
                            continue;
                        }

                        // Forward vector points toward village center
                        Vector3 toCenter = (villageCenter - snappedPos);
                        toCenter.y = 0f;
                        if (toCenter.sqrMagnitude < 0.001f) continue;

                        Quaternion houseRotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);

                        Vector3 overlapCenter = snappedPos + Vector3.up * houseOverlapBoxHalfExtents.y;
                        if (IsPositionBlockedByOverlapBox(overlapCenter, houseOverlapBoxHalfExtents, houseRotation))
                        {
                            continue;
                        }

                        var prefabToSpawn = validPrefabs[UnityEngine.Random.Range(0, validPrefabs.Count)];
                        GameObject houseObj = (GameObject)PrefabUtility.InstantiatePrefab(prefabToSpawn, parent);
                        if (houseObj != null)
                        {
                            Undo.RegisterCreatedObjectUndo(houseObj, "Place House On Path");
                            houseObj.transform.position = snappedPos;
                            houseObj.transform.rotation = houseRotation;

                            EnsureColliderOnObject(houseObj, houseOverlapBoxHalfExtents);
                            Physics.SyncTransforms();

                            placedHousePositions.Add(snappedPos);
                            placedCount++;
                        }
                    }
                }
            }
        }

        #endregion

        #region Step 3: Props Placement

        private void PlacePropsAroundHouses(Transform parent)
        {
            var validProps = propPrefabs.Where(p => p != null).ToList();
            if (validProps.Count == 0 || placedHousePositions.Count == 0) return;

            foreach (Vector3 housePos in placedHousePositions)
            {
                int propsPlaced = 0;
                for (int attempt = 0; attempt < propsPerHouse * 4 && propsPlaced < propsPerHouse; attempt++)
                {
                    float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    float dist = UnityEngine.Random.Range(propScatterRadius * 0.7f, propScatterRadius * 1.3f);
                    Vector3 candidatePos = housePos + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                    if (!TrySnapToGround(candidatePos, out Vector3 snappedPos, out Vector3 groundNormal))
                    {
                        continue;
                    }

                    Quaternion propRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                    Vector3 overlapCenter = snappedPos + Vector3.up * propOverlapBoxHalfExtents.y;

                    if (IsPositionBlockedByOverlapBox(overlapCenter, propOverlapBoxHalfExtents, propRotation))
                    {
                        continue;
                    }

                    var selectedProp = validProps[UnityEngine.Random.Range(0, validProps.Count)];
                    GameObject propObj = (GameObject)PrefabUtility.InstantiatePrefab(selectedProp, parent);
                    if (propObj != null)
                    {
                        Undo.RegisterCreatedObjectUndo(propObj, "Place Village Prop");
                        propObj.transform.position = snappedPos;
                        propObj.transform.rotation = propRotation;

                        EnsureColliderOnObject(propObj, propOverlapBoxHalfExtents);
                        Physics.SyncTransforms();

                        propsPlaced++;
                    }
                }
            }
        }

        #endregion

        #region Step 4: Poisson Disk Tree Population

        private void PopulateTreesPoisson(Transform parent)
        {
            var validTrees = treePrefabs.Where(p => p != null).ToList();
            if (validTrees.Count == 0) return;

            // Generate 2D Poisson Disk samples
            var sampler = new PoissonDiskSampler2D(treeAreaSize.x, treeAreaSize.y, treeMinDistance, treeRejectionSamples);
            var samples = sampler.GenerateSamples();

            Vector3 areaOrigin = new Vector3(
                villageCenter.x - treeAreaSize.x * 0.5f,
                0f,
                villageCenter.z - treeAreaSize.y * 0.5f
            );

            foreach (Vector2 sample in samples)
            {
                Vector3 worldXZ = areaOrigin + new Vector3(sample.x, 0f, sample.y);

                // 1. Village Center Exclusion Check
                float distToCenter = Vector2.Distance(new Vector2(worldXZ.x, worldXZ.z), new Vector2(villageCenter.x, villageCenter.z));
                if (distToCenter < villageExclusionRadius)
                {
                    continue;
                }

                // 2. House Exclusion Check
                bool tooCloseToHouse = false;
                foreach (Vector3 housePos in placedHousePositions)
                {
                    float distToHouse = Vector2.Distance(new Vector2(worldXZ.x, worldXZ.z), new Vector2(housePos.x, housePos.z));
                    if (distToHouse < houseExclusionRadius)
                    {
                        tooCloseToHouse = true;
                        break;
                    }
                }
                if (tooCloseToHouse) continue;

                // 3. Downward Raycast Snap to Terrain
                if (!TrySnapToGround(worldXZ, out Vector3 snappedPos, out Vector3 groundNormal))
                {
                    continue;
                }

                // 4. Slope Angle Check
                float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
                if (slopeAngle > maxTreeSlopeAngle)
                {
                    continue;
                }

                // 5. OverlapBox Clearance Check
                Quaternion treeRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                Vector3 overlapCenter = snappedPos + Vector3.up * treeOverlapBoxHalfExtents.y;
                if (IsPositionBlockedByOverlapBox(overlapCenter, treeOverlapBoxHalfExtents, treeRotation))
                {
                    continue;
                }

                // 6. Instantiate Tree Prefab with randomized rotation and scale
                var selectedTreePrefab = validTrees[UnityEngine.Random.Range(0, validTrees.Count)];
                GameObject treeObj = (GameObject)PrefabUtility.InstantiatePrefab(selectedTreePrefab, parent);
                if (treeObj != null)
                {
                    Undo.RegisterCreatedObjectUndo(treeObj, "Plant Tree");
                    treeObj.transform.position = snappedPos;
                    treeObj.transform.rotation = treeRotation;

                    float uniformScale = UnityEngine.Random.Range(treeScaleRange.x, treeScaleRange.y);
                    treeObj.transform.localScale = Vector3.one * uniformScale;

                    EnsureColliderOnObject(treeObj, treeOverlapBoxHalfExtents);
                    Physics.SyncTransforms();
                }
            }
        }

        #endregion

        #region Physics Helper Methods

        /// <summary>
        /// Snaps a horizontal position downward onto the terrain or ground collider via Physics.Raycast.
        /// </summary>
        private bool TrySnapToGround(Vector3 horizontalPosition, out Vector3 snappedPosition, out Vector3 surfaceNormal)
        {
            Vector3 rayOrigin = new Vector3(horizontalPosition.x, raycastStartHeight, horizontalPosition.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastMaxDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                snappedPosition = hit.point + Vector3.up * groundSurfaceOffset;
                surfaceNormal = hit.normal;
                return true;
            }

            // Fallback to active terrain if available
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                float terrainY = terrain.SampleHeight(horizontalPosition) + terrain.transform.position.y;
                snappedPosition = new Vector3(horizontalPosition.x, terrainY + groundSurfaceOffset, horizontalPosition.z);
                surfaceNormal = Vector3.up;
                return true;
            }

            snappedPosition = horizontalPosition;
            surfaceNormal = Vector3.up;
            return false;
        }

        /// <summary>
        /// Verifies whether the specified bounding box collides with existing buildings or obstacles using Physics.OverlapBox.
        /// Automatically excludes ground colliders and terrain colliders.
        /// </summary>
        private bool IsPositionBlockedByOverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation)
        {
            Collider[] colliders = Physics.OverlapBox(center, halfExtents, orientation, obstacleLayerMask, QueryTriggerInteraction.Ignore);
            foreach (Collider col in colliders)
            {
                // Disregard terrain and pure ground planes
                if (col is TerrainCollider) continue;
                if ((1 << col.gameObject.layer) == groundLayerMask.value) continue;
                if (col.CompareTag("Terrain") || col.CompareTag("Ground")) continue;

                // Found an obstacle collider
                return true;
            }
            return false;
        }

        /// <summary>
        /// Ensures the instantiated GameObject has a Collider so subsequent Physics.OverlapBox tests detect it.
        /// </summary>
        private void EnsureColliderOnObject(GameObject go, Vector3 fallbackHalfExtents)
        {
            var existingCollider = go.GetComponentInChildren<Collider>();
            if (existingCollider == null)
            {
                var box = go.AddComponent<BoxCollider>();
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds combinedBounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        combinedBounds.Encapsulate(renderers[i].bounds);
                    }
                    box.center = go.transform.InverseTransformPoint(combinedBounds.center);
                    box.size = combinedBounds.size;
                }
                else
                {
                    box.size = fallbackHalfExtents * 2f;
                    box.center = Vector3.up * fallbackHalfExtents.y;
                }
            }
        }

        #endregion

        #region Scene View Gizmos Visualization

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!showGizmosInSceneView) return;

            Vector3 center = villageCenterTransform != null ? villageCenterTransform.position : villageCenter;

            // 1. Village Center Point & Plaza Radius
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Handles.DrawWireDisc(center, Vector3.up, 3f);
            Handles.Label(center + Vector3.up * 1.5f, "Village Center");

            // 2. Village House Radius Ring
            if (layoutMode == VillageLayoutMode.RadialRingAroundCenter)
            {
                Handles.color = new Color(1f, 0.85f, 0.2f, 0.6f);
                Handles.DrawWireDisc(center, Vector3.up, villageRadius - radiusJitter);
                Handles.DrawWireDisc(center, Vector3.up, villageRadius + radiusJitter);
                Handles.color = new Color(1f, 0.85f, 0.2f, 0.15f);
                Handles.DrawSolidDisc(center, Vector3.up, villageRadius);
            }
            else if (pathWaypoints != null && pathWaypoints.Count > 1)
            {
                Handles.color = new Color(0.9f, 0.6f, 0.2f, 0.8f);
                for (int i = 0; i < pathWaypoints.Count - 1; i++)
                {
                    Handles.DrawLine(pathWaypoints[i], pathWaypoints[i + 1]);
                    Handles.DrawSolidDisc(pathWaypoints[i], Vector3.up, 0.8f);
                }
                Handles.DrawSolidDisc(pathWaypoints[^1], Vector3.up, 0.8f);
            }

            // 3. Tree Population Boundary Box & Village Exclusion Disc
            if (populateTrees)
            {
                Handles.color = new Color(0.3f, 0.9f, 0.3f, 0.4f);
                Vector3 cornerA = center + new Vector3(-treeAreaSize.x * 0.5f, 0f, -treeAreaSize.y * 0.5f);
                Vector3 cornerB = center + new Vector3(treeAreaSize.x * 0.5f, 0f, -treeAreaSize.y * 0.5f);
                Vector3 cornerC = center + new Vector3(treeAreaSize.x * 0.5f, 0f, treeAreaSize.y * 0.5f);
                Vector3 cornerD = center + new Vector3(-treeAreaSize.x * 0.5f, 0f, treeAreaSize.y * 0.5f);

                Handles.DrawLine(cornerA, cornerB);
                Handles.DrawLine(cornerB, cornerC);
                Handles.DrawLine(cornerC, cornerD);
                Handles.DrawLine(cornerD, cornerA);

                // Tree village exclusion zone
                Handles.color = new Color(1f, 0.3f, 0.3f, 0.4f);
                Handles.DrawWireDisc(center, Vector3.up, villageExclusionRadius);
            }
        }

        #endregion
    }

    #region Poisson Disk Sampling Algorithm

    /// <summary>
    /// Fast 2D Poisson Disk Sampling based on Robert Bridson's algorithm.
    /// Distributes points uniformly with guaranteed minimum Euclidean distance.
    /// </summary>
    public class PoissonDiskSampler2D
    {
        private readonly float width;
        private readonly float height;
        private readonly float minDistance;
        private readonly int rejectionLimit;
        private readonly float cellSize;
        private readonly int gridWidth;
        private readonly int gridHeight;
        private readonly int[,] grid;
        private readonly List<Vector2> points;
        private readonly List<int> activeIndices;

        public PoissonDiskSampler2D(float width, float height, float minDistance, int rejectionLimit = 30)
        {
            this.width = Mathf.Max(1f, width);
            this.height = Mathf.Max(1f, height);
            this.minDistance = Mathf.Max(0.5f, minDistance);
            this.rejectionLimit = Mathf.Max(5, rejectionLimit);

            cellSize = minDistance / Mathf.Sqrt(2f);
            gridWidth = Mathf.CeilToInt(this.width / cellSize);
            gridHeight = Mathf.CeilToInt(this.height / cellSize);

            grid = new int[gridWidth, gridHeight];
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    grid[x, y] = -1;
                }
            }

            points = new List<Vector2>();
            activeIndices = new List<int>();
        }

        public List<Vector2> GenerateSamples()
        {
            // Initial seed point at center of the sampling bounds
            Vector2 initialPoint = new Vector2(width * 0.5f, height * 0.5f);
            AddPoint(initialPoint);

            while (activeIndices.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, activeIndices.Count);
                int pointIndex = activeIndices[randomIndex];
                Vector2 currentPoint = points[pointIndex];
                bool foundCandidate = false;

                for (int i = 0; i < rejectionLimit; i++)
                {
                    float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float distance = UnityEngine.Random.Range(minDistance, 2f * minDistance);
                    Vector2 candidate = currentPoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                    if (IsValidCandidate(candidate))
                    {
                        AddPoint(candidate);
                        foundCandidate = true;
                        break;
                    }
                }

                if (!foundCandidate)
                {
                    activeIndices.RemoveAt(randomIndex);
                }
            }

            return points;
        }

        private bool IsValidCandidate(Vector2 candidate)
        {
            if (candidate.x < 0f || candidate.x >= width || candidate.y < 0f || candidate.y >= height)
            {
                return false;
            }

            int cellX = (int)(candidate.x / cellSize);
            int cellY = (int)(candidate.y / cellSize);

            int startX = Mathf.Max(0, cellX - 2);
            int endX = Mathf.Min(gridWidth - 1, cellX + 2);
            int startY = Mathf.Max(0, cellY - 2);
            int endY = Mathf.Min(gridHeight - 1, cellY + 2);

            float sqrMinDist = minDistance * minDistance;

            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    int pointIndex = grid[x, y];
                    if (pointIndex != -1)
                    {
                        Vector2 neighbor = points[pointIndex];
                        if ((candidate - neighbor).sqrMagnitude < sqrMinDist)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private void AddPoint(Vector2 point)
        {
            int index = points.Count;
            points.Add(point);
            activeIndices.Add(index);

            int cellX = (int)(point.x / cellSize);
            int cellY = (int)(point.y / cellSize);

            if (cellX >= 0 && cellX < gridWidth && cellY >= 0 && cellY < gridHeight)
            {
                grid[cellX, cellY] = index;
            }
        }
    }

    #endregion
}
