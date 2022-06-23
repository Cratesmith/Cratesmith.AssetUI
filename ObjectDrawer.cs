#if UNITY_EDITOR
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else 
using UnityEditor.Experimental.SceneManagement;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using static UnityEditor.AssetDatabase;
using Object = UnityEngine.Object;

namespace cratesmith.assetui
{
	[CustomPropertyDrawer(typeof(Object), true)]
	public class ObjectDrawer : PropertyDrawer
	{
		static Texture2D                 s_ExpandButton;
		static Texture2D                 s_NewButton;
		static HashSet<ScriptableObject> s_CyclicCheck = new HashSet<ScriptableObject>();

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

		static Dictionary<Type, (Type type, string displayName, string fileName, string fileExtension)[]> s_cachedTypes = new Dictionary<Type, (Type type, string displayName, string fileName, string fileExtension)[]>();

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
            s_CyclicCheck.Clear();
            PropertyField(position, property, label);
            s_CyclicCheck.Clear();
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
			Rect popupWindowRect = new Rect(EditorGUIUtility.GUIToScreenPoint(new Vector2(position.xMin,position.yMin+EditorGUIUtility.singleLineHeight)),
			                                new Vector2(position.width, 500));
			
			int singleButtonWidth = 20;
			Rect propertyFieldRect = position;

			if (DrawPickAsset(position, property, propertyFieldRect, singleButtonWidth))
			{
				propertyFieldRect.xMax -= singleButtonWidth;
			}
			
			// Draw "Create" for supported assets
			if (property.propertyType == SerializedPropertyType.ObjectReference)
			{
				Rect createBtnRect = new Rect(propertyFieldRect.xMax - singleButtonWidth, position.y, singleButtonWidth, EditorGUIUtility.singleLineHeight);
				if (DrawCreateAsset(createBtnRect, property, popupWindowRect))
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
		
		static bool DrawPickAsset(Rect position, SerializedProperty property, Rect propertyFieldRect, int singleButtonWidth)
		{
			Type propType = property.GetSerializedPropertyType();
			var canScenePick = ObjectDrawerPickerTool.CanPickProperty(property);
			var canAssetPick = typeof(GameObject).IsAssignableFrom(propType) || typeof(Component).IsAssignableFrom(propType);
			Rect buttonRect = new Rect(propertyFieldRect.xMax - singleButtonWidth, position.y, singleButtonWidth, EditorGUIUtility.singleLineHeight);

			var wasScenePicking = ObjectDrawerPickerTool.IsPickingFor(property);
			var isScenePicking = false;

			if (!canScenePick && !canAssetPick)
				return false;
			
			// Draw "Picker tool"
			if (canScenePick && (!canAssetPick||wasScenePicking))
			{
				isScenePicking = ImageToggle(buttonRect, ObjectDrawerPickerTool.PickerIcon, wasScenePicking, "Pick from scene");
			}
			else if (canAssetPick && canScenePick)
			{
				var options = new []
				{
					"Eyedrop from scene...",
					"Pick from project assets..."
				};

				var result = ImagePopup(buttonRect, ObjectDrawerPickerTool.PickerIcon, "Pick from scene/project", options);

				switch (result)
				{
					case 0: 
						isScenePicking = true;
						break;
					
					case 1:
						DoProjectAssetPick(position, property, propType);
						break;				
				}
			}
			else
			{
				if (ImageButton(buttonRect, ObjectDrawerPickerTool.PickerIcon, "Pick from project"))
				{
					DoProjectAssetPick(position, property, propType);
				}
			}
			
			if (!wasScenePicking && isScenePicking)
			{
				ObjectDrawerPickerTool.DoPicker(property);
			} else if (!isScenePicking && wasScenePicking)
			{
				ObjectDrawerPickerTool.Cancel(); 
			}

			return true;
		}
		
		static void DoProjectAssetPick(Rect position, SerializedProperty property, Type propType)
		{
			(string path, Type type)[] validAssets;

			if (typeof(Component).IsAssignableFrom(propType))
			{
				if (s_CachedScripts == null)
					s_CachedScripts = MonoImporter.GetAllRuntimeMonoScripts();

				var validScripts = s_CachedScripts
				                   .Where(x => x && propType.IsAssignableFrom(x.GetClass()))
				                   .ToDictionary(AssetDatabase.GetAssetPath);

				if (validScripts.Count > 0)
				{
					var validScriptPathsSet = new HashSet<string>(validScripts.Keys);
					var allPrefabs = AssetDatabase.FindAssets("t:prefab")
					                              .Select(AssetDatabase.GUIDToAssetPath)
					                              .Select(x=>(path:x, dependencies:AssetDatabase.GetDependencies(x,false).ToHashSet()))
					                              .ToArray();
					
					EditorUtility.DisplayProgressBar("Prefab lookup search", "Quick search. Won't find built in types. Will be faster on repeat uses", 0.5f);

					var basePrefabsSet = allPrefabs.Where(x => validScriptPathsSet.Overlaps(x.dependencies))
					                               .Select(x=>x.path)
					                                .ToHashSet();

					while(true)
					{
						var newVariants = allPrefabs.Where(x => x.dependencies.Overlaps(basePrefabsSet) && !basePrefabsSet.Contains(x.path))
						                            .Select(x => (path: x.path, obj: AssetDatabase.LoadAssetAtPath<GameObject>(x.path)))
						                            .Where(x =>PrefabUtility.IsPartOfVariantPrefab(x.obj) && basePrefabsSet.Contains(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(x.obj))))
						                            .Select(x=>x.path)
						                            .ToArray();

						if (newVariants.Length == 0)
							break;

						basePrefabsSet.UnionWith(newVariants);
					}

					validAssets = basePrefabsSet.Select(AssetDatabase.LoadAssetAtPath<GameObject>)
					                           .SelectMany(x=>x.GetComponents(propType))
					                           .Select(x=>(path:AssetDatabase.GetAssetPath(x), type:x.GetType()))
					                           .ToArray();

					// validAssets = AssetDatabase.FindAssets("t:prefab")
					//                            .Select(AssetDatabase.GUIDToAssetPath)
					//                            .Where(x=>validScriptPathsSet.Overlaps(AssetDatabase.GetDependencies(x, false)))
					//                            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
					//                            .SelectMany(x=>x.GetComponents(propType))
					//                            .Select(x=>(path:AssetDatabase.GetAssetPath(x), type:x.GetType()))
					//                            .ToArray();
					
					EditorUtility.ClearProgressBar();
				} else
				{
					EditorUtility.DisplayProgressBar("Project wide prefab search", "Slower, but finds inbuilt types. Will be faster on repeat uses", 0.5f);
					validAssets = AssetDatabase.FindAssets("t:prefab")
					                           .Select(AssetDatabase.GUIDToAssetPath)
					                           .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
					                           .SelectMany(x=>x.GetComponents(propType))
					                           .Select(x=>(path:AssetDatabase.GetAssetPath(x), type:x.GetType()))
					                           .ToArray();
					EditorUtility.ClearProgressBar();
				}
			} else
			{
				Assert.IsTrue(typeof(GameObject).IsAssignableFrom(propType));
				validAssets = AssetDatabase.FindAssets("t:prefab")
				                           .Select(x=>(path:AssetDatabase.GUIDToAssetPath(x), type:typeof(GameObject)))
				                           .ToArray();
			}

			var guiOptions = validAssets.Select(x => new GUIContent($"{Path.GetFileName(x.path)} ({x.type.Name})", EditorIconUtility.GetIcon(x.type)))
			    .ToArray();

			var callbackProperty = property.Copy();
			var callbackSO = callbackProperty.serializedObject;
			OptionPopupWindow.Create($"Pick Project asset {propType.Name}",
			                         index =>
			                         {
				                         var selection = validAssets[index];
				                         property.objectReferenceValue = AssetDatabase.LoadAssetAtPath(selection.path, selection.type);
				                         callbackSO.ApplyModifiedProperties();
			                         },
			                         guiOptions,
			                         EditorIconUtility.GetIcon(propType),
									"prefab root components only");
		}

		static void DrawScriptableObjectFoldout(Rect position, SerializedProperty property)
		{
			ScriptableObject data = !property.hasMultipleDifferentValues
				? property.objectReferenceValue as ScriptableObject
				: null;

			if (!data || !s_CyclicCheck.Add(data))
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

			if (property.isExpanded)
			{
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

				//using (new EditorGUI.IndentLevelScope())
				{
					float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
					iterator = so.GetIterator();
					iterator.NextVisible(true);

					while (iterator.NextVisible(false))
					{
						var current = iterator.Copy();
						float childHeight = EditorGUI.GetPropertyHeight(iterator, new GUIContent(iterator.displayName), true);
						Rect childRect = new Rect(position.x + 15, y, position.width - 15, childHeight);
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

			s_CyclicCheck.Remove(data);
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
		static MonoScript[] s_CachedScripts;
		static PropertyInfo AssetFileNameExtensionAttributePreferredInfoPI => s_AssetFileNameExtensionAttributePreferredInfoPI = s_AssetFileNameExtensionAttributePreferredInfoPI ?? AssetFileNameExtensionAttributeType.GetProperty("preferredExtension");


		private static bool CanCreateAssetType(Type t)
		{
			if (t == null)
				return false;
			
			if (typeof(ScriptableObject).IsAssignableFrom(t))
			{
				return GetAssetAttribute(t) != null;
			}
			return typeof(GameObject).IsAssignableFrom(t) || typeof(Component).IsAssignableFrom(t);
		}
		
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
		
		private static string GetAssetDisplayName(Type type)
		{
			if (typeof(ScriptableObject).IsAssignableFrom(type))
			{
				var attribute = GetAssetAttribute(type);
				if (attribute is CreateAssetMenuAttribute caa)
				{
					return !string.IsNullOrEmpty(caa.menuName)
						? caa.menuName.Substring(caa.menuName.LastIndexOf('/') + 1)
						: type.Name;
				} else if (AssetFileNameExtensionAttributeType.IsAssignableFrom(attribute.GetType()))
				{
					return type.Name;
				}

				throw new ArgumentException("Unkown attribute type");
			}

			return type.Name;
		}

		private static string GetAssetFileName(Type type)
		{
			if (typeof(ScriptableObject).IsAssignableFrom(type))
			{
				var attribute = GetAssetAttribute(type);
				if (attribute is CreateAssetMenuAttribute caa)
					return caa.fileName;
				else if (AssetFileNameExtensionAttributeType.IsAssignableFrom(attribute.GetType()))
				{
					return null;
				}

				throw new ArgumentException("Unkown attribute type");
			}
			
			return null;
		}

		private static string GetAssetFileExtension(Type type)
		{
			if (typeof(ScriptableObject).IsAssignableFrom(type))
			{
				var attribute = GetAssetAttribute(type);

				if (attribute is CreateAssetMenuAttribute caa)
					return $"{type.Name}.asset";
				else if (AssetFileNameExtensionAttributeType.IsAssignableFrom(attribute.GetType()))
				{
					return (string)AssetFileNameExtensionAttributePreferredInfoPI.GetValue(attribute);
				}

				throw new ArgumentException("Unkown attribute type");
			}

			
			if (typeof(GameObject).IsAssignableFrom(type))
				return "prefab";
			else if(typeof(Component).IsAssignableFrom(type))
				return $"{type.Name}.prefab";

			return null;
		}

		static bool DrawCreateAsset(Rect position, SerializedProperty property, Rect popupWindowRect)
		{
			using var scope = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
			Type propType = property.GetSerializedPropertyType();
			if (propType == null)
			{
				return false;
			}

			if (!s_cachedTypes.TryGetValue(propType, out (Type type, string displayName, string fileName, string fileExtension)[] validTypes))
			{
				var candidates = AllTypes.Where(t => t != null && !t.IsAbstract && propType.IsAssignableFrom(t))
				                         .Where(t=>CanCreateAssetType(t))
				                         .Select(t => (type: t, menuName:GetAssetDisplayName(t), fileName: GetAssetFileName(t), extension:GetAssetFileExtension(t))).ToArray();
				
				s_cachedTypes[propType] = validTypes = candidates.ToArray();
			}

			if (validTypes.Length == 0)
			{
				return false;
			}

			var selection = property.serializedObject.isEditingMultipleObjects || !(property.serializedObject.targetObject is ScriptableObject) || !(typeof(ScriptableObject).IsAssignableFrom(propType)) 
				? (ImageButton(position, NewButton, "New Asset...") ? 0:-1)
				: ImagePopup(position, NewButton, "New Asset...", new [] {"New external asset...", "New sub asset..." });
			
			if (selection!=-1)
			{
				GUIContent[] _options = validTypes.Select(x => (name: x.displayName, icon: EditorIconUtility.GetIcon(x.type)))
				                                  .Select(x => new GUIContent(x.name, x.icon))
				                                  .ToArray();


				Texture2D icon = EditorIconUtility.GetIcon(propType);
				var callbackPropery = property.Copy();

				switch (selection)
				{
					case 0: 
						OptionPopupWindow.Create($"Create External Asset {propType.Name}",
						                         index =>
						                         {
							                         (Type _, string displayName, string fileName, string fileExtension) = validTypes[index];
														
							                         if (!CreateExternalAssetPopup(callbackPropery, validTypes[index].type, fileName, fileExtension, displayName, out Object instance))
							                         {
								                         return;
							                         }

							                         EditorGUIUtility.PingObject(instance);
							                         SerializedProperty prop = new SerializedObject(property.serializedObject.targetObjects).FindProperty(callbackPropery.propertyPath);
							                         prop.objectReferenceValue = instance;
							                         prop.serializedObject.ApplyModifiedProperties();
						                         },
						                         _options,
						                         icon);
						break;
					case 1: 
						OptionPopupWindow.Create($"Create Sub Asset {propType.Name}",
												index =>
												{
													(Type _, string _, string _, string fileExtension) = validTypes[index];

													Type type = validTypes[index].type;

													Object instance = null;

													if (callbackPropery.serializedObject.isEditingMultipleObjects)
													    return;
													
													var name = callbackPropery.serializedObject.targetObject is Component 
													    ? $"{callbackPropery.serializedObject.targetObject.name}.{callbackPropery.GetSanitizedPropertyPath()}"
													    : $"{callbackPropery.GetSanitizedPropertyPath()}";
													
													TextFieldPopupWindow.Create($"{fileExtension}", defaultValue:name, icon:EditorIconUtility.GetIcon(type), action: value =>
													{
														instance = CreateSubAssetOfType(type, property.serializedObject.targetObject, $"{value}");
													
														if (!instance)
														{
															return;
														}

														EditorGUIUtility.PingObject(instance);
														SerializedProperty prop = new SerializedObject(callbackPropery.serializedObject.targetObjects).FindProperty(callbackPropery.propertyPath);
														prop.objectReferenceValue = instance;
														prop.serializedObject.ApplyModifiedProperties();
													});
												},
												_options,
												icon);
						break;
				}
			}

			return true;
		}
		static bool CreateExternalAssetPopup(SerializedProperty property, Type type, string fileName, string fileExtension, string displayName, out Object instance)
		{
			string filepathPrefix = property.serializedObject.isEditingMultipleObjects
				? GetPath(property.serializedObject.targetObject)
				: GetLongestCommonPrefix(property.serializedObject.targetObjects
				                                 .Select(x => Path.GetDirectoryName(GetPath(x))).ToArray());

			string defaultOutputPath = GenerateUniqueAssetPath($"{filepathPrefix}/{property.serializedObject.targetObject.name}");

			string defaultName = string.IsNullOrEmpty(fileName)
				? Path.GetFileNameWithoutExtension(defaultOutputPath)
				: fileName;

			var extension = fileExtension;

			string filePath = EditorUtility.SaveFilePanel($"Create {displayName}",
			                                              $"{filepathPrefix}",
			                                              defaultName,
			                                              extension);

			if (string.IsNullOrEmpty(filePath))
			{
				instance = null;
				return false;
			}

			filePath = GetRelativePath(Directory.GetParent(Application.dataPath).FullName + Path.DirectorySeparatorChar, filePath);

			Object asset;
			(asset, instance) = CreateAssetOfType(type, filePath);
			return true;
		}

		static (Object asset, Object instance) CreateAssetOfType(Type type, string filePath)
		{
			Object asset = null;
			Object instance = null;

			if (typeof(ScriptableObject).IsAssignableFrom(type))
			{
				asset = instance = ScriptableObject.CreateInstance(type);
				CreateAsset(asset, filePath);
			}
			else if (typeof(Component).IsAssignableFrom(type))
			{
				var go = new GameObject(Path.GetFileNameWithoutExtension(filePath),new []{type});
				var goAsset = PrefabUtility.SaveAsPrefabAsset(go, filePath);
				Object.DestroyImmediate(go);
				instance = goAsset.GetComponent(type);
				asset = goAsset;
					
			} else if (typeof(GameObject).IsAssignableFrom(type))
			{
				var go = new GameObject(Path.GetFileNameWithoutExtension(filePath));
				var goAsset = PrefabUtility.SaveAsPrefabAsset(go, filePath);
				Object.DestroyImmediate(go);
				asset = instance = goAsset;
			}

			return (asset, instance);
		}
		
		static Object CreateSubAssetOfType(Type type, Object parentAsset, string assetName)
		{
			Object instance = null;

			if (typeof(ScriptableObject).IsAssignableFrom(type))
			{
				instance = ScriptableObject.CreateInstance(type);
				instance.name = assetName;
				AddObjectToAsset(instance, parentAsset);
				SaveAssets();
			}

			return instance;
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
		
		static int ImagePopup(Rect position, Texture2D buttonImg, string tooltip, string[] options)
		{
			int result = EditorGUI.Popup(position, -1, options,"button");
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