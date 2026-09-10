using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class UtilityWindow : EditorWindow
{
	[MenuItem("Window/Empty Window")]
	static void Open()
	{
		GetWindow<UtilityWindow>("Utility Window");
	}
	void OnGUI()
	{
		if (GUILayout.Button("Change Shader"))
		{
			PrintAllSceneObjectNames();
		}
	}


	static void PrintAllSceneObjectNames()
	{
		var scene = SceneManager.GetActiveScene();
		if (!scene.IsValid() || !scene.isLoaded)
		{
			Debug.LogWarning("当前没有已加载的活动场景");
			return;
		}

		foreach (var root in scene.GetRootGameObjects())
		{
			PrintGameObjectHierarchy(root);
		}
	}

	static void PrintGameObjectHierarchy(GameObject go)
	{
		MeshRenderer mr = go.GetComponent<MeshRenderer>();
		if(mr)
		{
			List<Material> mats = new List<Material>();
			mr.GetMaterials(mats);

			foreach(var mat in mats)
			{
				//改变材质的shader
				mat.shader = Shader.Find("Custom RP/Lit");
			}

		}

		var transform = go.transform;
		for (int i = 0; i < transform.childCount; i++)
		{
			PrintGameObjectHierarchy(transform.GetChild(i).gameObject);
		}
	}
}


