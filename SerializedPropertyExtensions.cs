#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;


namespace cratesmith.assetui
{
	public static class SerializedPropertyExtensions 
	{
		public static string GetSanitizedPropertyPath(this SerializedProperty @this)
		{
			return @this.propertyPath.Replace(".Array.data[", "[");
		}

		public static System.Type GetSerializedPropertyType(this SerializedProperty @this)
		{
			// follow reflection up to match path and return type of last node

			// fix path for arrays
			var path = GetSanitizedPropertyPath(@this);
	
			var currentType = @this.serializedObject.targetObject.GetType();

			string[] slices = path.Split('.', '[');
			foreach (var slice in slices)
			{
				if (currentType == null)
				{
					Debug.LogErrorFormat("GetSerializedPropertyType Couldn't extract type from {0}:{1}",
						@this.serializedObject.targetObject.name,
						@this.propertyPath);

					return null;
				}

				// array element: get array type if this is an array element
				if (slice.EndsWith("]"))
				{
					if (currentType.IsArray)
					{
						currentType = currentType.GetElementType();
					}
					else if (currentType.IsGenericType && currentType.GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>)))
					{
						currentType = currentType.GetGenericArguments()[0];
					}
					else
					{
						Debug.LogErrorFormat("GetSerializedPropertyType unkown array/container type for {0}:{1}",
							@this.serializedObject.targetObject.name,
							@this.propertyPath);

						return null;
					}
				}
				else // field: find field by same name as slice and match to type
				{
					var type = currentType;
					while (type != null)
					{
						var fieldInfo = type.GetField(slice, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (fieldInfo == null)
						{
							type = type.BaseType;
							continue;
						}

						currentType = fieldInfo.FieldType;
						break;
					}
					// Assert.IsNotNull(type);
				}
			}

			return currentType;
		}
	}
}
#endif