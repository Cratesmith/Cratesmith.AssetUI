#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using static UnityEditor.AssetDatabase;

namespace cratesmith.assetui
{
	public class PopupEditorWindow : EditorWindow
	{
		private Editor       m_editor;
		private List<Editor> m_subEditors     = new List<Editor>();
		[SerializeField] private Vector2      m_scrollPosition = Vector2.zero;
		[SerializeField] private Object       m_target;
		[SerializeField]         bool         m_Pinned;

		static Texture2D s_PinButton;
		static Texture2D PinButton => s_PinButton
			? s_PinButton
			: s_PinButton = LoadAssetAtPath<Texture2D>(
				GUIDToAssetPath(
					FindAssets("popout_pin t:texture").FirstOrDefault()));
		
		private const string MENUITEM_WINDOW_STRING  = "Window/View in Popup Inspector... &\\";
		private const string MENUITEM_PREFABS_STRING = "Window/Go to in scene and prefabs... ^t";
		private const string MENUITEM_ASSETS_STRING  = "Window/Go to in scriptable objects... &t";
		private const string MENUITEM_ALLASSETS_STRING  = "Window/Go to in all assets... ^#t";
		
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
		
		static void Popup(Object obj)
		{
			if (obj is GameObject gameObject)
			{
				GameObjectPopup(gameObject);
			} else if (obj)
				Create(obj, new Rect(GUIUtility.GUIToScreenPoint(Event.current.mousePosition), new Vector2(600, 500)));
		}

		[MenuItem(MENUITEM_PREFABS_STRING)]
		public static void PopupEditorWindowMenuItemPrefabs()
		{
			var title = "Go to gameobject or prefab";
			var extraObjects = new List<Object>();
			for (int i = 0; i < EditorSceneManager.sceneCount; i++)
			{
				var roots = EditorSceneManager.GetSceneAt(i).GetRootGameObjects();

				extraObjects.AddRange(roots.SelectMany(x => x.GetComponentsInChildren<Transform>()
				                                             .Select(x => x.gameObject)
				                                             .Where(x => !PrefabUtility.IsPartOfPrefabInstance(x) || PrefabUtility.IsOutermostPrefabInstanceRoot(x))));
			}
			
			AssetPopup("t:prefab", title,extraObjects);
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
		
		static void AssetPopup(string filter, string title, List<Object> extraObjects=null)
		{
			if (extraObjects == null)
				extraObjects = new List<Object>();

			var options = extraObjects.Select(x => (x, x.name, ""))
			                          .Concat(AssetDatabase.FindAssets(filter).Select(AssetDatabase.GUIDToAssetPath).Select(x => ((Object)null, Path.GetFileName(x), x))).ToArray();

			OptionPopupWindow.Create(title,
			                         id => Popup(options[id].Item1 ? options[id].Item1 : AssetDatabase.LoadMainAssetAtPath(options[id].Item3)),
			                         options.Select(x => new GUIContent($"{x.Item2}", x.Item1 ? EditorIconUtility.GetIcon(x.Item1) : AssetDatabase.GetCachedIcon(x.Item3))).ToArray());
		}

		static void GameObjectPopup(GameObject gameObject)
		{
			if (!gameObject)
				return;
		
			var options = gameObject.GetComponentsInChildren<Transform>().Select(x => x.gameObject)
			                        .SelectMany(x => new Object[]
			                        {
				                        x
			                        }.Concat(x.GetComponentsInChildren<Component>()))
			                        .ToArray();

			OptionPopupWindow.Create("Select Component/GameObject",
			                         id => Create(options[id], new Rect(GUIUtility.GUIToScreenPoint(Event.current.mousePosition), new Vector2(600, 500))),
			                         options.Select(x => new GUIContent($"{x.GetType().Name} ({x.name})", EditorIconUtility.GetIcon(x))).ToArray());
		}

		static bool ImageToggle(Rect position, Texture2D buttonImg, bool value, string tooltip)
		{
			bool result = GUI.Toggle(position, value, new GUIContent("",tooltip), "Button");
			Vector2 imgSize = new Vector2(buttonImg.width, buttonImg.height);
			Rect imgRect = new Rect(position.center - imgSize / 2f, imgSize);

			Color prevColor = GUI.color;
			GUI.color = EditorGUIUtility.isProSkin ? Color.white : Color.gray;
			GUI.DrawTexture(imgRect, buttonImg);
			GUI.color = prevColor;
			return result;
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
			m_subEditors.Clear();

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
			if (m_target)
			{
				Init(m_target);
			}		
		}

		void Update()
		{
			if (EditorWindow.focusedWindow != null 
			    && focusedWindow!=this 
			    && !(focusedWindow.GetType().Name == "ObjectSelector")
				&& !(focusedWindow is OptionPopupWindow)
			    && !m_Pinned)
			{
				Close();
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
				m_Pinned = ImageToggle(new Rect(4, 33, 18, 18), PinButton, m_Pinned,"Keep Open");
			
			if (!m_Pinned && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
			{
				Close();
			}
			
			GUILayout.EndScrollView();
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