using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class UnrealCollisionPostprocessor : AssetPostprocessor
{
    // --- 1. Tuontivaiheen automaattinen käsittely uusille FBX-tiedostoille ---
    private void OnPostprocessModel(GameObject root)
    {
        ProcessModelColliders(root.transform);
    }

    private void ProcessModelColliders(Transform target)
    {
        if (target.name.StartsWith("UCX_", System.StringComparison.OrdinalIgnoreCase))
        {
            MeshFilter meshFilter = target.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                MeshRenderer meshRenderer = target.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    Object.DestroyImmediate(meshRenderer);
                }

                MeshCollider meshCollider = target.GetComponent<MeshCollider>();
                if (meshCollider == null)
                {
                    meshCollider = target.gameObject.AddComponent<MeshCollider>();
                }

                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = true;
            }
        }

        for (int i = 0; i < target.childCount; i++)
        {
            ProcessModelColliders(target.GetChild(i));
        }
    }

    // --- 2. Työkalu skenessä jo olevien objektien massamuunnokseen ---
    [MenuItem("Tools/Convert UCX Colliders in Active Scene")]
    public static void ConvertUCXInActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = activeScene.GetRootGameObjects();

        int processedCount = 0;

        foreach (GameObject root in rootObjects)
        {
            processedCount += ProcessSceneObjectRecursive(root.transform);
        }

        if (processedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log($"UCX conversion complete: Processed {processedCount} collider objects in '{activeScene.name}'.");
        }
        else
        {
            Debug.Log("No UCX objects found in the active scene.");
        }
    }

    private static int ProcessSceneObjectRecursive(Transform target)
    {
        int count = 0;

        if (target.name.StartsWith("UCX_", System.StringComparison.OrdinalIgnoreCase))
        {
            MeshFilter meshFilter = target.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                // Rekisteröidään GameObject kumoamista varten (Undo)
                Undo.RegisterCompleteObjectUndo(target.gameObject, "Convert UCX Collider");

                MeshRenderer meshRenderer = target.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    Undo.DestroyObjectImmediate(meshRenderer);
                }

                MeshCollider meshCollider = target.GetComponent<MeshCollider>();
                if (meshCollider == null)
                {
                    meshCollider = Undo.AddComponent<MeshCollider>(target.gameObject);
                }

                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = true;

                count++;
            }
        }

        for (int i = 0; i < target.childCount; i++)
        {
            count += ProcessSceneObjectRecursive(target.GetChild(i));
        }

        return count;
    }
}