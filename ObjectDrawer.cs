#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else 
using UnityEditor.Experimental.SceneManagement;
#endif
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

		static Dictionary<Type, (Type type, Attribute attribute)[]> s_cachedTypes = new Dictionary<Type, (Type type, Attribute attribute)[]>();

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
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
			Rect propertyFieldRect = PropertyFieldRight(position, property);
			EditorGUI.ObjectField(propertyFieldRect, property, objType, label);
			PropertyFieldLeft(position, property);
		}

		public static void ObjectField(Rect position, SerializedProperty property, Type objType)
		{
			Rect propertyFieldRect = PropertyFieldRight(position, property);
			EditorGUI.ObjectField(propertyFieldRect, property, objType);
			PropertyFieldLeft(position, property);
		}

		public static void PropertyField(Rect position, SerializedProperty property, GUIContent label)
		{
			Rect propertyFieldRect = PropertyFieldRight(position, property);
			EditorGUI.PropertyField(propertyFieldRect, property, label, true);
			PropertyFieldLeft(position, property);
		}
		static void PropertyFieldLeft(Rect position, SerializedProperty property)
		{
			if (property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceValue && !property.hasMultipleDifferentValues)
			{
				DrawScriptableObjectFoldout(position, property);
			}
		}
		static Rect PropertyFieldRight(Rect position, SerializedProperty property)
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

		static void DrawScriptableObjectFoldout(Rect position, SerializedProperty property)
		{
			ScriptableObject data = !property.hasMultipleDifferentValues
				? property.objectReferenceValue as ScriptableObject
				: null;

			if (!data)
			{
				return;
			}

			Rect foldoutRect = new Rect(position.position,
			                            new Vector2(position.width, EditorGUIUtility.singleLineHeight));

			GUI.color = Color.cyan;
			property.isExpanded = EditorGUI.Foldout(foldoutRect,
			                                        property.isExpanded,
			                                        "",
			                                        true);
			GUI.color = Color.white;
			if (!property.isExpanded)
			{
				return;
			}
			
			SerializedObject so = new SerializedObject(data);
			int count = 0;
			float fullHeight = 0f;
			SerializedProperty iterator = so.GetIterator();
			iterator.NextVisible(true);
			while (iterator.NextVisible(false))
			{
				float childHeight = EditorGUI.GetPropertyHeight(iterator, new GUIContent(iterator.displayName), true);
				fullHeight += childHeight + EditorGUIUtility.standardVerticalSpacing;
				++count;
			}
			fullHeight = Mathf.Max(EditorGUIUtility.singleLineHeight, fullHeight);
				
			var boxRect = new Rect(position.x + EditorGUI.indentLevel * 15f,
			                       position.y + EditorGUIUtility.singleLineHeight,
			                       position.width - EditorGUI.indentLevel * 15f + 2,
			                       Mathf.Max(fullHeight, EditorGUIUtility.singleLineHeight));

			GUI.Box(boxRect, "");

			if (count == 0)
				GUI.Label(boxRect, "No properties");

			var prevLabelWidth = EditorGUIUtility.labelWidth;
				
			EditorGUI.BeginChangeCheck();

			using (new EditorGUI.IndentLevelScope())
			{
				float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
				iterator = so.GetIterator();
				iterator.NextVisible(true);
				while (iterator.NextVisible(false))
				{
					var current = iterator.Copy();
					float childHeight = EditorGUI.GetPropertyHeight(iterator, new GUIContent(iterator.displayName), true);
					Rect childRect = new Rect(position.x, y, position.width, childHeight);
					EditorGUI.PropertyField(childRect, current, true);
					y += childHeight + EditorGUIUtility.standardVerticalSpacing;
					++count;
				}
			}


			if (EditorGUI.EndChangeCheck())
			{
				so.ApplyModifiedProperties();
			}
			EditorGUIUtility.labelWidth = prevLabelWidth;
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

		static Type[] s_AllTypes;
		static Type[] AllTypes => s_AllTypes = s_AllTypes 
		                                        ?? AppDomain.CurrentDomain.GetAssemblies().SelectMany(x =>
		                                        {
			                                        try
			                                        {
				                                        return x.GetTypes();
			                                        }
			                                        catch (ReflectionTypeLoadException e)
			                                        {
				                                        return e.Types.Where(t => t != null);
			                                        }
		                                        }).ToArray();
		
		static Type s_AssetFileNameExtensionAttributeType;
		static Type AssetFileNameExtensionAttributeType => s_AssetFileNameExtensionAttributeType = s_AssetFileNameExtensionAttributeType ?? AllTypes.Where(x => x.Name == "AssetFileNameExtensionAttribute").First();
		static PropertyInfo s_AssetFileNameExtensionAttributePreferredInfoPI;
		static PropertyInfo AssetFileNameExtensionAttributePreferredInfoPI => s_AssetFileNameExtensionAttributePreferredInfoPI = s_AssetFileNameExtensionAttributePreferredInfoPI ?? AssetFileNameExtensionAttributeType.GetProperty("preferredExtension");
		
		
		private static Attribute GetAssetAttribute(Type t)
		{
			if (t == null)
				return null;

			var caa = t.GetCustomAttribute<CreateAssetMenuAttribute>(true);
			if (caa!=null)
				return caa;

			while (t != null)
			{
				var asf = t.GetCustomAttribute(AssetFileNameExtensionAttributeType,true);
				if (asf!=null)
					return asf;

				t = t.BaseType;
			}

			return null;
		}
		
		private static string GetAttributeDisplayName(Type type, Attribute attribute)
		{
			if (attribute is CreateAssetMenuAttribute caa)
			{
				return !string.IsNullOrEmpty(caa.menuName)
					? caa.menuName.Substring(caa.menuName.LastIndexOf('/') + 1)
					: type.Name;
			}
			else if (AssetFileNameExtensionAttributeType.IsAssignableFrom(attribute.GetType()))
			{
				return type.Name;
			}
			throw new ArgumentException("Unkown attribute type");
		}

		private static string GetAttributeFileName(Type type, Attribute attribute)
		{
			if (attribute is CreateAssetMenuAttribute caa)
				return caa.fileName;
			else if (AssetFileNameExtensionAttributeType.IsAssignableFrom(attribute.GetType()))
			{
				return null;
			}

			throw new ArgumentException("Unkown attribute type");
		}
		
		private static string GetAttributeFileExtension(Type type, Attribute attribute)
		{
			if (attribute is CreateAssetMenuAttribute caa)
				return $"{type.Name}.asset";
			else if (AssetFileNameExtensionAttributeType.IsAssignableFrom(attribute.GetType()))
			{
				return (string)AssetFileNameExtensionAttributePreferredInfoPI.GetValue(attribute);
			}

			throw new ArgumentException("Unkown attribute type");
		}
		
		static bool DrawCreateScriptableObject(Rect position, SerializedProperty property, Rect popupWindowRect)
		{
			Type propType = property.GetSerializedPropertyType();

			if (propType == null || !typeof(ScriptableObject).IsAssignableFrom(propType))
			{
				return false;
			}

			if (!s_cachedTypes.TryGetValue(propType, out (Type type, Attribute attribute)[] validTypes))
			{
				var candidates = AllTypes.Where(t => t != null && !t.IsAbstract && propType.IsAssignableFrom(t))
				                         .Select(t => (type: t, attribute: GetAssetAttribute(t))).ToArray();
				
				s_cachedTypes[propType] = validTypes = candidates
				                                       .Where(t => t.attribute != null)
				                                       .ToArray();
			}

			if (validTypes.Length == 0)
			{
				return false;
			}

			if (ImageButton(position, NewButton, "Create Asset..."))
			{
				string filepathPrefix = property.serializedObject.isEditingMultipleObjects
					? GetPath(property.serializedObject.targetObject)
					: GetLongestCommonPrefix(property.serializedObject.targetObjects
					                                 .Select(x => Path.GetDirectoryName(GetPath(x))).ToArray());

				GUIContent[] _options = validTypes.Select(x => (name: GetAttributeDisplayName(x.type, x.attribute), icon: EditorIconUtility.GetIcon(x.type)))
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
					                                    (Type type, Attribute attribute) typeData = validTypes[index];

					                                    var attribFilename = GetAttributeFileName(typeData.type, typeData.attribute);
					                                    string defaultName = string.IsNullOrEmpty(attribFilename)
						                                    ? Path.GetFileNameWithoutExtension(defaultOutputPath)
						                                    : attribFilename;

					                                    var displayName = GetAttributeDisplayName(typeData.type, typeData.attribute);
					                                    var extension = GetAttributeFileExtension(typeData.type, typeData.attribute);
					                                    
					                                    string filePath = EditorUtility.SaveFilePanel($"Create {displayName}",
					                                                                                  $"{filepathPrefix}",
					                                                                                  defaultName,
					                                                                                  extension);

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
#endif