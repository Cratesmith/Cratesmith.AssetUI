#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace cratesmith.assetui
{
	public class PopupEditorWindow : EditorWindow
	{
		[SerializeField] private Editor       m_editor;
		[SerializeField] private List<Editor> m_subEditors     = new List<Editor>();
		[SerializeField] private Vector2      m_scrollPosition = Vector2.zero;
		[SerializeField] private Object       m_target;
		[SerializeField]         bool         m_Pinned;

		private const string MENUITEM_WINDOW_STRING = "Window/Create Popup Inspector...";
		private const string MENUITEM_ASSETS_STRING = "Assets/View in Popup Inspector...";
		private const string MENUITEM_CONTEXT_STRING = "CONTEXT/Object/View in Popup Inspector...";

		[MenuItem(MENUITEM_WINDOW_STRING, true)]
		[MenuItem(MENUITEM_ASSETS_STRING, true)]
		public static bool _PopupEditorWindowMenuItem()
		{
			return Selection.activeObject != null;
		}

		[MenuItem(MENUITEM_CONTEXT_STRING)]
		public static void PopupEditorWindowMenuItem(MenuCommand command)
		{
			Create(command.context, new Rect(50, 50, 600, 500));
		}

		[MenuItem(MENUITEM_WINDOW_STRING)]
		[MenuItem(MENUITEM_ASSETS_STRING)]
		public static void PopupEditorWindowMenuItem()
		{
			Create(Selection.activeObject, new Rect(50, 50, 600, 500));
		}

		public static void DrawPopOutButton(Rect position, Object popoutReference)
		{
			if (popoutReference != null)
			{
				var wasEnabled = GUI.enabled;
				GUI.enabled = true;
				var indentedPos = EditorGUI.IndentedRect(position);
				var buttonRect = new Rect(indentedPos.x - indentedPos.height, indentedPos.y, indentedPos.height, indentedPos.height);
				if (GUI.Button(buttonRect, "", "OL Plus"))
				{
					var windowRect = new Rect(GUIUtility.GUIToScreenPoint(position.position), new Vector2(400, 500));
					PopupEditorWindow.Create(popoutReference, windowRect);
				}
				GUI.enabled = wasEnabled;
			}
		}

		public static PopupEditorWindow Create(Object obj, Rect rect)
		{
			var window = CreateInstance<PopupEditorWindow>();
			window.minSize	= new Vector2(400, 500);

			window.Init(obj);

			window.Show();
			window.Focus();
			window.position	= rect;
			return window;
		}

		private void Init(Object obj)
		{
			m_target = obj;
			m_editor = Editor.CreateEditor(m_target);

			var gameObject = m_target as GameObject;
			if (gameObject)
			{
				foreach (var component in gameObject.GetComponents<Component>())
				{
					m_subEditors.Add(Editor.CreateEditor(component));
				}
			}

			var icon = EditorIconUtility.GetIcon(obj);
			if (!icon) icon = AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(obj)) as Texture2D;
			
			titleContent = new GUIContent(m_target.name, icon);
		}

		void OnEnable()
		{
			if (m_target && m_editor == null)
			{
				Init(m_target);
			}		
		}

		void OnGUI()
		{
			if (m_editor==null || m_editor.target==null)
			{
				return;		
			}

			m_scrollPosition = GUILayout.BeginScrollView(m_scrollPosition, GUIStyle.none);
			OnGUI_DrawEditor(m_editor, true, false);
			foreach (var editor in m_subEditors)
			{
				OnGUI_DrawEditor(editor, false, true);
			}

			if (docked)
				m_Pinned = true;
			else
				m_Pinned = GUI.Toggle(new Rect(46, 28, 75, 18), m_Pinned, "Keep Open","button");
			
			if (!m_Pinned && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
			{
				Close();
			}
			
			GUILayout.EndScrollView();
		}

		void OnLostFocus()
		{
			if(!m_Pinned)
				Close();
		}

		private void OnGUI_DrawEditor(Editor editor, bool drawHeader, bool isExpandable)
		{
			if (editor.targets.Length == 0)
			{
				return;		
			}

			bool wideMode	= EditorGUIUtility.wideMode;
			var labelWidth	= EditorGUIUtility.labelWidth;
			var fieldWidth	= EditorGUIUtility.fieldWidth;

			EditorGUIUtility.wideMode = true;
			EditorGUIUtility.labelWidth = 0;
			EditorGUIUtility.fieldWidth = 0;

			if (drawHeader)
			{
				editor.DrawHeader();
			}

			var style = !editor.UseDefaultMargins() ? GUIStyle.none : EditorStyles.inspectorDefaultMargins;
			using (new EditorGUILayout.VerticalScope(style))
			{
				bool drawEditor = !isExpandable;
				if (isExpandable)
				{
					var prevExpanded = false;
					foreach (var target in editor.targets)
					{
						if (UnityEditorInternal.InternalEditorUtility.GetIsInspectorExpanded(target))
						{
							prevExpanded = true;
							break;
						}
					}

					var expanded = EditorGUILayout.InspectorTitlebar(prevExpanded, editor.targets);
					if (expanded != prevExpanded)
					{
						foreach (var target in editor.targets)
						{
							UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(target, expanded);
						}
					}

					drawEditor = expanded;
				}

				if (drawEditor)
				{
					editor.OnInspectorGUI();
				}
			}

			EditorGUIUtility.labelWidth = labelWidth;
			EditorGUIUtility.fieldWidth = fieldWidth;
			EditorGUIUtility.wideMode = wideMode;
		}
	}
}
#endif