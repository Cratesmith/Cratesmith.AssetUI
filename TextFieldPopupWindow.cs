#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace cratesmith.assetui
{
	public class TextFieldPopupWindow : EditorWindow
	{
		[Serializable] public class OnDoneAction : UnityEvent<string> {}
		[SerializeField] private string       value;
		[SerializeField] private OnDoneAction action = new OnDoneAction();
		[SerializeField] private GUIContent   titleText;
		[SerializeField] private bool         initalized;
		[SerializeField] private Texture      icon;
    
		static readonly Color    s_EvenColor = Color.white;
		static readonly Color    s_OddColor  = new Color(0.75f, 0.75f, 0.75f, 1f);
		static          GUIStyle s_ButtonStyle;
		int                      updateCount;

		void OnLostFocus()
		{
			Close();
		}

		public static void Create(string title, 
		                          UnityAction<string> action,
		                          string defaultValue="",
		                          Texture icon=null)
		{
			var wnd = CreateInstance<TextFieldPopupWindow>();
			wnd.titleText = new GUIContent(title);
			wnd.value = defaultValue;;
			wnd.action.AddListener(action);
			wnd.icon = icon;
			wnd.initalized = false;
			wnd.minSize = wnd.maxSize = new Vector2(320, Mathf.Min(320, 70f));
			wnd.updateCount = 0;
			wnd.ShowPopup();
		}

		void Update()
		{
			++updateCount;
		}

		void OnGUI()
		{
			if (s_ButtonStyle == null)
			{
				s_ButtonStyle = new GUIStyle(GUI.skin.GetStyle("Button"));
				s_ButtonStyle.alignment = TextAnchor.MiddleLeft;
			}

			if (!initalized)
			{            
				position = new Rect(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)-new Vector2(Screen.width,0), minSize);
				initalized = true;
				Repaint();
				Focus();
			}

			var cancelled = false;
			var confirmed = false;

			using (new EditorGUILayout.VerticalScope("box"))
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					if (icon != null)
					{
						GUILayout.Label(icon, GUILayout.Height(16), GUILayout.Width(16*(icon.width/icon.height)), GUILayout.ExpandWidth(false));
					}
                
					if(titleText!=null) GUILayout.Label(titleText);                
				}

				var changed = false;
				GUI.SetNextControlName("input");
				var prevValue = value;
				value = EditorGUILayout.TextField("", value, "miniTextField");
				changed = prevValue != value;
				
				EditorGUI.FocusTextInControl("input");

				using (new EditorGUILayout.HorizontalScope())
				{
					GUILayout.FlexibleSpace();

					if (GUILayout.Button("Cancel", GUILayout.Width(60))
					    || Event.current.keyCode == KeyCode.Escape)
					{
						cancelled = true;
					}

					GUILayout.Space(5);

					if (GUILayout.Button("Ok", GUILayout.Width(60))
					    || Event.current.keyCode == KeyCode.Return)
					{
						confirmed = true;
					}
				}
			}
			
			if(updateCount>30)
			{
				if (cancelled)
				{
					Close();
				}
				else if (confirmed)
				{
					action.Invoke(value);
					Close();
				}
			}
		}

		void OnEnable()
		{
			EditorApplication.update += ForceFocus;
			EditorApplication.LockReloadAssemblies();        
		}

		private void ForceFocus()
		{
			if (action == null)
			{
				Close();
				return;
			}

			if (focusedWindow != this)
			{
				Focus();
			}
		}

		void OnDisable()
		{
			EditorApplication.update -= ForceFocus;        
			UnityEditor.EditorApplication.UnlockReloadAssemblies();
		}
	}
}
#endif