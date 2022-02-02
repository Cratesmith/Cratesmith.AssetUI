#if UNITY_EDITOR
using cratesmith.assetui;
using UnityEditor;
#endif
using UnityEngine;

namespace Cratesmith.AssetUI
{
	public interface INoLabelDrawer
	{
	}

	public class NoLabelPropertyAttribute : PropertyAttribute
	{
	}

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(INoLabelDrawer), true)]
	[CustomPropertyDrawer(typeof(NoLabelPropertyAttribute), true)]
	public class NoLabelDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return EditorGUI.GetPropertyHeight(property, GUIContent.none, true);
		}	

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (property.propertyType == SerializedPropertyType.ObjectReference)
			{
				ObjectDrawer.ObjectField(position, property, fieldInfo.FieldType.GetElementType() ?? fieldInfo.FieldType, GUIContent.none);
			} else
			{
				EditorGUI.PropertyField(position, property, GUIContent.none, true);
			}
		}
	}
#endif
}
