#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEditor.AssetDatabase;

namespace cratesmith.assetui
{
	public class PopupEditorWindow : EditorWindow
	{
		Editor       m_editor;
		List<Editor> m_subEditors = new List<Editor>();
		[SerializeField]
		Vector2 m_scrollPosition = Vector2.zero;
		[SerializeField]
		Object m_target;
		[SerializeField] bool m_Pinned;

		static Texture2D s_PinButton;
		static Texture2D PinButton => s_PinButton
			? s_PinButton
			: s_PinButton = LoadAssetAtPath<Texture2D>(
				GUIDToAssetPath(
					FindAssets("popout_pin t:texture").FirstOrDefault()));

		const string MENUITEM_WINDOW_STRING    = "Window/View in Popup Inspector... &\\";
		const string MENUITEM_PREFABS_STRING   = "Window/Go to in scene and prefabs... ^t";
		const string MENUITEM_ASSETS_STRING    = "Window/Go to in scriptable objects... &t";
		const string MENUITEM_ALLASSETS_STRING = "Window/Go to in all assets... ^#t";

		[MenuItem(MENUITEM_WINDOW_STRING, true)]
		public static bool _PopupEditorWindowMenuItem()
		{
			return Selection.activeObject != null;
		}

		[MenuItem(MENUITEM_WINDOW_STRING)]
		public static void PopupEditorWindowMenuItem()
		{
			Popup(Selection.activeObject);
		}

		static void Popup(Object obj, string initialText="")
		{
			if (obj is GameObject gameObject)
			{
				GameObjectPopup(gameObject,initialText);
			} else if (obj)
			{
				Create(obj, GUIUtility.GUIToScreenPoint(Event.current.mousePosition), new Vector2(600, 500));
			}
		}

		[MenuItem(MENUITEM_PREFABS_STRING)]
		public static void PopupEditorWindowMenuItemPrefabs()
		{
			string title = "Go to gameobject or prefab";
			List<Object> extraObjects = new List<Object>();

			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				var scene = SceneManager.GetSceneAt(i);
				if (!scene.isLoaded)
					continue;

				extraObjects.AddRange(scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>()
				                                                                  .Select(x => x.gameObject)
				                                                                  .Where(x => !PrefabUtility.IsPartOfPrefabInstance(x) || PrefabUtility.IsOutermostPrefabInstanceRoot(x))));
			}

			AssetPopup("t:prefab", title, extraObjects);
		}

		[MenuItem(MENUITEM_ASSETS_STRING)]
		public static void PopupEditorWindowMenuItemAssets()
		{
			AssetPopup("t:ScriptableObject", "Go to ScriptableObject asset");
		}

		[MenuItem(MENUITEM_ALLASSETS_STRING)]
		public static void PopupEditorWindowMenuItemAllAssets()
		{
			AssetPopup("t:ScriptableObject t:prefab", "Go to asset");
		}

		static void AssetPopup(string filter, string title, List<Object> extraObjects = null)
		{
			if (extraObjects == null)
			{
				extraObjects = new List<Object>();
			}

			(Object, string, string)[] options = extraObjects.Select(x => (x, x.name, ""))
			                                                 .Concat(FindAssets(filter).Select(GUIDToAssetPath).Select(x => ((Object)null, Path.GetFileName(x), x))).ToArray();

			OptionPopupWindow.Create(title,
			                         (id,searchValue) =>
			                         {
				                         Object obj = options[id].Item1
					                         ? options[id].Item1
					                         : LoadMainAssetAtPath(options[id].Item3);

				                         Popup(obj, "");

				                         if (Event.current.control || Event.current.shift)
				                         {
					                         EditorGUIUtility.PingObject(obj);
				                         }

				                         if (Event.current.control)
				                         {
					                         Selection.activeObject = obj;
				                         }
			                         },
			                         options.Select(x => new GUIContent($"{x.Item2}", x.Item1 ? EditorIconUtility.GetIcon(x.Item1) : GetCachedIcon(x.Item3))).ToArray(),
			                         extraText: "+ctrl: select, +shift:highlight");
		}

		static void GameObjectPopup(GameObject gameObject, string initialText)
		{
			if (!gameObject)
			{
				return;
			}

			Object[] options = gameObject.GetComponentsInChildren<Transform>().Select(x => x.gameObject)
			                             .SelectMany(x => new Object[]
			                             {
				                             x
			                             }.Concat(x.GetComponentsInChildren<Component>()))
			                             .ToArray();

			OptionPopupWindow.Create("Select Component/GameObject",
			                         (id,_) => Create(options[id], GUIUtility.GUIToScreenPoint(Event.current.mousePosition), new Vector2(600, 500)),
			                         options.Select(x => new GUIContent($"{x.GetType().Name} ({x.name})", EditorIconUtility.GetIcon(x))).ToArray(), 
			                         initialText:initialText);
		}

		static bool ImageToggle(Rect position, Texture2D buttonImg, bool value, string tooltip)
		{
			bool result = GUI.Toggle(position, value, new GUIContent("", tooltip), "Button");
			Vector2 imgSize = new Vector2(buttonImg.width, buttonImg.height);
			Rect imgRect = new Rect(position.center - imgSize / 2f, imgSize);

			Color prevColor = GUI.color;
			GUI.color = EditorGUIUtility.isProSkin ? Color.white : Color.gray;
			GUI.DrawTexture(imgRect, buttonImg);
			GUI.color = prevColor;
			return result;
		}

		public static PopupEditorWindow Create(Object obj, Vector2 position, Vector2 size)
		{
			return Create(obj, new Rect(position - Vector2.right * (size.x * .5f), size));
		}

		public static PopupEditorWindow Create(Object obj, Rect rect)
		{
			PopupEditorWindow window = CreateInstance<PopupEditorWindow>();
			window.minSize = new Vector2(400, 500);

			window.Init(obj);

			window.Show();
			window.Focus();
			window.position = rect;
			return window;
		}

		void Init(Object obj)
		{
			m_target = obj;
			m_editor = Editor.CreateEditor(m_target);
			m_subEditors.Clear();

			GameObject gameObject = m_target as GameObject;

			if (gameObject)
			{
				foreach (Component component in gameObject.GetComponents<Component>())
				{
					m_subEditors.Add(Editor.CreateEditor(component));
				}
			}

			Texture2D icon = EditorIconUtility.GetIcon(obj);

			if (!icon)
			{
				icon = GetCachedIcon(GetAssetPath(obj)) as Texture2D;
			}

			titleContent = new GUIContent(m_target.name, icon);
		}

		void OnEnable()
		{
			if (m_target)
			{
				Init(m_target);
			}
		}

		void Update()
		{
			if (focusedWindow != null && focusedWindow != this && !(focusedWindow.GetType().Name == "ObjectSelector") && !(focusedWindow is OptionPopupWindow) && !m_Pinned && !docked)
			{
				Close();
			}
		}

		void OnGUI()
		{
			if (m_editor == null || m_editor.target == null)
			{
				return;
			}

			m_scrollPosition = GUILayout.BeginScrollView(m_scrollPosition, GUIStyle.none);
			OnGUI_DrawEditor(m_editor, true, false);

			foreach (Editor editor in m_subEditors)
			{
				OnGUI_DrawEditor(editor, false, true);
			}

			if (docked)
			{
				m_Pinned = true;
			} else
			{
				m_Pinned = ImageToggle(new Rect(4, 33, 18, 18), PinButton, m_Pinned, "Keep Open");
			}

			if (!m_Pinned && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
			{
				Close();
			}

			GUILayout.EndScrollView();
		}

		void OnGUI_DrawEditor(Editor editor, bool drawHeader, bool isExpandable)
		{
			if (editor.targets.Length == 0)
			{
				return;
			}

			bool wideMode = EditorGUIUtility.wideMode;
			float labelWidth = EditorGUIUtility.labelWidth;
			float fieldWidth = EditorGUIUtility.fieldWidth;

			EditorGUIUtility.wideMode = true;
			EditorGUIUtility.labelWidth = 0;
			EditorGUIUtility.fieldWidth = 0;

			if (drawHeader)
			{
				editor.DrawHeader();
			}

			GUIStyle style = !editor.UseDefaultMargins() ? GUIStyle.none : EditorStyles.inspectorDefaultMargins;

			using (new EditorGUILayout.VerticalScope(style))
			{
				bool drawEditor = !isExpandable;

				if (isExpandable)
				{
					bool prevExpanded = false;

					foreach (Object target in editor.targets)
					{
						if (InternalEditorUtility.GetIsInspectorExpanded(target))
						{
							prevExpanded = true;
							break;
						}
					}

					bool expanded = EditorGUILayout.InspectorTitlebar(prevExpanded, editor.targets);

					if (expanded != prevExpanded)
					{
						foreach (Object target in editor.targets)
						{
							InternalEditorUtility.SetIsInspectorExpanded(target, expanded);
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
