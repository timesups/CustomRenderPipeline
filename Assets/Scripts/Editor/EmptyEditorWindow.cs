using UnityEditor;
using UnityEngine;

public class EmptyEditorWindow : EditorWindow
{
	[MenuItem("Window/Empty Window")]
	static void Open()
	{
		GetWindow<EmptyEditorWindow>("Empty Window");
	}

	void OnGUI()
	{
	}
}
