#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using static UnityEditor.AssetDatabase;

namespace cratesmith.assetui
{
	[CustomPropertyDrawer(typeof(Object), true)]
	public class ObjectDrawer : PropertyDrawer
	{
		static Texture2D s_ExpandButton;
		static Texture2D s_NewButton;
		
		static Texture2D ExpandButton => s_ExpandButton
			? s_ExpandButton
			: (s_ExpandButton = LoadAssetAtPath<Texture2D>(
				AssetDatabase.GUIDToAssetPath(
					AssetDatabase.FindAssets("objectdrawer_expand t:texture").FirstOrDefault())));

		static Texture2D NewButton => s_NewButton
			? s_NewButton
			: (s_NewButton = LoadAssetAtPath<Texture2D>(
				AssetDatabase.GUIDToAssetPath(
					AssetDatabase.FindAssets("objectdrawer_new t:texture").FirstOrDefault())));


		static Dictionary<Type, (Type type, CreateAssetMenuAttribute attribute)[]> s_cachedTypes = new Dictionary<Type,(Type type, CreateAssetMenuAttribute attribute)[]>();

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => CalcPropertyHeight(property, label);

		static float CalcPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var baseHeight = EditorGUIUtility.singleLineHeight;
			var data = property.isExpanded && !property.hasMultipleDifferentValues
				? property.objectReferenceValue as ScriptableObject
				: null;
			if (data)
			{
				baseHeight += EditorGUIUtility.standardVerticalSpacing*2;

				var so = new SerializedObject(data);
				var iterator = so.GetIterator();
				iterator.NextVisible(true);
				while (iterator.NextVisible(false))
				{
					baseHeight += EditorGUI.GetPropertyHeight(iterator, label, true)
					              + EditorGUIUtility.standardVerticalSpacing;
				}
			}

			return baseHeight;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{ 
			PropertyField(position, property, label);
		}
		
		public static void PropertyField(Rect position, SerializedProperty property, GUIContent label)
		{

			var popupWindowRect = new Rect(GUIUtility.GUIToScreenPoint(position.position - Vector2.right * 416),
				new Vector2(400, 500));
			var buttonWidth = 20;
			var propertyFieldRect = position;

			if (!property.hasMultipleDifferentValues 
			    && property.propertyType == SerializedPropertyType.ObjectReference)
			{
				if (property.objectReferenceValue)
				{
					propertyFieldRect = new Rect(position.position, new Vector2(position.width - buttonWidth, EditorGUIUtility.singleLineHeight));
					var buttonRect = new Rect(propertyFieldRect.xMax, position.y, buttonWidth, EditorGUIUtility.singleLineHeight);
					DrawPopoutInspector(buttonRect, property, popupWindowRect);
				}
				else 
				{
					var propType = property.GetSerializedPropertyType();
					if (propType != null && typeof(ScriptableObject).IsAssignableFrom(propType))
					{
						propertyFieldRect = new Rect(position.position, new Vector2(position.width - buttonWidth, EditorGUIUtility.singleLineHeight));
						var buttonRect = new Rect(propertyFieldRect.xMax, position.y, buttonWidth, EditorGUIUtility.singleLineHeight);
						DrawCreateScriptableObject(buttonRect,property, popupWindowRect);
					}
				}
			}
			
			EditorGUI.PropertyField(propertyFieldRect, property, label, true);

			if (property.objectReferenceValue 
			    && !property.hasMultipleDifferentValues)
			{
				DrawScriptableObjectFoldout(position, property, label);
			}
		}

		static void DrawScriptableObjectFoldout(Rect position, SerializedProperty property, GUIContent label)
		{
			var data = !property.hasMultipleDifferentValues
				? property.objectReferenceValue as ScriptableObject
				: null;
			if (data)
			{
				var y = position.y
				        + EditorGUIUtility.singleLineHeight
				        + EditorGUIUtility.standardVerticalSpacing;
				
				var foldoutRect = new Rect(position.position,
					new Vector2(position.width, EditorGUIUtility.singleLineHeight));

				GUI.color = Color.cyan;
				property.isExpanded = EditorGUI.Foldout(foldoutRect,
					property.isExpanded,
					"",
					true);
				GUI.color = Color.white;

				if (property.isExpanded)
				{
					using (new EditorGUI.IndentLevelScope())
					{
						var fullHeight = CalcPropertyHeight(property, label);
						GUI.Box(new Rect(position.x + EditorGUI.indentLevel * 15f - 2,
								position.y + EditorGUIUtility.singleLineHeight,
								position.width - +EditorGUI.indentLevel * 15f + 4,
								fullHeight - EditorGUIUtility.singleLineHeight),
							"");
						y += EditorGUIUtility.standardVerticalSpacing;
					}

					var so = new SerializedObject(data);
					EditorGUI.BeginChangeCheck();

					var iterator = so.GetIterator();
					iterator.NextVisible(true);
					while (iterator.NextVisible(false))
					{
						var childHeight = EditorGUI.GetPropertyHeight(iterator);
						var childRect = new Rect(position.x+15f, y, position.width-15f, childHeight);
						EditorGUI.PropertyField(childRect, iterator, true);
						y += EditorGUI.GetPropertyHeight(iterator, new GUIContent(iterator.displayName), true)
							+ EditorGUIUtility.standardVerticalSpacing;
					}

					if (EditorGUI.EndChangeCheck())
					{
						so.ApplyModifiedProperties();
					}
				}
			}
		}

		static void DrawPopoutInspector(Rect position, SerializedProperty property, Rect popupWindowRect)
		{
			if (property.objectReferenceValue != null && !property.hasMultipleDifferentValues)
			{
				var wasEnabled = GUI.enabled;
				GUI.enabled = true;

				if (ImageButton(position,ExpandButton))
				{
					PopupEditorWindow.Create(property.objectReferenceValue, popupWindowRect);
				}
				GUI.enabled = wasEnabled;
			}
		}

		static void DrawCreateScriptableObject(Rect position, SerializedProperty property, Rect popupWindowRect)
		{
			if (property.objectReferenceValue != null) 
				return;
		
			var propType = property.GetSerializedPropertyType();
			if (propType == null || !typeof(ScriptableObject).IsAssignableFrom(propType))
				return;

			if (!s_cachedTypes.TryGetValue(propType, out var validTypes))
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
				return;
		
			// var indentedPos = EditorGUI.IndentedRect(position);
			// var buttonRect = new Rect(indentedPos.x - indentedPos.height, indentedPos.y, indentedPos.height,
			// 	indentedPos.height);
			if (ImageButton(position, NewButton))
			{
				var filepathPrefix = property.serializedObject.isEditingMultipleObjects
					? GetPath(property.serializedObject.targetObject)
					: GetLongestCommonPrefix(property.serializedObject.targetObjects
						.Select(x => Path.GetDirectoryName(GetPath(x))).ToArray());
			
				var _options = validTypes.Select(x => (name: !string.IsNullOrEmpty(x.attribute.menuName)
						? x.attribute.menuName.Substring(x.attribute.menuName.LastIndexOf('/') + 1)
						: x.type.Name, icon:EditorIconUtility.GetIcon(x.type)))
					.Select(x=>new GUIContent(x.name, x.icon))
					.ToArray();

				var propertyPath = property.GetSanitizedPropertyPath();
				var defaultOutputPath = property.serializedObject.targetObject is Component component 
					? GenerateUniqueAssetPath($"{filepathPrefix}/{property.serializedObject.targetObject.name}_{component.GetType().Name}.{propertyPath}")
					: GenerateUniqueAssetPath($"{filepathPrefix}/{property.serializedObject.targetObject.name}_{propertyPath}");

				var icon = EditorIconUtility.GetIcon(propType);
				CreateScriptableObjectWindow.Create($"Create {propType.Name}",
					(index) =>
					{
						var defaultName = Path.GetFileNameWithoutExtension(defaultOutputPath);
						var typeData = validTypes[index];
						var filePath = EditorUtility.SaveFilePanel($"Create {typeData.attribute.menuName}",
							$"{filepathPrefix}",
							defaultName,
							$"{typeData.type.Name}.asset");

						if (string.IsNullOrEmpty(filePath)) 
							return;

						
						filePath = GetRelativePath(Directory.GetParent(Application.dataPath).FullName+Path.DirectorySeparatorChar, filePath);
						
						var instance = ScriptableObject.CreateInstance(validTypes[index].type);
						CreateAsset(instance, filePath);
						EditorGUIUtility.PingObject(instance);
						property.objectReferenceValue = instance;
						property.serializedObject.ApplyModifiedProperties();
						PopupEditorWindow.Create(property.objectReferenceValue, popupWindowRect);
					},
					_options,
					defaultOutputPath,
					icon);
			}
		}

		static bool ImageButton(Rect position, Texture2D buttonImg)
		{
			
			var result = GUI.Button(position, "");
			var imgSize = new Vector2(buttonImg.width, buttonImg.height);
			var imgRect = new Rect(position.center - imgSize / 2f, imgSize);

			var prevColor = GUI.color;
			GUI.color = EditorGUIUtility.isProSkin ? Color.white : Color.gray;
			GUI.DrawTexture(imgRect, buttonImg);
			GUI.color = prevColor;
			return result;
		}

		private static string GetPath(Object target)
		{
			// 1. check for an asset path (ScriptableObject)
			var path = GetAssetPath(target);

			// 2. check for a prefab path (could be referenced by a monobehaviour)
			var prefab = PrefabUtility.GetCorrespondingObjectFromSource(target);
			if (prefab !=null && string.IsNullOrEmpty(path))
			{
				path = GetAssetPath(prefab);
			}

			// 3. check for a scene path
			var comp = target as Component;
			if (comp != null)
			{
#if UNITY_2018_3_OR_NEWER
				var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
				if (prefabStage!=null && prefabStage.IsPartOfPrefabContents(comp.gameObject))
				{
#if UNITY_2020_1_OR_NEWER					
					path = Path.GetDirectoryName(prefabStage.assetPath)
					       +"/"+Path.GetFileNameWithoutExtension(prefabStage.assetPath)
					       +"."+comp.gameObject.name+".obj";  // fake extension as we strip it
#else
					path = Path.GetDirectoryName(prefabStage.prefabAssetPath)
					       +"/"+Path.GetFileNameWithoutExtension(prefabStage.prefabAssetPath)
					       +"."+comp.gameObject.name+".obj";  // fake extension as we strip it
#endif
				}
				else
#endif
				if (comp.gameObject.scene.IsValid())
				{
					path = Path.GetDirectoryName(comp.gameObject.scene.path)
					       +"/"+Path.GetFileNameWithoutExtension(comp.gameObject.scene.path)
					       +"."+comp.gameObject.name+".obj";  // fake extension as we strip it
				}

			}

			// 4. give up
			if (string.IsNullOrEmpty(path))
			{
				path = "Assets/" + target.name;
			}

			return path;
		}
	
		private static string GetLongestCommonPrefix(string[] s)
		{
			int k = s[0].Length;
			for (int i = 1; i < s.Length; i++)
			{
				k = Mathf.Min(k, s[i].Length);
				for (int j = 0; j < k; j++)
					if (s[i][j] != s[0][j])
					{
						k = j;
						break;
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
			var toUri = new Uri(toPath);

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