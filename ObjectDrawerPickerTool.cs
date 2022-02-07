#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else 
using UnityEditor.Experimental.SceneManagement;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

namespace cratesmith.assetui
{
	class ObjectDrawerPickerTool : EditorTool
	{
		Object                        m_PickObj;
		Type                          m_PropType;
		Object[]                      m_TargetObjects;
		string                        m_SerializedPropertyPath;
		static Texture2D              s_PickerButton;
		static ObjectDrawerPickerTool s_Instance;

		public static ObjectDrawerPickerTool Instance => s_Instance = s_Instance
			? s_Instance
			: CreateInstance<ObjectDrawerPickerTool>();
		
		public static Texture2D PickerIcon => s_PickerButton
			? s_PickerButton
			: s_PickerButton = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("objectdrawer_pick t:texture").FirstOrDefault()));

		public override GUIContent toolbarIcon => new GUIContent(PickerIcon);
		public override bool IsAvailable() => m_TargetObjects != null && m_TargetObjects.Length>0;

		public static bool CanPickProperty(SerializedProperty property)
		{
			if(property== null || property.propertyType != SerializedPropertyType.ObjectReference) 
				return false;

			var propType = property.GetSerializedPropertyType();
			if (propType!=typeof(Object)
			    && !typeof(GameObject).IsAssignableFrom(propType) 
			    && !typeof(Component).IsAssignableFrom(propType))
				return false;
			
			foreach (var target in property.serializedObject.targetObjects)
			{
				if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(target)))
					return false;
			}
			return true;
		}

		public static void DoPicker(SerializedProperty property)
		{
			if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
			{
				Debug.LogError("Picker must point to an object reference property");
			}
			Instance.m_SerializedPropertyPath = property.propertyPath;
			Instance.m_PropType = property.GetSerializedPropertyType();  
			Instance.m_TargetObjects = property.serializedObject.targetObjects;
			ToolManager.SetActiveTool(Instance);
		}

		public override void OnActivated()
		{
			m_PickObj = null;
			Selection.selectionChanged += SelectionChanged;
		}
		void SelectionChanged()
		{
			ToolManager.RestorePreviousTool();
		}

		public override void OnWillBeDeactivated()
		{
			m_SerializedPropertyPath = null;
			m_TargetObjects = null;
			m_PickObj = null;
			m_PropType = null;
			Selection.selectionChanged -= SelectionChanged;
			DestroyImmediate(s_Instance);
		}

		public override void OnToolGUI(EditorWindow window)
		{
			HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
			
			if (!(window is SceneView sceneView))
			{
				Debug.LogError("Not a scene view");
				return;
			}
			
			var serializedObject = m_TargetObjects != null && m_TargetObjects.Length > 0 ? new SerializedObject(m_TargetObjects) : null;
			var property = serializedObject != null ? serializedObject.FindProperty(m_SerializedPropertyPath):null;
			if (property==null || m_PropType==null)
			{
				ToolManager.RestorePreviousTool();
				return;
			}
  
			if (Event.current.type == EventType.MouseMove)
			{
				var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
				var filter = typeof(Component).IsAssignableFrom(m_PropType) 
					? (prefabStage==null 
						? GameObject.FindObjectsOfType(m_PropType)
						: prefabStage.prefabContentsRoot.GetComponentsInChildren(m_PropType))
					  .SelectMany(x=>((Component)x).GetComponentsInChildren<Component>())
					  .Select(x=>x.gameObject).ToArray()
					: null;
				if (!TryPickObject(Event.current.mousePosition, true, filter, out m_PickObj))
				{
					TryPickObject(Event.current.mousePosition, false, filter, out m_PickObj);
				}
			}

			if (m_PickObj)
			{
				EditorGUIUtility.AddCursorRect(sceneView.position, MouseCursor.Link);
				Handles.Label(HandleUtility.GUIPointToWorldRay(Event.current.mousePosition - Vector2.down*30f).GetPoint(0), m_PickObj.name);
			}

			if (Event.current.type == EventType.MouseDown
			    && Event.current.button==0
			    && m_PickObj)
			{
				Undo.RecordObjects(m_TargetObjects, $"Picking object {m_PickObj.name}");
				property.objectReferenceValue = m_PickObj;
				serializedObject.ApplyModifiedProperties();
				ToolManager.RestorePreviousTool();
			}
		}
		bool TryPickObject(Vector2 position, bool selectPrefabRoot, GameObject[] filter, out Object result)
		{
			var pickGO = HandleUtility.PickGameObject(position, selectPrefabRoot, null, filter);
			return result = pickGO==null || !typeof(Component).IsAssignableFrom(m_PropType)
				? pickGO
				: (Object)pickGO.GetComponentInParent(m_PropType);
		}
		
		public static bool IsPickingFor(SerializedProperty property)
		{
			return ToolManager.IsActiveTool(Instance)
			       && Instance.m_SerializedPropertyPath !=null
			       && Instance.m_TargetObjects!=null
			       && property !=null
			       && new HashSet<Object>(property.serializedObject.targetObjects).SetEquals(Instance.m_TargetObjects) 
			       && property.propertyPath == Instance.m_SerializedPropertyPath;
		}
		public static void Cancel()
		{
			if (ToolManager.IsActiveTool(Instance))
			{
				ToolManager.RestorePreviousTool();
			}
		}
	}
}
#endif