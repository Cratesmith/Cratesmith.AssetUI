using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using static UnityEditor.AssetDatabase;
using Object = UnityEngine.Object;

namespace cratesmith.assetui
{
	[CustomPropertyDrawer(typeof(Object), true)]
	public class ObjectDrawer : PropertyDrawer
	{
		static Texture2D                                     s_ExpandButton;
		static Texture2D                                     s_NewButton;
		static Dictionary<(object, string, string), ReorderableList> s_Lists = new Dictionary<(object, string, string), ReorderableList>();

		static Texture2D ExpandButton => s_ExpandButton
			? s_ExpandButton
			: s_ExpandButton = LoadAssetAtPath<Texture2D>(
				GUIDToAssetPath(
					FindAssets("objectdrawer_expand t:texture").FirstOrDefault()));

		static Texture2D NewButton => s_NewButton
			? s_NewButton
			: s_NewButton = LoadAssetAtPath<Texture2D>(
				GUIDToAssetPath(
					FindAssets("objectdrawer_new t:texture").FirstOrDefault()));

		static Dictionary<Type, (Type type, CreateAssetMenuAttribute attribute)[]> s_cachedTypes = new Dictionary<Type, (Type type, CreateAssetMenuAttribute attribute)[]>();

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return CalcPropertyHeight(property, label);
		}
		
		public static float CalcPropertyHeight(SerializedProperty property, GUIContent label)
		{
			float baseHeight = EditorGUIUtility.singleLineHeight;

			ScriptableObject data = property!=null 
			                        && property.isExpanded 
			                        && property.propertyType==SerializedPropertyType.ObjectReference 
			                        && !property.hasMultipleDifferentValues
				? property.objectReferenceValue as ScriptableObject
				: null;
			
			if (data)
			{
				baseHeight += EditorGUIUtility.standardVerticalSpacing * 2;

				SerializedObject so = new SerializedObject(data);
				SerializedProperty iterator = so.GetIterator();
				iterator.NextVisible(true);

				var foldoutHeight = 0f;
				while (iterator.NextVisible(false))
				{
					var current = iterator.Copy();
					if (TryGetSublist(property, current, out var list))
						foldoutHeight += list.GetHeight() + EditorGUIUtility.standardVerticalSpacing;   
					else 
						foldoutHeight += EditorGUI.GetPropertyHeight(current, label, true) + EditorGUIUtility.standardVerticalSpacing;
				}
				baseHeight += Mathf.Max(EditorGUIUtility.singleLineHeight, foldoutHeight);
			}

			return baseHeight;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			PropertyField(position, property, label);
		}

		public static void ObjectField(Rect position, SerializedProperty property, Type objType, GUIContent label)
		{
			Rect propertyFieldRect = PropertyFieldLeft(position, property);
			EditorGUI.ObjectField(propertyFieldRect, property, objType, label);
			PropertyFieldRight(position, property, label);
		}

		public static void ObjectField(Rect position, SerializedProperty property, Type objType)
		{
			Rect propertyFieldRect = PropertyFieldLeft(position, property);
			EditorGUI.ObjectField(propertyFieldRect, property, objType);
			PropertyFieldRight(position, property, GUIContent.none);
		}

		public static void PropertyField(Rect position, SerializedProperty property, GUIContent label)
		{
			Rect propertyFieldRect = PropertyFieldLeft(position, property);
			EditorGUI.PropertyField(propertyFieldRect, property, label, true);
			PropertyFieldRight(position, property, label);
		}
		static void PropertyFieldRight(Rect position, SerializedProperty property, GUIContent label)
		{
			if (property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceValue && !property.hasMultipleDifferentValues)
			{
				DrawScriptableObjectFoldout(position, property, label);
			}
		}
		static Rect PropertyFieldLeft(Rect position, SerializedProperty property)
		{
			Rect popupWindowRect = new Rect(GUIUtility.GUIToScreenPoint(position.position - Vector2.right * 416),
			                                new Vector2(400, 500));

			int singleButtonWidth = 20;
			Rect propertyFieldRect = position;
			
			// Draw "Picker tool"
			if (ObjectDrawerPickerTool.CanPickProperty(property))
			{
				Rect buttonRect = new Rect(propertyFieldRect.xMax-singleButtonWidth, position.y, singleButtonWidth, EditorGUIUtility.singleLineHeight);
				var wasPicking = ObjectDrawerPickerTool.IsPickingFor(property);
				var isPicking = ImageToggle(buttonRect, ObjectDrawerPickerTool.PickerIcon, wasPicking, "Pick from scene"); 
				if (!wasPicking && isPicking)
				{
					ObjectDrawerPickerTool.DoPicker(property);
				} else if(!isPicking && wasPicking)
				{
					ObjectDrawerPickerTool.Cancel();
				}
				propertyFieldRect.xMax -= singleButtonWidth;
			}	
			
			// Draw "Create" for scriptable objects
			if (property.propertyType == SerializedPropertyType.ObjectReference)
			{
				Rect createBtnRect = new Rect(propertyFieldRect.xMax - singleButtonWidth, position.y, singleButtonWidth, EditorGUIUtility.singleLineHeight);
				if (DrawCreateScriptableObject(createBtnRect, property, popupWindowRect))
				{
					propertyFieldRect.xMax -= singleButtonWidth;
				}
			}

			// Draw "Popout window"
			if (!property.hasMultipleDifferentValues && property.propertyType == SerializedPropertyType.ObjectReference)
			{
				if (property.objectReferenceValue)
				{
					Rect popoutBtnRect = new Rect(propertyFieldRect.xMax-singleButtonWidth, position.y, singleButtonWidth, EditorGUIUtility.singleLineHeight);
					DrawPopoutInspector(popoutBtnRect, property, popupWindowRect);
					propertyFieldRect.xMax -= singleButtonWidth;
				}
			}

			return propertyFieldRect;
		}

		static void DrawScriptableObjectFoldout(Rect position, SerializedProperty property, GUIContent label)
		{
			ScriptableObject data = !property.hasMultipleDifferentValues
				? property.objectReferenceValue as ScriptableObject
				: null;

			if (data)
			{
				float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

				Rect foldoutRect = new Rect(position.position,
				                            new Vector2(position.width, EditorGUIUtility.singleLineHeight));

				GUI.color = Color.cyan;

				property.isExpanded = EditorGUI.Foldout(foldoutRect,
				                                        property.isExpanded,
				                                        "",
				                                        true);

				GUI.color = Color.white;

				if (property.isExpanded)
				{
					var prevLabelWidth = EditorGUIUtility.labelWidth;
					EditorGUIUtility.labelWidth -= 15f;
					//using (new EditorGUI.IndentLevelScope())
					{
						float fullHeight = CalcPropertyHeight(property, label);

						GUI.Box(new Rect(position.x + EditorGUI.indentLevel * 15f,
						                 position.y + EditorGUIUtility.singleLineHeight,
						                 position.width - EditorGUI.indentLevel * 15f + 2,
						                 Mathf.Max(fullHeight - EditorGUIUtility.singleLineHeight,EditorGUIUtility.singleLineHeight)),
						        "");

						y += EditorGUIUtility.standardVerticalSpacing;
					}

					SerializedObject so = new SerializedObject(data);
					EditorGUI.BeginChangeCheck();

					SerializedProperty iterator = so.GetIterator();
					iterator.NextVisible(true);

					int count = 0;
					while (iterator.NextVisible(false))
					{
						var current = iterator.Copy();
						float childHeight = CalcPropertyHeight(iterator, new GUIContent(iterator.displayName));
						Rect childRect = new Rect(position.x + 15f, y, position.width - 15f, childHeight);
						if (TryGetSublist(property, current, out var list))
							list.DoList(childRect);
						else
							EditorGUI.PropertyField(childRect, current, true);
						y += childHeight + EditorGUIUtility.standardVerticalSpacing;
						++count;
					}

					if (count == 0)
					{
						Rect childRect = new Rect(position.x + 15f, y, position.width - 15f, EditorGUIUtility.singleLineHeight);
						GUI.Label(childRect, "No properties");
						y += EditorGUIUtility.singleLineHeight;
					}
					
					if (EditorGUI.EndChangeCheck())
					{
						so.ApplyModifiedProperties();
					}
					EditorGUIUtility.labelWidth = prevLabelWidth;
				}
			}
		}
		static bool TryGetSublist(SerializedProperty ownerProperty, SerializedProperty property, out ReorderableList list)
		{
			if (ownerProperty == null || property == null || !property.isArray || property.arraySize==0)
			{
				list = null;
				return false;
			}

			var key = (ownerProperty.serializedObject, ownerProperty.propertyPath, property.propertyPath);
			if (!s_Lists.TryGetValue(key, out list))
			{
				var newList = list = s_Lists[key] = new ReorderableList(property.serializedObject, property, true, false, true, true);
				list.drawElementCallback = (rect, index, active, focused) =>
				{
					var current = newList.serializedProperty.GetArrayElementAtIndex(index);
					var prevWidth = EditorGUIUtility.labelWidth;
					EditorGUIUtility.labelWidth = Mathf.Min(CalcMaxLabelWidth(newList.serializedProperty), EditorGUIUtility.labelWidth);
					EditorGUI.PropertyField(rect, current);
					EditorGUIUtility.labelWidth = prevWidth;
				};
				list.elementHeightCallback = delegate(int index)
				{
					return EditorGUI.GetPropertyHeight(newList.serializedProperty.GetArrayElementAtIndex(index));
				};
			}
			return true;
		}
		static float CalcMaxLabelWidth(SerializedProperty property)
		{
			var width = 0f;
			var iterator = property.Copy();
			iterator.NextVisible(true);
			while (iterator.NextVisible(false))
			{
				width = Mathf.Max(GUI.skin.label.CalcSize(new GUIContent(property.displayName)).x+20, width);
			}
			return width;
		}

		static void DrawPopoutInspector(Rect position, SerializedProperty property, Rect popupWindowRect)
		{
			if (property.objectReferenceValue != null && !property.hasMultipleDifferentValues)
			{
				bool wasEnabled = GUI.enabled;
				GUI.enabled = true;
  
				if (ImageButton(position, ExpandButton, "Show in popout inspector..."))
				{
					PopupEditorWindow.Create(property.objectReferenceValue, popupWindowRect);
				}

				GUI.enabled = wasEnabled;
			}
		}

		static bool DrawCreateScriptableObject(Rect position, SerializedProperty property, Rect popupWindowRect)
		{
			Type propType = property.GetSerializedPropertyType();

			if (propType == null || !typeof(ScriptableObject).IsAssignableFrom(propType))
			{
				return false;
			}

			if (!s_cachedTypes.TryGetValue(propType, out (Type type, CreateAssetMenuAttribute attribute)[] validTypes))
			{
				s_cachedTypes[propType] = validTypes = AppDomain.CurrentDomain.GetAssemblies()
				                                                .SelectMany(x =>
				                                                {
					                                                try
					                                                {
						                                                return x.GetTypes();
					                                                }
					                                                catch (ReflectionTypeLoadException e)
					                                                {
						                                                return e.Types.Where(t => t != null);
					                                                }
				                                                })
				                                                .Select(t => (type: t, attribute: t?.GetCustomAttribute<CreateAssetMenuAttribute>()))
				                                                .Where(t => !t.type.IsAbstract && t.attribute != null && propType.IsAssignableFrom(t.type))
				                                                .ToArray();
			}

			if (validTypes.Length == 0)
			{
				return false;
			}

			// var indentedPos = EditorGUI.IndentedRect(position);
			// var buttonRect = new Rect(indentedPos.x - indentedPos.height, indentedPos.y, indentedPos.height,
			// 	indentedPos.height);
			if (ImageButton(position, NewButton, "Create Asset..."))
			{
				string filepathPrefix = property.serializedObject.isEditingMultipleObjects
					? GetPath(property.serializedObject.targetObject)
					: GetLongestCommonPrefix(property.serializedObject.targetObjects
					                                 .Select(x => Path.GetDirectoryName(GetPath(x))).ToArray());

				GUIContent[] _options = validTypes.Select(x => (name: !string.IsNullOrEmpty(x.attribute.menuName)
					                                                ? x.attribute.menuName.Substring(x.attribute.menuName.LastIndexOf('/') + 1)
					                                                : x.type.Name, icon: EditorIconUtility.GetIcon(x.type)))
				                                  .Select(x => new GUIContent(x.name, x.icon))
				                                  .ToArray();

				string defaultOutputPath = GenerateUniqueAssetPath($"{filepathPrefix}/{property.serializedObject.targetObject.name}");

				string callbackPropPath = property.propertyPath;
				SerializedObject callbackSO = new SerializedObject(property.serializedObject.targetObjects);

				Texture2D icon = EditorIconUtility.GetIcon(propType);

				CreateScriptableObjectWindow.Create($"Create {propType.Name}",
				                                    index =>
				                                    {
					                                    SerializedProperty prop = callbackSO.FindProperty(callbackPropPath);
					                                    (Type type, CreateAssetMenuAttribute attribute) typeData = validTypes[index];

					                                    string defaultName = string.IsNullOrEmpty(typeData.attribute.fileName)
						                                    ? Path.GetFileNameWithoutExtension(defaultOutputPath)
						                                    : typeData.attribute.fileName;

					                                    string filePath = EditorUtility.SaveFilePanel($"Create {typeData.attribute.menuName}",
					                                                                                  $"{filepathPrefix}",
					                                                                                  defaultName,
					                                                                                  $"{typeData.type.Name}.asset");

					                                    if (string.IsNullOrEmpty(filePath))
					                                    {
						                                    return;
					                                    }

					                                    filePath = GetRelativePath(Directory.GetParent(Application.dataPath).FullName + Path.DirectorySeparatorChar, filePath);

					                                    ScriptableObject instance = ScriptableObject.CreateInstance(validTypes[index].type);
					                                    CreateAsset(instance, filePath);
					                                    EditorGUIUtility.PingObject(instance);
					                                    prop.objectReferenceValue = instance;
					                                    prop.serializedObject.ApplyModifiedProperties();
					                                    PopupEditorWindow.Create(prop.objectReferenceValue, popupWindowRect);
				                                    },
				                                    _options,
				                                    defaultOutputPath,
				                                    icon);
			}

			return true;
		}

		static bool ImageButton(Rect position, Texture2D buttonImg, string tooltip)
		{
			bool result = GUI.Button(position, new GUIContent("",tooltip));
			Vector2 imgSize = new Vector2(buttonImg.width, buttonImg.height);
			Rect imgRect = new Rect(position.center - imgSize / 2f, imgSize);

			Color prevColor = GUI.color;
			GUI.color = EditorGUIUtility.isProSkin ? Color.white : Color.gray;
			GUI.DrawTexture(imgRect, buttonImg);
			GUI.color = prevColor;
			return result;
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

		static string GetPath(Object target)
		{
			// 1. check for an asset path (ScriptableObject)
			string path = GetAssetPath(target);

			// 2. check for a prefab path (could be referenced by a monobehaviour)
			Object prefab = PrefabUtility.GetCorrespondingObjectFromSource(target);

			if (prefab != null && string.IsNullOrEmpty(path))
			{
				path = GetAssetPath(prefab);
			}

			// 3. check for a scene path
			Component comp = target as Component;

			if (comp != null)
			{
#if UNITY_2018_3_OR_NEWER
				PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

				if (prefabStage != null && prefabStage.IsPartOfPrefabContents(comp.gameObject))
				{
#if UNITY_2020_1_OR_NEWER
					path = Path.GetDirectoryName(prefabStage.assetPath) 
					       + "/" + Path.GetFileNameWithoutExtension(prefabStage.assetPath) 
					       + "." + comp.gameObject.name + ".obj"; // fake extension as we strip it
#else
					path = Path.GetDirectoryName(prefabStage.prefabAssetPath)
					       +"/"+Path.GetFileNameWithoutExtension(prefabStage.prefabAssetPath)
					       +"."+comp.gameObject.name+".obj";  // fake extension as we strip it
#endif
				} else
#endif
				if (!string.IsNullOrEmpty(comp.gameObject.scene.path))
				{
					path = Path.GetDirectoryName(comp.gameObject.scene.path) 
					       + "/" + Path.GetFileNameWithoutExtension(comp.gameObject.scene.path) 
					       + "." + comp.gameObject.name + ".obj"; // fake extension as we strip it
				}
			}

			// 4. give up
			if (string.IsNullOrEmpty(path))
			{
				path = "Assets/" + target.name;
			}

			return path;
		}

		static string GetLongestCommonPrefix(string[] s)
		{
			int k = s[0].Length;

			for (int i = 1; i < s.Length; i++)
			{
				k = Mathf.Min(k, s[i].Length);

				for (int j = 0; j < k; j++)
				{
					if (s[i][j] != s[0][j])
					{
						k = j;
						break;
					}
				}
			}

			return s[0].Substring(0, k);
		}

		/// <summary>
		/// Creates a relative path from one file or folder to another.
		/// https://stackoverflow.com/questions/275689/how-to-get-relative-path-from-absolute-path
		/// </summary>
		/// <param name="fromPath">Contains the directory that defines the start of the relative path.</param>
		/// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
		/// <returns>The relative path from the start directory to the end path.</returns>
		public static string GetRelativePath(string fromPath, string toPath)
		{
			if (string.IsNullOrEmpty(fromPath))
			{
				throw new ArgumentNullException("fromPath");
			}

			if (string.IsNullOrEmpty(toPath))
			{
				throw new ArgumentNullException("toPath");
			}

			Uri fromUri = !Path.HasExtension(fromPath) && !fromPath.EndsWith(Path.DirectorySeparatorChar.ToString())
				? new Uri($"{fromPath}{Path.DirectorySeparatorChar}")
				: new Uri(fromPath);

			// Uri toUri = !Path.HasExtension(toPath) && !toPath.EndsWith(Path.DirectorySeparatorChar.ToString())
			// 	? new Uri($"{fromPath}{Path.DirectorySeparatorChar}")
			// 	: new Uri(toPath);
			Uri toUri = new Uri(toPath);

			if (fromUri.Scheme != toUri.Scheme)
			{
				return toPath;
			}

			Uri relativeUri = fromUri.MakeRelativeUri(toUri);
			string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

			if (string.Equals(toUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
			{
				relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
			}

			return relativePath;
		}
	}
}
