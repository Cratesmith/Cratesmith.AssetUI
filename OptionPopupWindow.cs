#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using static UnityEditor.AssetDatabase;

namespace cratesmith.assetui
{
    public class OptionPopupWindow : EditorWindow
    {
        [Serializable] public class OnDoneAction : UnityEvent<int, string> {} 
    
        [SerializeField]         GUIContent[] options;
        [SerializeField] private string       value;
        [SerializeField] private OnDoneAction action = new OnDoneAction();
        [SerializeField] private GUIContent   titleText;
        [SerializeField] private bool         initalized;
        [SerializeField] private int          updateCount;
        [SerializeField] private Texture      icon;
        [SerializeField]         int          selectedIndex;
        [SerializeField]         Vector2      scrollPosition;
        [SerializeField]         GUIContent[] shownOptions;
        [SerializeField]         int[]        optionIds;
        [SerializeField]         bool         scrollTo;  
        [SerializeField]         string       extraText;

        
        static readonly Color    s_EvenColor = Color.white;
        static readonly Color    s_OddColor  = new Color(0.75f, 0.75f, 0.75f, 1f);
        static readonly Color    s_ShortcutColor  = new Color(0.35f, 0.35f, 0.35f, 1f);
        static          GUIStyle s_ButtonStyle;

        static Texture2D s_TransHeart;
        static Texture2D TransHeart => s_TransHeart
            ? s_TransHeart
            : s_TransHeart = LoadAssetAtPath<Texture2D>(
                GUIDToAssetPath(
                    FindAssets("trans_heart t:texture").FirstOrDefault()));

        
        void OnLostFocus()
        {
            Close();
        }

        public static void Create(string title, 
            UnityAction<int, string> action, 
            GUIContent[] options,
            Texture icon=null,
            string extraText="",
            string initialText="")
        {
            var wnd = CreateInstance<OptionPopupWindow>();
            wnd.titleText = new GUIContent(title);
            wnd.value = "";;
            wnd.action.AddListener(action);
            wnd.icon = icon;
            wnd.initalized = false;
            wnd.minSize = wnd.maxSize = new Vector2(320, Mathf.Min(352, options.Length*25f + 80f));
            wnd.ShowPopup();
            wnd.updateCount = 0;
            wnd.options = options;
            wnd.scrollPosition = Vector2.zero;
            wnd.scrollTo = false;
            wnd.extraText = extraText;
            wnd.value = initialText;
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
                updateCount = 0;
                position = new Rect(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)-new Vector2(Screen.width,0), minSize);
                initalized = true;
                Repaint();
                Focus();
            }

            var cancelled = false;
            var confirmed = false;

            if (Event.current.rawType == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.UpArrow)
                {
                    --selectedIndex;
                    scrollTo = true;
                    Event.current.Use();
                    Repaint();
                }
                if (Event.current.keyCode == KeyCode.DownArrow)
                {
                    ++selectedIndex;
                    scrollTo = true;
                    Event.current.Use();
                    Repaint();
                }
                if (Event.current.functionKey)
                {
                    for (int i = 0; i < Mathf.Min(12,options.Length); i++)
                    {
                        var keycode = (KeyCode)Enum.Parse(typeof(KeyCode), $"F{i+1}");
                        if (Event.current.keyCode == keycode)
                        {
                            selectedIndex = i;
                            scrollTo = true;
                            Event.current.Use();
                            confirmed = true;
                        }
                    }
                }
            }
            
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
                if (options.Length > 1)
                {
                    GUI.SetNextControlName("input");
                    var prevValue = value;
                    value = EditorGUILayout.TextField("", value, "SearchTextField");
                    changed = prevValue != value;
                }
            
                if (changed || shownOptions==null)
                {
                    var splits = value!=null 
                        ? value.ToLowerInvariant().Split(' ')
                        : new string[0];
                
                    shownOptions = splits.Length > 0
                        ? options.Where(x => splits.All(x.text.ToLowerInvariant().Contains)).ToArray()
                        : options;

                    optionIds = shownOptions.Select(x => Array.IndexOf(options, x)).ToArray();

                    selectedIndex = shownOptions.Length>0 ? 0:-1;
                    scrollTo = true;
                }
            
                EditorGUI.FocusTextInControl("input");
            
                selectedIndex = shownOptions.Length > 0
                    ? Mathf.Clamp(selectedIndex, 0, shownOptions.Length - 1)
                    : selectedIndex = -1;

                using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPosition, GUILayout.ExpandHeight(true)))
                {
                    scrollPosition = scroll.scrollPosition;
                    var prevBGColor = GUI.backgroundColor;
                    for (int i = 0; i < shownOptions.Length; i++)
                    {
                        GUI.backgroundColor = s_ShortcutColor;
                        GUILayout.Box(i<12?$"F{i+1}":"",s_ButtonStyle, GUILayout.Height(20));
                        var lastRect = GUILayoutUtility.GetLastRect();
                        var buttonRect = new Rect(lastRect.x + 30, lastRect.y, lastRect.width-30, lastRect.height);
                        GUI.backgroundColor = 
                            i == selectedIndex 
                                ? Color.blue
                                : (i%2==0 ? s_EvenColor : s_OddColor); 
                        if (GUI.Button(buttonRect,shownOptions[i], s_ButtonStyle))
                        {
                            selectedIndex = i;
                            confirmed = true;
                        }
                
                        if (Event.current.isKey && scrollTo && selectedIndex == i)
                        {
                            scrollPosition.y = lastRect.y - lastRect.height*1.5f;
                            scrollTo = false;
                            Repaint();
                        };
                    }

                    GUI.backgroundColor = prevBGColor;
                }
           
            
                using (new EditorGUILayout.HorizontalScope())
                {
                    if(!string.IsNullOrEmpty(extraText))
                        GUILayout.Label(extraText, GUILayout.Width(Screen.width-150));
                    else
                    {
                        GUI.color = new Color(.75f, .75f, .75f, 1f);
                        GUILayout.Label(new GUIContent("Trans rights!",TransHeart), GUILayout.Height(16), GUILayout.Width(Screen.width-150));
                        GUI.color = Color.white;
                    }
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Cancel", GUILayout.Width(60))
                        || Event.current.keyCode == KeyCode.Escape)
                    {
                        cancelled = true;
                    }

                    GUILayout.Space(5);

                    if (GUILayout.Button("Ok", GUILayout.Width(60))
                        || (Event.current.keyCode == KeyCode.Return))
                    {
                        confirmed = true;
                    }
                }
            }

            if(updateCount > 30)
            {
                if (cancelled)            
                {
                    Close();
                }
                else if (confirmed && selectedIndex>=0 && selectedIndex < optionIds.Length)
                {
                    action.Invoke(optionIds[selectedIndex], value);
                    Close();
                }
            }
        }

        void OnEnable()
        {
            EditorApplication.update += ForceFocus;
            EditorApplication.LockReloadAssemblies();        
        }

        private void Update()
        {
            updateCount++;
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
            EditorApplication.UnlockReloadAssemblies();
        }
    }
}
#endif